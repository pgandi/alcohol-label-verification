using System.Diagnostics;
using System.Text.Json;
using LabelVerifier.Models;
using OpenAI.Chat;

namespace LabelVerifier.Services;

/// <summary>
/// Vision-based extractor built on the OpenAI chat API surface. The same
/// <see cref="ChatClient"/> type is produced by both the OpenAI and Azure OpenAI
/// SDKs, so this one implementation serves both providers.
/// </summary>
public class VisionLabelExtractor : ILabelExtractor
{
    private readonly ChatClient _client;
    private readonly ILogger<VisionLabelExtractor> _logger;

    public string ProviderName { get; }
    public bool IsLive => true;

    public VisionLabelExtractor(ChatClient client, string providerName, ILogger<VisionLabelExtractor> logger)
    {
        _client = client;
        ProviderName = providerName;
        _logger = logger;
    }

    private const string SystemPrompt =
        "You are a meticulous TTB (Alcohol and Tobacco Tax and Trade Bureau) label reader. " +
        "You are given a photograph of an alcohol beverage label that may be shot at an angle, " +
        "have glare, or be poorly lit. Read the text that is actually printed on the label and " +
        "return it verbatim. Preserve the ORIGINAL capitalization exactly (this matters for the " +
        "government warning). Do not correct, expand, paraphrase, or invent text. If a field is " +
        "not present on the label, return null for it. If the image is too blurry, dark, or " +
        "obstructed to read reliably, set readable=false and explain why in unreadableReason.";

    private const string UserPrompt =
        "Extract the following fields from this label and respond with ONLY a JSON object using " +
        "exactly these keys:\n" +
        "{\n" +
        "  \"readable\": boolean,\n" +
        "  \"unreadableReason\": string | null,\n" +
        "  \"brandName\": string | null,\n" +
        "  \"classType\": string | null,           // e.g. 'Kentucky Straight Bourbon Whiskey'\n" +
        "  \"alcoholContent\": string | null,       // e.g. '45% Alc./Vol. (90 Proof)'\n" +
        "  \"netContents\": string | null,          // e.g. '750 mL'\n" +
        "  \"producerNameAddress\": string | null,  // bottler/producer name and address\n" +
        "  \"countryOfOrigin\": string | null,\n" +
        "  \"governmentWarning\": string | null,    // the full warning EXACTLY as printed, keep original case\n" +
        "  \"governmentWarningAppearsBold\": boolean | null  // does the 'GOVERNMENT WARNING:' lead-in look bold/prominent?\n" +
        "}";

    public async Task<ExtractedFields> ExtractAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(UserPrompt),
                ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(imageBytes), mimeType, ChatImageDetailLevel.Auto))
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            ChatCompletion completion = await _client.CompleteChatAsync(messages, options, ct);
            string json = completion.Content.Count > 0 ? completion.Content[0].Text : "";
            _logger.LogInformation("Vision extraction completed in {Ms}ms via {Provider}", sw.ElapsedMilliseconds, ProviderName);

            var extracted = ParseJson(json);
            return extracted;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vision extraction failed via {Provider}", ProviderName);
            return new ExtractedFields
            {
                Readable = false,
                UnreadableReason = $"The {ProviderName} vision service returned an error: {ex.Message}"
            };
        }
    }

    private static ExtractedFields ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ExtractedFields { Readable = false, UnreadableReason = "Empty response from the vision model." };
        }

        // Strip accidental markdown code fences if present.
        json = json.Trim();
        if (json.StartsWith("```"))
        {
            int firstBrace = json.IndexOf('{');
            int lastBrace = json.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                json = json[firstBrace..(lastBrace + 1)];
        }

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ExtractedFields>(json, opts)
                   ?? new ExtractedFields { Readable = false, UnreadableReason = "Could not parse the model response." };
        }
        catch (JsonException)
        {
            return new ExtractedFields { Readable = false, UnreadableReason = "The model response was not valid JSON." };
        }
    }
}
