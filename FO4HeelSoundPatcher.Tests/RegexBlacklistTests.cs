using FO4HeelSoundPatcher.Filtering;
using FO4HeelSoundPatcher.Logging;

namespace FO4HeelSoundPatcher.Tests;

public class RegexBlacklistTests
{
    private static RegexBlacklist Build(IEnumerable<string> patterns, bool caseSensitive = false) =>
        new("test", patterns, caseSensitive, new PatcherLog(LogVerbosity.Quiet, logFilePath: null));

    [Fact]
    public void Plain_dotnet_patterns_work()
    {
        var blacklist = Build([@"\bboots$"]);

        Assert.Equal(@"\bboots$", blacklist.Match("Combat Boots"));
        Assert.Null(blacklist.Match("Bootstrap Heels"));
    }

    [Fact]
    public void Slash_delimited_patterns_have_their_delimiters_stripped()
    {
        var blacklist = Build([@"/\bboots$/i"]);

        Assert.Equal(@"/\bboots$/i", blacklist.Match("Combat BOOTS"));
    }

    [Fact]
    public void Matching_is_case_insensitive_by_default()
    {
        Assert.NotNull(Build(["heels"]).Match("HEELS"));
    }

    [Fact]
    public void Case_sensitivity_can_be_required()
    {
        var blacklist = Build(["heels"], caseSensitive: true);

        Assert.Null(blacklist.Match("HEELS"));
        Assert.NotNull(blacklist.Match("heels"));
    }

    [Fact]
    public void An_explicit_i_flag_wins_over_the_case_sensitive_setting()
    {
        Assert.NotNull(Build(["/heels/i"], caseSensitive: true).Match("HEELS"));
    }

    // A typo in one pattern must not take the whole run down, and must not silently disable the
    // patterns around it either.
    [Fact]
    public void An_invalid_pattern_is_dropped_and_the_rest_still_apply()
    {
        var blacklist = Build(["invalid[regex", "heels"]);

        Assert.NotNull(blacklist.Match("Party Heels"));
        Assert.Null(blacklist.Match("invalid[regex"));
    }

    [Fact]
    public void Blank_entries_are_ignored_rather_than_matching_everything()
    {
        var blacklist = Build(["", "   "]);

        Assert.True(blacklist.IsEmpty);
        Assert.Null(blacklist.Match("anything at all"));
    }

    [Fact]
    public void A_null_value_never_matches()
    {
        Assert.Null(Build([".*"]).Match(null));
    }

    [Fact]
    public void The_returned_pattern_is_the_one_the_user_typed()
    {
        // The log quotes this back, so it has to be recognisable rather than the compiled form.
        Assert.Equal("/heels$/i", Build(["/heels$/i"]).Match("VIP Heels"));
    }
}
