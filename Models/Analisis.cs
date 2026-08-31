namespace RiesgoWebEmpresarial.Models;

public enum EstadoAnalisis
{
    Pendiente,
    Procesando,
    Completo,
    Error
}

public enum SeveridadGeneral
{
    SinHallazgos,
    Moderado,
    Elevado
}

/// <summary>
/// Una corrida de analisis: un PDF (scorecard enriquecido) evaluado bajo un instructivo.
/// </summary>
public class Analisis
{
    public Guid AnalisisId { get; set; } = Guid.NewGuid();

    /// <summary>Instructivo (version) usado para esta corrida.</summary>
    public Guid InstructivoId { get; set; }

    public string NombreArchivoOriginal { get; set; } = string.Empty;

    /// <summary>CUIT detectado en el PDF (heuristica).</summary>
    public string Cuit { get; set; } = string.Empty;

    /// <summary>Razon social detectada en el PDF (heuristica).</summary>
    public string RazonSocial { get; set; } = string.Empty;

    public EstadoAnalisis EstadoAnalisis { get; set; } = EstadoAnalisis.Pendiente;

    // ---- Resultado (se completa cuando EstadoAnalisis == Completo) ----

    public string EmpresaAnalizada { get; set; } = string.Empty;

    public List<string> EmpresasVinculadas { get; set; } = new();

    public List<Hallazgo> Hallazgos { get; set; } = new();

    public string ConclusionEjecutiva { get; set; } = string.Empty;

    public SeveridadGeneral SeveridadGeneral { get; set; } = SeveridadGeneral.SinHallazgos;

    // ---- Auditoria ----

    public string UsuarioSolicitante { get; set; } = string.Empty;

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public DateTime? FechaRespuesta { get; set; }

    /// <summary>Detalle del error cuando EstadoAnalisis == Error.</summary>
    public string? MensajeError { get; set; }
}
