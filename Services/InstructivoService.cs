using System.Collections.Concurrent;
using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

/// <summary>
/// Storage temporal en memoria de instructivos. Thread-safe via ConcurrentDictionary.
/// Para produccion: reemplazar por persistencia real (EF Core / SQL Server). Ver README.
/// </summary>
public class InstructivoService : IInstructivoService
{
    private readonly ConcurrentDictionary<Guid, Instructivo> _store = new();

    public InstructivoService()
    {
        Seed();
    }

    public IReadOnlyList<Instructivo> ObtenerTodos() =>
        _store.Values.OrderBy(i => i.Nombre).ThenBy(i => i.Version).ToList();

    public IReadOnlyList<Instructivo> ObtenerActivos() =>
        _store.Values.Where(i => i.Activo).OrderBy(i => i.Nombre).ThenBy(i => i.Version).ToList();

    public Instructivo? ObtenerPorId(Guid id) =>
        _store.TryGetValue(id, out var i) ? i : null;

    public Instructivo CrearOriginal(string nombre, string contenidoPrompt, string creadoPor, string? notas)
    {
        var instructivo = new Instructivo
        {
            Nombre = string.IsNullOrWhiteSpace(nombre) ? "Instructivo sin nombre" : nombre.Trim(),
            Version = 1,
            InstructivoPadreId = null,
            ContenidoPrompt = contenidoPrompt ?? string.Empty,
            EsOriginal = true,
            Activo = true,
            CreadoPor = string.IsNullOrWhiteSpace(creadoPor) ? "desconocido" : creadoPor.Trim(),
            Notas = notas
        };
        _store[instructivo.InstructivoId] = instructivo;
        return instructivo;
    }

    public Instructivo Bifurcar(Guid instructivoPadreId, string contenidoPrompt, string creadoPor, string? notas, string? nuevoNombre = null)
    {
        if (!_store.TryGetValue(instructivoPadreId, out var padre))
            throw new InvalidOperationException($"No existe el instructivo padre {instructivoPadreId}.");

        var hijo = new Instructivo
        {
            Nombre = string.IsNullOrWhiteSpace(nuevoNombre) ? padre.Nombre : nuevoNombre.Trim(),
            Version = SiguienteVersion(padre),
            InstructivoPadreId = padre.InstructivoId,
            ContenidoPrompt = contenidoPrompt ?? string.Empty,
            EsOriginal = false,
            Activo = true,
            CreadoPor = string.IsNullOrWhiteSpace(creadoPor) ? "desconocido" : creadoPor.Trim(),
            Notas = notas
        };
        _store[hijo.InstructivoId] = hijo;
        return hijo;
    }

    public Instructivo Actualizar(Guid id, string nombre, string contenidoPrompt, string? notas)
    {
        if (!_store.TryGetValue(id, out var instructivo))
            throw new InvalidOperationException($"No existe el instructivo {id}.");

        instructivo.Nombre = string.IsNullOrWhiteSpace(nombre) ? instructivo.Nombre : nombre.Trim();
        instructivo.ContenidoPrompt = contenidoPrompt ?? string.Empty;
        instructivo.Notas = notas;
        return instructivo;
    }

    public void SetActivo(Guid id, bool activo)
    {
        if (_store.TryGetValue(id, out var instructivo))
            instructivo.Activo = activo;
    }

    /// <summary>
    /// Version siguiente: maximo entre las versiones que comparten la misma raiz de la cadena + 1.
    /// </summary>
    private int SiguienteVersion(Instructivo padre)
    {
        var raizId = RaizDe(padre).InstructivoId;
        var maxVersion = _store.Values
            .Where(i => i.InstructivoId == raizId || PerteneceALaCadena(i, raizId))
            .Max(i => i.Version);
        return maxVersion + 1;
    }

    private Instructivo RaizDe(Instructivo instructivo)
    {
        var actual = instructivo;
        var visitados = new HashSet<Guid>();
        while (actual.InstructivoPadreId is Guid padreId
               && visitados.Add(actual.InstructivoId)
               && _store.TryGetValue(padreId, out var padre))
        {
            actual = padre;
        }
        return actual;
    }

    private bool PerteneceALaCadena(Instructivo instructivo, Guid raizId)
    {
        var actual = instructivo;
        var visitados = new HashSet<Guid>();
        while (actual.InstructivoPadreId is Guid padreId && visitados.Add(actual.InstructivoId))
        {
            if (padreId == raizId) return true;
            if (!_store.TryGetValue(padreId, out var padre)) break;
            actual = padre;
        }
        return false;
    }

    private void Seed()
    {
        CrearOriginal(
            nombre: "Riesgo reputacional y legal - base",
            creadoPor: "sistema",
            notas: "Instructivo original provisto con el proyecto. Bifurcalo para ajustar el criterio.",
            contenidoPrompt:
                """
                Sos un analista de riesgo reputacional y legal de empresas argentinas.
                A partir del scorecard enriquecido adjunto y de busquedas en la web,
                identifica hechos que puedan afectar el perfil crediticio o reputacional
                de la empresa analizada y de sus empresas vinculadas relevantes
                (controlantes, controladas, socios, directores con exposicion publica).

                Priorizar:
                - Causas judiciales, concursos, quiebras, embargos, inhibiciones.
                - Sanciones de organismos (AFIP, CNV, BCRA, UIF, defensa del consumidor, ambientales).
                - Deudas fiscales o previsionales relevantes.
                - Investigaciones periodisticas serias sobre fraude, lavado, corrupcion, laborales graves.
                - Cambios societarios o de control sospechosos.

                Reglas:
                - Citar SIEMPRE la fuente (medio + fecha, o el organismo/expediente).
                - Distinguir hecho comprobado de version periodistica o denuncia sin resolucion.
                - No inventar. Si no hay hallazgos, decirlo explicitamente.
                - Ser conciso y accionable para un comite de credito.
                """);
    }
}
