using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

/// <summary>
/// Arma el prompt (instructivo + CUIT + razon social + texto del scorecard),
/// llama a la Responses API de OpenAI con web search habilitado y pide la
/// respuesta como JSON estricto, que se deserializa con System.Text.Json.
///
/// NOTA: la Responses API del SDK oficial (paquete OpenAI) esta marcada como
/// experimental (diagnostico OPENAI001, suprimido en el .csproj). Si actualizas
/// el paquete y cambia la firma de CreateResponseAsync / ResponseTool, ajusta
/// unicamente este archivo.
/// </summary>
public class OpenAiRiesgoService
{
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenAiRiesgoService(IConfiguration config)
    {
        _apiKey = config["OpenAI:ApiKey"]
                  ?? throw new InvalidOperationException(
                      "Falta la API key. Configura 'OpenAI:ApiKey' con User Secrets " +
                      "(dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\") " +
                      "o la variable de entorno OpenAI__ApiKey.");

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("'OpenAI:ApiKey' esta vacia. Cargala antes de correr un analisis.");

        _model = config["OpenAI:Model"] is { Length: > 0 } m ? m : "gpt-4o-mini";
    }

    public async Task<RiesgoRespuestaDto> AnalizarAsync(
        string instructivoPrompt,
        string? cuit,
        string? razonSocial,
        string textoScorecard,
        CancellationToken ct = default)
    {
        var client = new OpenAIResponseClient(_model, new ApiKeyCredential(_apiKey));

        var prompt = ConstruirPrompt(instructivoPrompt, cuit, razonSocial, textoScorecard);

        var options = new ResponseCreationOptions();
        options.Tools.Add(ResponseTool.CreateWebSearchTool());

        OpenAIResponse response = await client.CreateResponseAsync(prompt, options, ct);

        var raw = response.GetOutputText() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("La Responses API devolvio una respuesta vacia.");

        var json = ExtraerJson(raw);

        try
        {
            return JsonSerializer.Deserialize<RiesgoRespuestaDto>(json, JsonOpts)
                   ?? throw new InvalidOperationException("El JSON deserializo en null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "No se pudo interpretar la respuesta del modelo como JSON. " +
                "Respuesta cruda:\n" + Recortar(raw, 2000), ex);
        }
    }

    private string ConstruirPrompt(string instructivoPrompt, string? cuit, string? razonSocial, string textoScorecard)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== INSTRUCTIVO DE ANALISIS ===");
        sb.AppendLine(instructivoPrompt.Trim());
        sb.AppendLine();
        sb.AppendLine("=== EMPRESA A ANALIZAR ===");
        sb.AppendLine($"CUIT detectado: {(string.IsNullOrWhiteSpace(cuit) ? "(no detectado)" : cuit)}");
        sb.AppendLine($"Razon social detectada: {(string.IsNullOrWhiteSpace(razonSocial) ? "(no detectada)" : razonSocial)}");
        sb.AppendLine();
        sb.AppendLine("=== TEXTO DEL SCORECARD ENRIQUECIDO (extraido del PDF) ===");
        sb.AppendLine(Recortar(textoScorecard, 60_000));
        sb.AppendLine();
        sb.AppendLine("=== TAREA ===");
        sb.AppendLine("Usa la herramienta de busqueda web para verificar y ampliar. Luego responde.");
        sb.AppendLine("IMPORTANTE: responde EXCLUSIVAMENTE con un objeto JSON valido, sin texto antes ni despues,");
        sb.AppendLine("sin bloque de codigo markdown, con exactamente esta forma:");
        sb.AppendLine(
            """
            {
              "empresa_analizada": "string",
              "empresas_vinculadas_relevantes": ["string"],
              "hallazgos": [
                {
                  "descripcion": "string",
                  "fuente": "string (medio + fecha, u organismo/expediente)",
                  "severidad": "alto | medio | bajo",
                  "impacto_crediticio": "string",
                  "empresa_vinculada": "string o null si el hallazgo es de la empresa analizada"
                }
              ],
              "conclusion_ejecutiva": "string, 3-6 oraciones para un comite de credito",
              "severidad_general": "sin_hallazgos | moderado | elevado"
            }
            """);
        sb.AppendLine("Si no encontras hallazgos, devolve \"hallazgos\": [] y \"severidad_general\": \"sin_hallazgos\".");
        return sb.ToString();
    }

    /// <summary>
    /// Tolera que el modelo devuelva el JSON envuelto en ```json ... ``` o con texto alrededor:
    /// toma desde la primera '{' hasta la ultima '}'.
    /// </summary>
    private static string ExtraerJson(string raw)
    {
        var texto = raw.Trim();

        var fenceStart = texto.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFence = texto.IndexOf('\n', fenceStart);
            var fenceEnd = texto.LastIndexOf("```", StringComparison.Ordinal);
            if (afterFence >= 0 && fenceEnd > afterFence)
                texto = texto[(afterFence + 1)..fenceEnd].Trim();
        }

        var open = texto.IndexOf('{');
        var close = texto.LastIndexOf('}');
        if (open >= 0 && close > open)
            return texto[open..(close + 1)];

        return texto;
    }

    private static string Recortar(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "\n[...recortado...]";
}
