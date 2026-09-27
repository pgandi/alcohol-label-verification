using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace LabelVerifier.Services;

/// <summary>
/// Builds the configured <see cref="ILabelExtractor"/>. Falls back to the demo
/// stub (rather than crashing) when a provider is selected but its key/endpoint
/// is missing, so the app always runs.
/// </summary>
public static class LabelExtractorFactory
{
    public static ILabelExtractor Create(LlmOptions options, ILoggerFactory loggerFactory)
    {
        var log = loggerFactory.CreateLogger("LabelExtractorFactory");
        var provider = (options.Provider ?? "Stub").Trim();

        switch (provider.ToLowerInvariant())
        {
            case "openai":
            {
                var key = options.OpenAI.ApiKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    log.LogWarning("Llm:Provider is OpenAI but no API key is configured. Falling back to Demo mode.");
                    return new StubLabelExtractor();
                }
                var client = new ChatClient(options.Model, new ApiKeyCredential(key));
                return new VisionLabelExtractor(client, "OpenAI", loggerFactory.CreateLogger<VisionLabelExtractor>());
            }

            case "azureopenai":
            case "azure":
            {
                var s = options.AzureOpenAI;
                if (string.IsNullOrWhiteSpace(s.Endpoint) || string.IsNullOrWhiteSpace(s.ApiKey) || string.IsNullOrWhiteSpace(s.Deployment))
                {
                    log.LogWarning("Llm:Provider is AzureOpenAI but Endpoint/Deployment/ApiKey is incomplete. Falling back to Demo mode.");
                    return new StubLabelExtractor();
                }
                var azure = new AzureOpenAIClient(new Uri(s.Endpoint), new ApiKeyCredential(s.ApiKey));
                var client = azure.GetChatClient(s.Deployment);
                return new VisionLabelExtractor(client, "Azure OpenAI", loggerFactory.CreateLogger<VisionLabelExtractor>());
            }

            case "stub":
            case "demo":
                return new StubLabelExtractor();

            default:
                log.LogWarning("Unknown Llm:Provider '{Provider}'. Falling back to Demo mode.", provider);
                return new StubLabelExtractor();
        }
    }
}
