using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;

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
        Sos un transcriptor, no un editor. Se te da el PDF del enunciado de un problema
        de programación competitiva. Tu única tarea es transcribir el texto EXACTAMENTE
        como aparece, convertido a HTML con soporte MathJax, separado en las secciones
        que ya existen en el documento.

        Reglas estrictas, no negociables:
        - No cambies ni una palabra del texto original. No parafrasees, no resumas, no
          corrijas gramática ni ortografía, no agregues ni elimines palabras.
        - No cambies el significado ni el estilo de redacción del problema.
        - Preservá el idioma original tal cual está escrito.
        - Convertí notación matemática a delimitadores MathJax: \( ... \) para inline,
          \[ ... \] para bloques. Es la ÚNICA transformación de contenido permitida --
          es notación, no texto, y solo aplica a lo que ya era notación matemática en
          el PDF.
        - Usá únicamente etiquetas HTML básicas (<p>, <ul>, <li>, <b>, <i>) para
          reproducir la estructura visual del documento (párrafos, listas, énfasis). No
          agregues contenido, encabezados ni explicaciones que no estén en el PDF.
        - Donde el documento tenga una figura o imagen, insertá el comentario HTML
          "<!-- figure -->" en el lugar exacto donde aparece en el orden de lectura --
          no describas ni inventes el contenido de la imagen.
        - Separá el contenido en las secciones estándar de un enunciado: todo el texto
          antes de la sección de entrada va en "description"; la sección
          "Entrada"/"Input" va en "input" (sin repetir el título de la sección); la
          sección "Salida"/"Output" va en "output"; cualquier sección de
          "Nota"/"Note"/"Aclaración" va en "hint". Los casos de ejemplo
          (Sample Input/Output/Ejemplo) NO van en ninguna de estas cuatro secciones.
        - Si una de las cuatro secciones no existe en el documento, devolvela como
          cadena vacía -- nunca inventes contenido para completarla.
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

    // Returns null on any failure (missing key, API error, malformed response) so the
    // caller falls back to the plain pdftotext path -- an import must never fail, or
    // silently corrupt content, because the transcription step had a bad day.
    public static async Task<BocaStatementSections?> TryReadAsync(string pdfPath)
    {
        if (!IsConfigured)
        {
            return null;
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
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Transcribí este enunciado siguiendo exactamente las reglas del system prompt." },
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
            response.EnsureSuccessStatusCode();

            using JsonDocument completion = JsonDocument.Parse(responseBody);
            string? json = completion.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (json is null)
            {
                return null;
            }

            using JsonDocument parsed = JsonDocument.Parse(json);
            JsonElement root = parsed.RootElement;
            return new BocaStatementSections(
                DescriptionHtml: root.GetProperty("description").GetString() ?? "",
                InputHtml: root.GetProperty("input").GetString() ?? "",
                OutputHtml: root.GetProperty("output").GetString() ?? "",
                HintHtml: root.GetProperty("hint").GetString() ?? ""
            );
        }
        catch (Exception error)
        {
            Console.WriteLine($"  ! LLM statement transcription failed, falling back to pdftotext: {error.Message}");
            return null;
        }
    }
}
