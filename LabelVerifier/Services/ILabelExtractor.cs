using LabelVerifier.Models;

namespace LabelVerifier.Services;

/// <summary>
/// Reads the printed fields off a label image. Implementations wrap a vision LLM
/// (OpenAI / Azure OpenAI) or return canned data for the no-key demo mode.
/// </summary>
public interface ILabelExtractor
{
    /// <summary>Human-readable provider name shown in the UI (e.g. "OpenAI", "Demo mode").</summary>
    string ProviderName { get; }

    /// <summary>False for the stub/demo provider so the UI can show a banner.</summary>
    bool IsLive { get; }

    Task<ExtractedFields> ExtractAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
