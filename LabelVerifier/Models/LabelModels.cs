using System.Text.Json.Serialization;

namespace LabelVerifier.Models;

/// <summary>
/// The values an agent enters from the TTB application form. These are what the
/// label artwork is checked against. The Government Warning is not entered here -
/// it is always compared against the canonical TTB statement.
/// </summary>
public class ExpectedFields
{
    public string BrandName { get; set; } = "";
    public string ClassType { get; set; } = "";
    public string AlcoholContent { get; set; } = "";
    public string NetContents { get; set; } = "";
    public string ProducerNameAddress { get; set; } = "";

    /// <summary>Only required for imported products.</summary>
    public string CountryOfOrigin { get; set; } = "";

    /// <summary>When true, the country-of-origin field is verified.</summary>
    public bool IsImport { get; set; }

    public ExpectedFields Clone() => (ExpectedFields)MemberwiseClone();
}

/// <summary>
/// Verbatim text the vision model reads off the label. Strings are captured
/// exactly as they appear (including case) so downstream rules can judge things
/// like the all-caps "GOVERNMENT WARNING:" requirement.
/// </summary>
public class ExtractedFields
{
    [JsonPropertyName("readable")]
    public bool Readable { get; set; } = true;

    /// <summary>Model explanation when the image cannot be read reliably.</summary>
    [JsonPropertyName("unreadableReason")]
    public string? UnreadableReason { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("classType")]
    public string? ClassType { get; set; }

    [JsonPropertyName("alcoholContent")]
    public string? AlcoholContent { get; set; }

    [JsonPropertyName("netContents")]
    public string? NetContents { get; set; }

    [JsonPropertyName("producerNameAddress")]
    public string? ProducerNameAddress { get; set; }

    [JsonPropertyName("countryOfOrigin")]
    public string? CountryOfOrigin { get; set; }

    /// <summary>The government warning exactly as printed on the label.</summary>
    [JsonPropertyName("governmentWarning")]
    public string? GovernmentWarning { get; set; }

    /// <summary>
    /// Model's best judgement on whether the "GOVERNMENT WARNING:" lead-in appears
    /// bold/prominent. Advisory only - text can't fully prove typographic weight.
    /// </summary>
    [JsonPropertyName("governmentWarningAppearsBold")]
    public bool? GovernmentWarningAppearsBold { get; set; }
}

public enum MatchStatus
{
    /// <summary>Values are equivalent (may differ only in case/punctuation).</summary>
    Match,

    /// <summary>Close but not exact - a human should glance at it.</summary>
    Review,

    /// <summary>Values clearly differ.</summary>
    Mismatch,

    /// <summary>Field expected but not found on the label.</summary>
    Missing,

    /// <summary>Field not part of this check.</summary>
    NotChecked
}

public class FieldResult
{
    public string Field { get; set; } = "";
    public string Expected { get; set; } = "";
    public string Found { get; set; } = "";
    public MatchStatus Status { get; set; }
    public string Detail { get; set; } = "";

    public bool IsPass => Status is MatchStatus.Match or MatchStatus.NotChecked;
}

public class VerificationResult
{
    /// <summary>False when the image could not be read - no field checks are meaningful.</summary>
    public bool Readable { get; set; } = true;
    public string? UnreadableReason { get; set; }

    public List<FieldResult> Fields { get; set; } = new();

    /// <summary>The government warning check, kept separate because it is mandatory and strict.</summary>
    public FieldResult Warning { get; set; } = new() { Field = "Government Warning" };

    public long ProcessingMs { get; set; }

    /// <summary>Which provider produced the extraction (OpenAI, Azure OpenAI, or Demo).</summary>
    public string Provider { get; set; } = "";

    /// <summary>True only when every checked field passes and the warning is valid.</summary>
    public bool OverallPass =>
        Readable
        && Fields.All(f => f.IsPass)
        && Warning.IsPass;

    /// <summary>Convenience for the UI: number of fields that failed or need review.</summary>
    public int IssueCount =>
        Fields.Count(f => !f.IsPass) + (Warning.IsPass ? 0 : 1);
}
