using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OnlineJudgeAdmin.BocaImporter;

// Splits a BOCA PDF statement into Description/Input/Output/Hint HTML instead of the
// single flat-paragraph blob BocaPackageReader.ReadDescription produces from pdftotext.
// This is a TRANSCRIPTION step, never an edit: the model is instructed to reproduce the
// PDF's text verbatim (only notation is converted, to MathJax) and never to paraphrase,
// summarize, correct or drop wording. It is still always flagged for human review --
// this replaces a lossy text extractor, not the reviewer.
public sealed record BocaStatementSections(
    string DescriptionHtml,
    string InputHtml,
    string OutputHtml,
    string HintHtml
);

public static class BocaStatementLlmReader
{
    private const string DefaultModel = "google/gemini-3.1-flash-lite";
    private static readonly HttpClient HttpClient = new();

    // Same rule textually described to the model as the class-level rule above: it must
    // not appear as a rule the model could "interpret away" under some other framing.
    private const string SystemPrompt = """
        Sos un transcriptor, no un editor. Transcribí fielmente el PDF y distribuí el contenido
        únicamente en los campos que admite el formulario del problema: Description, Input,
        Output y Hint. No inventes ni completes contenido que no aparezca en el documento.

        Reglas:
        - No parafrasees, resumas, corrijas ni omitas texto. Conservá el idioma, datos, cifras,
          condiciones y orden lógico originales.
        - Description: historia, contexto, objetivo y restricciones generales del problema que
          no sean parte del formato de entrada.
        - Input: formato de entrada y restricciones sobre los datos ingresados (Entrada, Input,
          Formato de entrada, Input Format).
        - Output: formato de salida (Salida, Output, Formato de salida, Output Format).
        - Hint: notas, observaciones, aclaraciones o explicación explícita (Nota, Note,
          Observación, Explanation), solo si existen. No confundas la explicación de una muestra
          con Hint.
        - Las muestras/ejemplos (Sample Input/Output, Example, Ejemplo) no van en esos campos:
          el importador BOCA carga los casos de muestra desde los archivos input/output del ZIP.
        - No repitas los títulos de sección dentro de cada campo. No muevas contenido si cambia
          su significado.
        - Convertí notación matemática a delimitadores MathJax: \( ... \) inline y
          \[ ... \] en bloque.
        - Usá solo HTML básico (<p>, <ul>, <li>, <b>, <i>) para reflejar párrafos, listas y énfasis.
        - Donde haya una figura, insertá "<!-- figure -->" en su posición; no describas ni inventes
          su contenido.
        - Si una sección no existe, devolvé cadena vacía. Respondé solo el JSON pedido.
        """;

    private static readonly Dictionary<string, JsonElement> ResponseSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            description = new { type = "string" },
            input = new { type = "string" },
            output = new { type = "string" },
            hint = new { type = "string" },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "description", "input", "output", "hint" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    public static bool IsConfigured =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

    // Returns no sections on any failure (missing key, API error, malformed response) so the
    // caller falls back to the plain pdftotext path -- an import must never fail, or
    // silently corrupt content, because the transcription step had a bad day. The failure
    // text is logged and shown to the importer, who otherwise cannot tell why it was skipped.
    public static async Task<(BocaStatementSections? Sections, string? Failure)> TryReadAsync(string pdfPath)
    {
        if (!IsConfigured)
        {
            return Failed("OPENROUTER_API_KEY is not configured");
        }

        try
        {
            string base64Pdf = Convert.ToBase64String(await File.ReadAllBytesAsync(pdfPath));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = Environment.GetEnvironmentVariable("OPENROUTER_BOCA_MODEL") ?? DefaultModel,
                max_tokens = 16000,
                provider = new { require_parameters = true },
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Transcribí este enunciado siguiendo las reglas y los campos del formulario." },
                            new
                            {
                                type = "file",
                                file = new
                                {
                                    filename = Path.GetFileName(pdfPath),
                                    file_data = $"data:application/pdf;base64,{base64Pdf}"
                                }
                            }
                        }
                    }
                },
                plugins = new object[]
                {
                    new { id = "file-parser", pdf = new { engine = "cloudflare-ai" } }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new { name = "boca_statement_sections", strict = true, schema = ResponseSchema }
                }
            }), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return Failed($"OpenRouter answered {(int)response.StatusCode}: {responseBody[..Math.Min(responseBody.Length, 300)]}");
            }

            using JsonDocument completion = JsonDocument.Parse(responseBody);
            string? json = completion.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (json is null)
            {
                return Failed("OpenRouter returned an empty answer");
            }

            using JsonDocument parsed = JsonDocument.Parse(json);
            JsonElement root = parsed.RootElement;
            return (new BocaStatementSections(
                DescriptionHtml: root.GetProperty("description").GetString() ?? "",
                InputHtml: root.GetProperty("input").GetString() ?? "",
                OutputHtml: root.GetProperty("output").GetString() ?? "",
                HintHtml: root.GetProperty("hint").GetString() ?? ""
            ), null);
        }
        catch (Exception error)
        {
            return Failed(error.Message);
        }
    }

    private static (BocaStatementSections? Sections, string? Failure) Failed(string reason)
    {
        Console.WriteLine($"  ! LLM statement transcription failed, falling back to pdftotext: {reason}");
        return (null, reason);
    }
}
