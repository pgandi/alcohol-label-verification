using LabelVerifier.Models;

namespace LabelVerifier.Services;

/// <summary>
/// No-key demo provider. Returns a fixed, plausible extraction (the sample
/// "OLD TOM DISTILLERY" label) so the full UI and verification flow can be
/// explored without configuring any API key. Clearly flagged as not live so the
/// UI can show a "Demo mode" banner.
/// </summary>
public class StubLabelExtractor : ILabelExtractor
{
    public string ProviderName => "Demo mode (no API key)";
    public bool IsLive => false;

    public Task<ExtractedFields> ExtractAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var result = new ExtractedFields
        {
            Readable = true,
            BrandName = "OLD TOM DISTILLERY",
            ClassType = "Kentucky Straight Bourbon Whiskey",
            AlcoholContent = "45% Alc./Vol. (90 Proof)",
            NetContents = "750 mL",
            ProducerNameAddress = "Bottled by Old Tom Distillery, Bardstown, Kentucky",
            CountryOfOrigin = null,
            GovernmentWarning = GovernmentWarning.CanonicalText,
            GovernmentWarningAppearsBold = true
        };

        return Task.FromResult(result);
    }
}
