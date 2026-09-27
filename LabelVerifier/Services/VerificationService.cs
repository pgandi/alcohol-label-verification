using System.Text;
using System.Text.RegularExpressions;
using LabelVerifier.Models;

namespace LabelVerifier.Services;

/// <summary>
/// Deterministic comparison of the expected application data against what the
/// vision model read off the label. Keeping the decision logic here (rather than
/// asking the LLM to judge) makes every pass/fail explainable and repeatable.
/// </summary>
public class VerificationService
{
    // Similarity at/above this counts as a match once case/punctuation are ignored.
    private const double MatchThreshold = 0.92;
    // Between review and match: close enough that a human should glance at it.
    private const double ReviewThreshold = 0.80;

    public VerificationResult Verify(ExpectedFields expected, ExtractedFields extracted, string provider, long processingMs)
    {
        var result = new VerificationResult
        {
            Provider = provider,
            ProcessingMs = processingMs,
            Readable = extracted.Readable,
            UnreadableReason = extracted.UnreadableReason
        };

        if (!extracted.Readable)
        {
            // No point comparing fields against an unreadable image.
            return result;
        }

        result.Fields.Add(CompareBrand(expected.BrandName, extracted.BrandName));
        result.Fields.Add(CompareGeneric("Class / Type", expected.ClassType, extracted.ClassType));
        result.Fields.Add(CompareAlcohol(expected.AlcoholContent, extracted.AlcoholContent));
        result.Fields.Add(CompareNetContents(expected.NetContents, extracted.NetContents));
        result.Fields.Add(CompareGeneric("Producer Name / Address", expected.ProducerNameAddress, extracted.ProducerNameAddress));

        if (expected.IsImport)
        {
            result.Fields.Add(CompareGeneric("Country of Origin", expected.CountryOfOrigin, extracted.CountryOfOrigin));
        }

        result.Warning = CheckWarning(extracted);

        return result;
    }

    // ---- Field comparisons -------------------------------------------------

    private static FieldResult CompareBrand(string expected, string? found)
        => CompareGeneric("Brand Name", expected, found);

    private static FieldResult CompareGeneric(string field, string expected, string? found)
    {
        var r = new FieldResult { Field = field, Expected = expected, Found = found ?? "" };

        if (string.IsNullOrWhiteSpace(expected))
        {
            r.Status = MatchStatus.NotChecked;
            r.Detail = "No expected value provided.";
            return r;
        }

        if (string.IsNullOrWhiteSpace(found))
        {
            r.Status = MatchStatus.Missing;
            r.Detail = "Not found on the label.";
            return r;
        }

        var ne = Normalize(expected);
        var nf = Normalize(found);

        if (ne == nf)
        {
            r.Status = MatchStatus.Match;
            r.Detail = expected.Trim() == found.Trim()
                ? "Exact match."
                : "Matches (differs only in case/spacing/punctuation).";
            return r;
        }

        double sim = Similarity(ne, nf);
        if (sim >= MatchThreshold)
        {
            r.Status = MatchStatus.Match;
            r.Detail = $"Matches ({sim:P0} similar).";
        }
        else if (sim >= ReviewThreshold)
        {
            r.Status = MatchStatus.Review;
            r.Detail = $"Close but not exact ({sim:P0} similar) - please confirm.";
        }
        else
        {
            r.Status = MatchStatus.Mismatch;
            r.Detail = "Does not match the application.";
        }
        return r;
    }

    private static FieldResult CompareAlcohol(string expected, string? found)
    {
        var r = new FieldResult { Field = "Alcohol Content", Expected = expected, Found = found ?? "" };

        if (string.IsNullOrWhiteSpace(expected))
        {
            r.Status = MatchStatus.NotChecked;
            r.Detail = "No expected value provided.";
            return r;
        }
        if (string.IsNullOrWhiteSpace(found))
        {
            r.Status = MatchStatus.Missing;
            r.Detail = "Not found on the label.";
            return r;
        }

        var ea = ParseAbv(expected);
        var fa = ParseAbv(found);

        if (ea is not null && fa is not null)
        {
            double diff = Math.Abs(ea.Value - fa.Value);
            if (diff < 0.05)
            {
                r.Status = MatchStatus.Match;
                r.Detail = $"ABV matches ({fa.Value:0.##}%).";
            }
            else if (diff <= 0.5)
            {
                r.Status = MatchStatus.Review;
                r.Detail = $"ABV differs slightly: application {ea.Value:0.##}% vs label {fa.Value:0.##}%.";
            }
            else
            {
                r.Status = MatchStatus.Mismatch;
                r.Detail = $"ABV mismatch: application {ea.Value:0.##}% vs label {fa.Value:0.##}%.";
            }
            return r;
        }

        // Fall back to a compact text comparison if a number couldn't be parsed.
        return CompareCompact("Alcohol Content", expected, found);
    }

    private static FieldResult CompareNetContents(string expected, string? found)
        => CompareCompact("Net Contents", expected, found);

