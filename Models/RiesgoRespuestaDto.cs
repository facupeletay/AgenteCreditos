using System.Text.Json.Serialization;

namespace RiesgoWebEmpresarial.Models;

/// <summary>
/// Forma exacta del JSON que le pedimos al modelo. Se deserializa con System.Text.Json
/// y luego <see cref="Services.AnalisisService"/> lo mapea al modelo de dominio <see cref="Analisis"/>.
/// </summary>
public class RiesgoRespuestaDto
{
    [JsonPropertyName("empresa_analizada")]
    public string EmpresaAnalizada { get; set; } = string.Empty;

    [JsonPropertyName("empresas_vinculadas_relevantes")]
    public List<string> EmpresasVinculadasRelevantes { get; set; } = new();

    [JsonPropertyName("hallazgos")]
    public List<HallazgoDto> Hallazgos { get; set; } = new();

    [JsonPropertyName("conclusion_ejecutiva")]
    public string ConclusionEjecutiva { get; set; } = string.Empty;

    /// <summary>Uno de: "sin_hallazgos" | "moderado" | "elevado".</summary>
    [JsonPropertyName("severidad_general")]
    public string SeveridadGeneral { get; set; } = "sin_hallazgos";
}

public class HallazgoDto
{
    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("fuente")]
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Uno de: "alto" | "medio" | "bajo".</summary>
    [JsonPropertyName("severidad")]
    public string Severidad { get; set; } = "bajo";

    [JsonPropertyName("impacto_crediticio")]
    public string ImpactoCrediticio { get; set; } = string.Empty;

    [JsonPropertyName("empresa_vinculada")]
    public string? EmpresaVinculada { get; set; }
}
