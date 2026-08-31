namespace RiesgoWebEmpresarial.Models;

public enum Severidad
{
    Alto,
    Medio,
    Bajo
}

/// <summary>
/// Un hallazgo puntual de riesgo reputacional / legal detectado por el agente.
/// </summary>
public class Hallazgo
{
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>De donde sale el dato (medio, boletin oficial, expediente, etc.).</summary>
    public string Fuente { get; set; } = string.Empty;

    public Severidad Severidad { get; set; } = Severidad.Bajo;

    /// <summary>Lectura del impacto sobre el perfil crediticio de la empresa.</summary>
    public string ImpactoCrediticio { get; set; } = string.Empty;

    /// <summary>Si el hallazgo pertenece a una empresa vinculada y no a la analizada, su nombre.</summary>
    public string? EmpresaVinculada { get; set; }
}
