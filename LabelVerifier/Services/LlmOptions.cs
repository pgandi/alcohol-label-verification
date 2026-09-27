namespace LabelVerifier.Services;

/// <summary>
/// Configuration for the vision provider. Bound from the "Llm" section of
/// configuration (appsettings, environment variables, or user-secrets).
/// No secrets are committed - supply keys via env vars or user-secrets.
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>One of: OpenAI, AzureOpenAI, Stub. Defaults to Stub (demo mode).</summary>
    public string Provider { get; set; } = "Stub";

    /// <summary>Vision-capable model / deployment name (e.g. gpt-4o).</summary>
    public string Model { get; set; } = "gpt-4o";

    public OpenAiSettings OpenAI { get; set; } = new();
    public AzureOpenAiSettings AzureOpenAI { get; set; } = new();

    public class OpenAiSettings
    {
        public string ApiKey { get; set; } = "";
    }

    public class AzureOpenAiSettings
    {
        public string Endpoint { get; set; } = "";
        /// <summary>The Azure deployment name for the vision model.</summary>
        public string Deployment { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }
}