    /// <summary>Compares ignoring all whitespace (so "750 mL" == "750ml").</summary>
    private static FieldResult CompareCompact(string field, string expected, string? found)
    {
        var r = new FieldResult { Field = field, Expected = expected, Found = found ?? "" };

        if (string.IsNullOrWhiteSpace(expected))
        {
            r.Status = MatchStatus.NotChecked;
            r.Detail = "No expected value provided.";
            return r;
        }
        if (string.IsNullOrWhiteSpace(found))
        {
            r.Status = MatchStatus.Missing;
            r.Detail = "Not found on the label.";
            return r;
        }

        var ne = NormalizeCompact(expected);
        var nf = NormalizeCompact(found);

        if (ne == nf)
        {
            r.Status = MatchStatus.Match;
            r.Detail = "Matches.";
            return r;
        }

        double sim = Similarity(ne, nf);
        if (sim >= MatchThreshold)
        {
            r.Status = MatchStatus.Match;
            r.Detail = $"Matches ({sim:P0} similar).";
        }
        else if (sim >= ReviewThreshold)
        {
            r.Status = MatchStatus.Review;
            r.Detail = $"Close but not exact ({sim:P0} similar) - please confirm.";
        }
        else
        {
            r.Status = MatchStatus.Mismatch;
            r.Detail = "Does not match the application.";
        }
        return r;
    }

    // ---- Government warning -------------------------------------------------

    private static FieldResult CheckWarning(ExtractedFields extracted)
    {
        var r = new FieldResult
        {
            Field = "Government Warning",
            Expected = GovernmentWarning.CanonicalText,
            Found = extracted.GovernmentWarning ?? ""
        };

        var found = extracted.GovernmentWarning;
        if (string.IsNullOrWhiteSpace(found))
        {
            r.Status = MatchStatus.Missing;
            r.Detail = "No government warning was found on the label. This statement is mandatory.";
            return r;
        }

        var issues = new List<string>();

        // 1) The lead-in must be in ALL CAPS (case-sensitive check on the raw text).
        bool hasExactLeadIn = found.Contains(GovernmentWarning.LeadIn, StringComparison.Ordinal);
        bool hasLeadInAnyCase = found.Contains("GOVERNMENT WARNING", StringComparison.OrdinalIgnoreCase);
        if (!hasExactLeadIn)
        {
            issues.Add(hasLeadInAnyCase
                ? "The \"GOVERNMENT WARNING:\" lead-in must be in all capital letters."
                : "The \"GOVERNMENT WARNING:\" lead-in is missing.");
        }

        // 2) Wording must be exact. Compare each mandated sentence individually.
        if (!ContainsNormalized(found, GovernmentWarning.Sentence1))
            issues.Add("The Surgeon General / pregnancy sentence (1) is missing or altered.");
        if (!ContainsNormalized(found, GovernmentWarning.Sentence2))
            issues.Add("The impaired-ability / health-problems sentence (2) is missing or altered.");

        if (issues.Count > 0)
        {
            r.Status = MatchStatus.Mismatch;
            r.Detail = string.Join(" ", issues);
            return r;
        }

        // 3) Everything reads correctly. Bold is advisory (hard to prove from text).
        if (extracted.GovernmentWarningAppearsBold == false)
        {
            r.Status = MatchStatus.Review;
            r.Detail = "Wording and capitalization are correct, but the lead-in may not be bold - please confirm.";
            return r;
        }

        r.Status = MatchStatus.Match;
        r.Detail = "Present, correctly worded, and capitalized.";
        return r;
    }

    // ---- Helpers -----------------------------------------------------------

    private static readonly Regex NonAlnum = new(@"[^a-z0-9 ]", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Lowercase, strip punctuation, collapse whitespace.</summary>
    private static string Normalize(string s)
    {
        s = s.ToLowerInvariant().Trim();
        s = NonAlnum.Replace(s, " ");
        s = MultiSpace.Replace(s, " ").Trim();
        return s;
    }

    /// <summary>Like <see cref="Normalize"/> but also removes all whitespace.</summary>
    private static string NormalizeCompact(string s) => Normalize(s).Replace(" ", "");

    private static bool ContainsNormalized(string haystack, string needle)
        => Normalize(haystack).Contains(Normalize(needle), StringComparison.Ordinal);

    private static double? ParseAbv(string s)
    {
        // First number that looks like a percentage/ABV (avoid grabbing the proof value).
        var m = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%");
        if (!m.Success)
            m = Regex.Match(s, @"(\d+(?:\.\d+)?)"); // fallback: first number
        if (m.Success && double.TryParse(m.Groups[1].Value, out var v))
            return v;
        return null;
    }

    /// <summary>Normalized Levenshtein similarity in [0,1].</summary>
    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        int dist = Levenshtein(a, b);
        int max = Math.Max(a.Length, b.Length);
        return max == 0 ? 1.0 : 1.0 - (double)dist / max;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
