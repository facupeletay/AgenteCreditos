using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace RiesgoWebEmpresarial.Services;

/// <summary>
/// Extrae texto plano del PDF con PdfPig y aplica heuristicas simples para
/// detectar el CUIT y la razon social del scorecard.
/// </summary>
public class PdfExtractorService
{
    public record ExtraccionPdf(string TextoCompleto, string? Cuit, string? RazonSocial);

    // 20-12345678-9 con o sin guiones
    private static readonly Regex CuitRegex =
        new(@"\b(\d{2})-?(\d{8})-?(\d)\b", RegexOptions.Compiled);

    private static readonly Regex RazonSocialRegex =
        new(@"raz[oó]n\s+social", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ExtraccionPdf Extraer(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        using (var doc = PdfDocument.Open(pdfBytes))
        {
            foreach (var page in doc.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }

        var texto = sb.ToString();
        return new ExtraccionPdf(texto, BuscarCuit(texto), BuscarRazonSocial(texto));
    }

    private static string? BuscarCuit(string texto)
    {
        var m = CuitRegex.Match(texto);
        if (!m.Success) return null;
        // Normalizado con guiones: 20-12345678-9
        return $"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}";
    }

    private static string? BuscarRazonSocial(string texto)
    {
        var lineas = texto.Split('\n', '\r');
        for (var i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i];
            var m = RazonSocialRegex.Match(linea);
            if (!m.Success) continue;

            // Caso "Razon Social: ACME S.A." en la misma linea
            var resto = linea[(m.Index + m.Length)..].TrimStart(' ', '\t', ':', '-', '–');
            if (!string.IsNullOrWhiteSpace(resto))
                return Limpiar(resto);

            // Caso "Razon Social" y el valor en la linea siguiente no vacia
            for (var j = i + 1; j < lineas.Length && j < i + 4; j++)
            {
                if (!string.IsNullOrWhiteSpace(lineas[j]))
                    return Limpiar(lineas[j]);
            }
        }
        return null;
    }

    private static string Limpiar(string valor)
    {
        valor = valor.Trim();
        // Cortar si viene pegado el proximo campo tipo "ACME S.A.  CUIT: ..."
        var corte = valor.IndexOf("CUIT", StringComparison.OrdinalIgnoreCase);
        if (corte > 3) valor = valor[..corte].Trim();
        return valor.Length > 160 ? valor[..160].Trim() : valor;
    }
}
