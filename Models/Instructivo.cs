namespace RiesgoWebEmpresarial.Models;

/// <summary>
/// Instructivo = prompt versionable que gobierna como el agente analiza el riesgo.
/// Se versiona por bifurcacion: cada version nueva referencia a su padre via
/// <see cref="InstructivoPadreId"/>, formando un arbol de derivaciones.
/// </summary>
public class Instructivo
{
    public Guid InstructivoId { get; set; } = Guid.NewGuid();

    /// <summary>Nombre logico del instructivo (se hereda del padre al bifurcar salvo que se cambie).</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Numero de version dentro de la cadena. El original es 1; cada bifurcacion incrementa.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Id del instructivo del que deriva esta version. Null solo para el original.</summary>
    public Guid? InstructivoPadreId { get; set; }

    /// <summary>Texto del prompt / instrucciones que se le pasan al modelo.</summary>
    public string ContenidoPrompt { get; set; } = string.Empty;

    /// <summary>True si es la raiz de la cadena (no deriva de nadie).</summary>
    public bool EsOriginal { get; set; }

    /// <summary>Si esta activo aparece como opcion seleccionable para correr analisis.</summary>
    public bool Activo { get; set; } = true;

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Notas libres del autor: que cambio respecto del padre, por que, etc.</summary>
    public string? Notas { get; set; }
}
