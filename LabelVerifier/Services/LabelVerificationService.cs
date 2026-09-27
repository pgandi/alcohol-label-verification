using System.Diagnostics;
using LabelVerifier.Models;

namespace LabelVerifier.Services;

/// <summary>
/// Orchestrates a single verification: read the label with the vision provider,
/// then run the deterministic checks. Used by both the single and batch screens.
/// </summary>
public class LabelVerificationService
{
    private readonly ILabelExtractor _extractor;
    private readonly VerificationService _verifier;

    public LabelVerificationService(ILabelExtractor extractor, VerificationService verifier)
    {
        _extractor = extractor;
        _verifier = verifier;
    }

    public string ProviderName => _extractor.ProviderName;
    public bool IsLive => _extractor.IsLive;

    public async Task<VerificationResult> VerifyAsync(
        byte[] imageBytes, string mimeType, ExpectedFields expected, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var extracted = await _extractor.ExtractAsync(imageBytes, mimeType, ct);
        sw.Stop();
        return _verifier.Verify(expected, extracted, _extractor.ProviderName, sw.ElapsedMilliseconds);
    }
}
