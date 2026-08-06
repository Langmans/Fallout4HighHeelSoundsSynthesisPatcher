using System.Globalization;
using FO4HeelSoundPatcher.Detection;

namespace FO4HeelSoundPatcher.Tests;

public class HhsTxtParsingTests
{
    [Theory]
    [InlineData("Height=13.1", 13.1f)]
    [InlineData("Height=10.00", 10f)]
    [InlineData("height=8.2", 8.2f)]          // HHS matches the key case insensitively
    [InlineData("HEIGHT = 9.0", 9f)]          // and tolerates whitespace around the =
    [InlineData("Height=17", 17f)]            // whole numbers, no decimal point
    [InlineData("Height=-2.5", -2.5f)]        // HHS' own regex allows a leading minus
    [InlineData("Height=.5", 0.5f)]
    public void Recognised_forms_parse(string text, float expected)
    {
        var height = HhsTxtSource.ParseHeight(text, out var problem);

        Assert.Null(problem);
        Assert.Equal(expected, Assert.NotNull(height), precision: 4);
    }

    // These files are shipped by mod authors on machines with any decimal separator. Parsing them
    // with the current culture is the bug that keeps resurfacing, so pin both directions.
    [Theory]
    [InlineData("nl-NL")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void A_dot_decimal_parses_the_same_in_every_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var height = HhsTxtSource.ParseHeight("Height=13.1", out _);
            Assert.Equal(13.1f, Assert.NotNull(height), precision: 4);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_comma_decimal_is_accepted_too()
    {
        // Not something HHS handles, but a plausible author mistake and cheap to tolerate.
        Assert.Equal(13.1f, Assert.NotNull(HhsTxtSource.ParseHeight("Height=13,1", out _)), precision: 4);
    }

    [Fact]
    public void Comments_and_blank_lines_are_skipped()
    {
        var text = "; a comment\n\n# another\nHeight=12.5\n";

        Assert.Equal(12.5f, Assert.NotNull(HhsTxtSource.ParseHeight(text, out var problem)), precision: 4);
        Assert.Null(problem);
    }

    [Fact]
    public void Crlf_line_endings_work()
    {
        Assert.Equal(11.2f, Assert.NotNull(HhsTxtSource.ParseHeight("Height=11.2\r\n", out _)), precision: 4);
    }

    [Fact]
    public void A_file_without_a_height_line_reports_a_problem()
    {
        Assert.Null(HhsTxtSource.ParseHeight("Scale=2.0\n", out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void An_unparsable_value_reports_a_problem()
    {
        Assert.Null(HhsTxtSource.ParseHeight("Height=tall\n", out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_key_that_merely_ends_in_height_is_not_matched()
    {
        // HHS' regex_search would match this; being stricter here avoids a false reading.
        Assert.Null(HhsTxtSource.ParseHeight("SoleHeight=13.1\n", out _));
    }
}
