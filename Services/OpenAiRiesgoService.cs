using System.Text;
using System.Text.Json;
using System.ClientModel;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;
using RiesgoWebEmpresarial.Models;

namespace RiesgoWebEmpresarial.Services;

#pragma warning disable OPENAI001 // La Responses API del SDK oficial esta marcada como experimental.

/// <summary>
/// Arma el prompt (instructivo + CUIT + razon social + texto del scorecard),
/// llama a la Responses API de OpenAI con web search habilitado y pide la
/// respuesta como JSON estricto, que se deserializa con System.Text.Json.
///
/// Toda la dependencia del paquete OpenAI (experimental: diagnostico OPENAI001,
/// suprimido en el .csproj) esta aislada en este archivo. Verificado contra
/// OpenAI 2.13.0: cliente = ResponsesClient, opciones = CreateResponseOptions,
/// resultado = ResponseResult.GetOutputText().
/// </summary>
public class OpenAiRiesgoService
{
    private readonly IConfiguration _config;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // La API key NO se valida en el constructor a proposito: el servicio es Singleton
    // y la app tiene que poder levantar (y navegar Historial / Instructivos) sin key.
    // Se resuelve y valida recien al correr un analisis.
    public OpenAiRiesgoService(IConfiguration config) => _config = config;

    private string ResolverApiKey() =>
        _config["OpenAI:ApiKey"] is { Length: > 0 } k
            ? k
            : throw new InvalidOperationException(
                "Falta 'OpenAI:ApiKey'. Configurala con User Secrets " +
                "(dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\") o la variable de entorno OpenAI__ApiKey.");

    private string ResolverModelo() =>
        _config["OpenAI:Model"] is { Length: > 0 } m ? m : "gpt-4o-mini";

    public async Task<RiesgoRespuestaDto> AnalizarAsync(
        string instructivoPrompt,
        string? cuit,
        string? razonSocial,
        string textoScorecard,
        CancellationToken ct = default)
    {
        var apiKey = ResolverApiKey();
        ResponsesClient client = new OpenAIClient(new ApiKeyCredential(apiKey)).GetResponsesClient();

        var prompt = ConstruirPrompt(instructivoPrompt, cuit, razonSocial, textoScorecard);

        var options = new CreateResponseOptions
        {
            Model = ResolverModelo(),
            Instructions =
                "Sos un analista de riesgo reputacional y legal. Respondes SIEMPRE con un unico objeto JSON " +
                "valido, sin texto ni markdown alrededor. Usa la busqueda web para verificar los hechos."
        };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));
        options.Tools.Add(ResponseTool.CreateWebSearchTool());

        ResponseResult result = await client.CreateResponseAsync(options, ct);

        var raw = result.GetOutputText() ?? string.Empty;
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
                "No se pudo interpretar la respuesta del modelo como JSON. Respuesta cruda:\n" + Recortar(raw, 2000), ex);
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
