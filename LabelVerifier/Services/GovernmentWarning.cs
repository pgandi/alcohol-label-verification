namespace LabelVerifier.Services;

/// <summary>
/// The mandatory Government Health Warning statement required by the TTB on all
/// alcohol beverage labels (27 CFR 16.21). The lead-in "GOVERNMENT WARNING:" must
/// appear in capital letters and bold; the wording must be exact.
/// </summary>
public static class GovernmentWarning
{
    public const string LeadIn = "GOVERNMENT WARNING:";

    public const string Sentence1 =
        "(1) According to the Surgeon General, women should not drink alcoholic beverages " +
        "during pregnancy because of the risk of birth defects.";

    public const string Sentence2 =
        "(2) Consumption of alcoholic beverages impairs your ability to drive a car or " +
        "operate machinery, and may cause health problems.";

    /// <summary>The full canonical statement, exactly as it must read.</summary>
    public const string CanonicalText =
        LeadIn + " " + Sentence1 + " " + Sentence2;
}
