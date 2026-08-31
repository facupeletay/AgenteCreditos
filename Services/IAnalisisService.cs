using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

public interface IAnalisisService
{
    /// <summary>
    /// Registra la solicitud (estado Pendiente) y dispara el analisis en background.
    /// Devuelve de inmediato el <see cref="Analisis"/> creado para que la UI haga polling.
    /// </summary>
    Analisis IniciarAnalisis(byte[] pdfBytes, string nombreArchivo, Guid instructivoId, string usuario);

    Analisis? ObtenerPorId(Guid analisisId);

    IReadOnlyList<Analisis> ObtenerTodos();
}
