using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

/// <summary>
/// Orquesta el flujo completo de un analisis:
/// PDF -> extraccion (CUIT / razon social / texto) -> instructivo -> OpenAI (web search)
/// -> mapeo del JSON al modelo de dominio -> persistencia (en memoria).
///
/// El trabajo pesado corre en background con Task.Run; la UI hace polling contra
/// <see cref="ObtenerPorId"/> hasta que el estado sea Completo o Error.
/// </summary>
public class AnalisisService : IAnalisisService
{
    private readonly ConcurrentDictionary<Guid, Analisis> _store = new();
    private readonly IInstructivoService _instructivos;
    private readonly PdfExtractorService _pdf;
    private readonly OpenAiRiesgoService _openAi;
    private readonly ILogger<AnalisisService> _log;

    public AnalisisService(
        IInstructivoService instructivos,
        PdfExtractorService pdf,
        OpenAiRiesgoService openAi,
        ILogger<AnalisisService> log)
    {
        _instructivos = instructivos;
        _pdf = pdf;
        _openAi = openAi;
        _log = log;
    }

    public Analisis IniciarAnalisis(byte[] pdfBytes, string nombreArchivo, Guid instructivoId, string usuario)
    {
        var analisis = new Analisis
        {
            InstructivoId = instructivoId,
            NombreArchivoOriginal = nombreArchivo,
            UsuarioSolicitante = string.IsNullOrWhiteSpace(usuario) ? "desconocido" : usuario.Trim(),
            EstadoAnalisis = EstadoAnalisis.Pendiente
        };
        _store[analisis.AnalisisId] = analisis;

        // Fire-and-forget: el resultado se consulta por polling.
        _ = Task.Run(() => ProcesarAsync(analisis.AnalisisId, pdfBytes));

        return analisis;
    }

    public Analisis? ObtenerPorId(Guid analisisId) =>
        _store.TryGetValue(analisisId, out var a) ? a : null;

    public IReadOnlyList<Analisis> ObtenerTodos() =>
        _store.Values.OrderByDescending(a => a.FechaSolicitud).ToList();

    private async Task ProcesarAsync(Guid analisisId, byte[] pdfBytes)
    {
        if (!_store.TryGetValue(analisisId, out var analisis))
            return;

        try
        {
            analisis.EstadoAnalisis = EstadoAnalisis.Procesando;

            var instructivo = _instructivos.ObtenerPorId(analisis.InstructivoId)
                ?? throw new InvalidOperationException($"El instructivo {analisis.InstructivoId} no existe.");

            var extraccion = _pdf.Extraer(pdfBytes);
            analisis.Cuit = extraccion.Cuit ?? string.Empty;
            analisis.RazonSocial = extraccion.RazonSocial ?? string.Empty;

            if (string.IsNullOrWhiteSpace(extraccion.TextoCompleto))
                throw new InvalidOperationException("No se pudo extraer texto del PDF (¿es un PDF escaneado sin OCR?).");

            var dto = await _openAi.AnalizarAsync(
                instructivo.ContenidoPrompt,
                extraccion.Cuit,
                extraccion.RazonSocial,
                extraccion.TextoCompleto);

            MapearResultado(analisis, dto);

            analisis.EstadoAnalisis = EstadoAnalisis.Completo;
            analisis.FechaRespuesta = DateTime.UtcNow;
            _log.LogInformation("Analisis {Id} completo ({Hallazgos} hallazgos).", analisisId, analisis.Hallazgos.Count);
        }
        catch (Exception ex)
        {
            analisis.EstadoAnalisis = EstadoAnalisis.Error;
            analisis.MensajeError = ex.Message;
            analisis.FechaRespuesta = DateTime.UtcNow;
            _log.LogError(ex, "Analisis {Id} termino con error.", analisisId);
        }
    }

    private static void MapearResultado(Analisis analisis, RiesgoRespuestaDto dto)
    {
        analisis.EmpresaAnalizada = dto.EmpresaAnalizada ?? string.Empty;
        analisis.EmpresasVinculadas = dto.EmpresasVinculadasRelevantes?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList() ?? new List<string>();

        analisis.Hallazgos = (dto.Hallazgos ?? new List<HallazgoDto>())
            .Select(h => new Hallazgo
            {
                Descripcion = h.Descripcion ?? string.Empty,
                Fuente = h.Fuente ?? string.Empty,
                Severidad = ParseSeveridad(h.Severidad),
                ImpactoCrediticio = h.ImpactoCrediticio ?? string.Empty,
                EmpresaVinculada = string.IsNullOrWhiteSpace(h.EmpresaVinculada) ? null : h.EmpresaVinculada!.Trim()
            })
            .ToList();

        analisis.ConclusionEjecutiva = dto.ConclusionEjecutiva ?? string.Empty;
        analisis.SeveridadGeneral = ParseSeveridadGeneral(dto.SeveridadGeneral, analisis.Hallazgos.Count);
    }

    private static Severidad ParseSeveridad(string? valor) => (valor ?? "").Trim().ToLowerInvariant() switch
    {
        "alto" or "alta" or "high" => Severidad.Alto,
        "medio" or "media" or "medium" => Severidad.Medio,
        _ => Severidad.Bajo
    };

    private static SeveridadGeneral ParseSeveridadGeneral(string? valor, int cantidadHallazgos) =>
        (valor ?? "").Trim().ToLowerInvariant().Replace(" ", "_") switch
        {
            "elevado" or "elevada" or "alto" or "high" => SeveridadGeneral.Elevado,
            "moderado" or "moderada" or "medio" or "medium" => SeveridadGeneral.Moderado,
            "sin_hallazgos" or "ninguno" or "none" or "low" or "bajo" => SeveridadGeneral.SinHallazgos,
            _ => cantidadHallazgos == 0 ? SeveridadGeneral.SinHallazgos : SeveridadGeneral.Moderado
        };
}
