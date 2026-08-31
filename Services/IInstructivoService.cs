using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

public interface IInstructivoService
{
    IReadOnlyList<Instructivo> ObtenerTodos();

    IReadOnlyList<Instructivo> ObtenerActivos();

    Instructivo? ObtenerPorId(Guid id);

    /// <summary>Crea un instructivo raiz (sin padre).</summary>
    Instructivo CrearOriginal(string nombre, string contenidoPrompt, string creadoPor, string? notas);

    /// <summary>
    /// Crea una version nueva derivada de <paramref name="instructivoPadreId"/>.
    /// Hereda el nombre del padre (salvo <paramref name="nuevoNombre"/>) e incrementa la version.
    /// </summary>
    Instructivo Bifurcar(Guid instructivoPadreId, string contenidoPrompt, string creadoPor, string? notas, string? nuevoNombre = null);

    /// <summary>Edita in-place un instructivo existente (no crea version nueva).</summary>
    Instructivo Actualizar(Guid id, string nombre, string contenidoPrompt, string? notas);

    void SetActivo(Guid id, bool activo);
}
