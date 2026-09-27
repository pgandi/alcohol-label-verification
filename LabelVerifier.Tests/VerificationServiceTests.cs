using LabelVerifier.Models;
using LabelVerifier.Services;
using Xunit;

namespace LabelVerifier.Tests;

public class VerificationServiceTests
{
    private readonly VerificationService _svc = new();

    private static ExtractedFields GoodLabel(Action<ExtractedFields>? tweak = null)
    {
        var e = new ExtractedFields
        {
            Readable = true,
            BrandName = "OLD TOM DISTILLERY",
            ClassType = "Kentucky Straight Bourbon Whiskey",
            AlcoholContent = "45% Alc./Vol. (90 Proof)",
            NetContents = "750 mL",
            ProducerNameAddress = "Bottled by Old Tom Distillery, Bardstown, Kentucky",
            GovernmentWarning = GovernmentWarning.CanonicalText,
            GovernmentWarningAppearsBold = true
        };
        tweak?.Invoke(e);
        return e;
    }

    private static ExpectedFields GoodApplication() => new()
    {
        BrandName = "OLD TOM DISTILLERY",
        ClassType = "Kentucky Straight Bourbon Whiskey",
        AlcoholContent = "45% Alc./Vol. (90 Proof)",
        NetContents = "750 mL",
        ProducerNameAddress = "Bottled by Old Tom Distillery, Bardstown, Kentucky"
    };

    private FieldResult FieldNamed(VerificationResult r, string name) =>
        r.Fields.Single(f => f.Field == name);

    [Fact]
    public void FullyCompliant_Label_Passes()
    {
        var r = _svc.Verify(GoodApplication(), GoodLabel(), "test", 100);
        Assert.True(r.OverallPass);
        Assert.Equal(0, r.IssueCount);
    }

    [Fact]
    public void CaseAndPunctuation_Differences_StillMatch()
    {
        // Dave's example: "STONE'S THROW" on the label vs "Stone's Throw" in the app.
        var app = GoodApplication();
        app.BrandName = "Stone's Throw";
        var label = GoodLabel(e => e.BrandName = "STONE'S THROW");

        var brand = FieldNamed(_svc.Verify(app, label, "test", 1), "Brand Name");
        Assert.Equal(MatchStatus.Match, brand.Status);
    }

    [Fact]
    public void NetContents_UnitSpacing_Differences_Match()
    {
        var app = GoodApplication();
        app.NetContents = "750ml";
        var label = GoodLabel(e => e.NetContents = "750 mL");

        var net = FieldNamed(_svc.Verify(app, label, "test", 1), "Net Contents");
        Assert.Equal(MatchStatus.Match, net.Status);
    }

    [Fact]
    public void Abv_NumericMismatch_Fails()
    {
        var app = GoodApplication();
        app.AlcoholContent = "43% Alc./Vol.";
        var label = GoodLabel(e => e.AlcoholContent = "40% Alc./Vol. (80 Proof)");

        var abv = FieldNamed(_svc.Verify(app, label, "test", 1), "Alcohol Content");
        Assert.Equal(MatchStatus.Mismatch, abv.Status);
    }

    [Fact]
    public void Abv_SameValue_DifferentProofText_Matches()
    {
        var app = GoodApplication();
        app.AlcoholContent = "45%";
        var label = GoodLabel(e => e.AlcoholContent = "45% Alc./Vol. (90 Proof)");

        var abv = FieldNamed(_svc.Verify(app, label, "test", 1), "Alcohol Content");
        Assert.Equal(MatchStatus.Match, abv.Status);
    }

    [Fact]
    public void Warning_TitleCaseLeadIn_Fails()
    {
        // Jenny's rejection: "Government Warning" in title case instead of ALL CAPS.
        var label = GoodLabel(e => e.GovernmentWarning =
            GovernmentWarning.CanonicalText.Replace("GOVERNMENT WARNING:", "Government Warning:"));

        var r = _svc.Verify(GoodApplication(), label, "test", 1);
        Assert.Equal(MatchStatus.Mismatch, r.Warning.Status);
        Assert.False(r.OverallPass);
    }

    [Fact]
    public void Warning_Missing_Fails()
    {
        var label = GoodLabel(e => e.GovernmentWarning = null);
        var r = _svc.Verify(GoodApplication(), label, "test", 1);
        Assert.Equal(MatchStatus.Missing, r.Warning.Status);
    }

    [Fact]
    public void Warning_AlteredWording_Fails()
    {
        var label = GoodLabel(e => e.GovernmentWarning =
            "GOVERNMENT WARNING: (1) Drinking is bad for you. (2) Do not drive.");
        var r = _svc.Verify(GoodApplication(), label, "test", 1);
        Assert.Equal(MatchStatus.Mismatch, r.Warning.Status);
    }

    [Fact]
    public void Warning_CorrectButNotBold_IsReview()
    {
        var label = GoodLabel(e => e.GovernmentWarningAppearsBold = false);
        var r = _svc.Verify(GoodApplication(), label, "test", 1);
        Assert.Equal(MatchStatus.Review, r.Warning.Status);
    }

    [Fact]
    public void UnreadableImage_ProducesNoFieldChecks()
    {
        var label = new ExtractedFields { Readable = false, UnreadableReason = "Too blurry" };
        var r = _svc.Verify(GoodApplication(), label, "test", 1);
        Assert.False(r.Readable);
        Assert.Empty(r.Fields);
        Assert.False(r.OverallPass);
    }

    [Fact]
    public void MissingBrandOnLabel_Fails()
    {
        var label = GoodLabel(e => e.BrandName = null);
        var brand = FieldNamed(_svc.Verify(GoodApplication(), label, "test", 1), "Brand Name");
        Assert.Equal(MatchStatus.Missing, brand.Status);
    }

    [Fact]
    public void ImportCountry_Checked_WhenIsImport()
    {
        var app = GoodApplication();
        app.IsImport = true;
        app.CountryOfOrigin = "Scotland";
        var label = GoodLabel(e => e.CountryOfOrigin = "Scotland");

        var r = _svc.Verify(app, label, "test", 1);
        var country = FieldNamed(r, "Country of Origin");
        Assert.Equal(MatchStatus.Match, country.Status);
    }
}

public class SimpleCsvTests
{
    [Fact]
    public void Parse_HandlesQuotedFieldsWithCommas()
    {
        var rows = SimpleCsv.Parse("a,b\n\"hello, world\",2\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("hello, world", rows[1][0]);
        Assert.Equal("2", rows[1][1]);
    }

    [Fact]
    public void Escape_QuotesWhenNeeded()
    {
        Assert.Equal("plain", SimpleCsv.Escape("plain"));
        Assert.Equal("\"a,b\"", SimpleCsv.Escape("a,b"));
        Assert.Equal("\"she said \"\"hi\"\"\"", SimpleCsv.Escape("she said \"hi\""));
    }
}
