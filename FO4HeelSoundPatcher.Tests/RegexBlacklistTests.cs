using FO4HeelSoundPatcher.Filtering;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Tests;

public class RegexBlacklistTests
{
    private static readonly ModKey SomeMod = ModKey.FromFileName("SomeMod.esp");

    private static readonly HashSet<ModKey> LoadOrder =
        [ModKey.FromFileName("SomeMod.esp"), ModKey.FromFileName("Other.esp"),
         ModKey.FromFileName("IceStormsShoes.esl")];

    private static RegexBlacklist Build(IEnumerable<string> patterns, bool caseSensitive = false) =>
        new("test", patterns, caseSensitive, LoadOrder,
            new PatcherLog(LogVerbosity.Quiet, logFilePath: null));

    /// <summary>Most tests do not care which plugin a record came from.</summary>
    private static string? Match(RegexBlacklist blacklist, string? value) =>
        blacklist.Match(SomeMod, value);

    [Fact]
    public void Plain_dotnet_patterns_work()
    {
        var blacklist = Build([@"\bboots$"]);

        Assert.Equal(@"\bboots$", Match(blacklist, "Combat Boots"));
        Assert.Null(Match(blacklist, "Bootstrap Heels"));
    }

    [Fact]
    public void Slash_delimited_patterns_have_their_delimiters_stripped()
    {
        var blacklist = Build([@"/\bboots$/i"]);

        Assert.Equal(@"/\bboots$/i", Match(blacklist, "Combat BOOTS"));
    }

    [Fact]
    public void Matching_is_case_insensitive_by_default()
    {
        Assert.NotNull(Match(Build(["heels"]), "HEELS"));
    }

    [Fact]
    public void Case_sensitivity_can_be_required()
    {
        var blacklist = Build(["heels"], caseSensitive: true);

        Assert.Null(Match(blacklist, "HEELS"));
        Assert.NotNull(Match(blacklist, "heels"));
    }

    [Fact]
    public void An_explicit_i_flag_wins_over_the_case_sensitive_setting()
    {
        Assert.NotNull(Match(Build(["/heels/i"], caseSensitive: true), "HEELS"));
    }

    // A typo in one pattern must not take the whole run down, and must not silently disable the
    // patterns around it either.
    [Fact]
    public void An_invalid_pattern_is_dropped_and_the_rest_still_apply()
    {
        var blacklist = Build(["invalid[regex", "heels"]);

        Assert.NotNull(Match(blacklist, "Party Heels"));
        Assert.Null(Match(blacklist, "invalid[regex"));
    }

    [Fact]
    public void Blank_entries_are_ignored_rather_than_matching_everything()
    {
        var blacklist = Build(["", "   "]);

        Assert.True(blacklist.IsEmpty);
        Assert.Null(Match(blacklist, "anything at all"));
    }

    [Fact]
    public void A_null_value_never_matches()
    {
        Assert.Null(Match(Build([".*"]), null));
    }

    [Fact]
    public void The_returned_pattern_is_the_one_the_user_typed()
    {
        // The log quotes this back, so it has to be recognisable rather than the compiled form.
        Assert.Equal("/heels$/i", Match(Build(["/heels$/i"]), "VIP Heels"));
    }

    // Scoping an entry to one plugin keeps a loosely worded pattern from reaching other mods.
    [Fact]
    public void A_plugin_scoped_entry_only_applies_to_that_plugin()
    {
        var blacklist = Build(["SomeMod.esp:/_NCS$/i"]);

        Assert.NotNull(blacklist.Match(SomeMod, "AA_Shoes_NCS"));
        Assert.Null(blacklist.Match(ModKey.FromFileName("Other.esp"), "AA_Shoes_NCS"));
    }

    [Fact]
    public void An_unscoped_entry_still_applies_everywhere()
    {
        var blacklist = Build(["/_NCS$/i"]);

        Assert.NotNull(blacklist.Match(SomeMod, "AA_Shoes_NCS"));
        Assert.NotNull(blacklist.Match(ModKey.FromFileName("Other.esp"), "AA_Shoes_NCS"));
    }

    [Fact]
    public void The_scope_accepts_esl_and_esm_too()
    {
        var esl = ModKey.FromFileName("IceStormsShoes.esl");
        var blacklist = Build(["IceStormsShoes.esl:/_NCS$/i"]);

        Assert.NotNull(blacklist.Match(esl, "AA_AutumnShoesGothBoots_NCS"));
        Assert.Null(blacklist.Match(esl, "AA_AutumnShoesGothBoots"));
    }

    // A regex may contain colons, so only a prefix that parses as a plugin filename counts.
    [Fact]
    public void A_colon_inside_a_pattern_is_not_mistaken_for_a_scope()
    {
        var blacklist = Build([@"(?:heels|pumps)$"]);

        Assert.NotNull(blacklist.Match(SomeMod, "Party Heels"));
        Assert.NotNull(blacklist.Match(ModKey.FromFileName("Other.esp"), "Spike Pumps"));
    }

    [Fact]
    public void The_scope_is_reported_back_as_the_user_wrote_it()
    {
        // The log quotes this, so it has to include the prefix.
        Assert.Equal("SomeMod.esp:/_NCS$/i", Build(["SomeMod.esp:/_NCS$/i"]).Match(SomeMod, "X_NCS"));
    }

    // A scope naming a plugin that is not installed can never match. Silently doing nothing is
    // indistinguishable from working, so it has to be reported.
    [Fact]
    public void A_scope_naming_an_absent_plugin_is_warned_about()
    {
        using var log = new PatcherLog(LogVerbosity.Quiet, logFilePath: null);
        _ = new RegexBlacklist("test", ["NotInstalled.esp:/_NCS$/i"], false, LoadOrder, log);

        Assert.Equal(1, log.WarningCount);
    }

    [Fact]
    public void A_scope_naming_a_loaded_plugin_is_not_warned_about()
    {
        using var log = new PatcherLog(LogVerbosity.Quiet, logFilePath: null);
        _ = new RegexBlacklist("test", ["SomeMod.esp:/_NCS$/i"], false, LoadOrder, log);

        Assert.Equal(0, log.WarningCount);
    }

    [Fact]
    public void An_unscoped_pattern_is_never_warned_about()
    {
        using var log = new PatcherLog(LogVerbosity.Quiet, logFilePath: null);
        _ = new RegexBlacklist("test", ["/_NCS$/i"], false, LoadOrder, log);

        Assert.Equal(0, log.WarningCount);
    }

    // The other way a pattern quietly does nothing: it is valid and its plugin is loaded, but it
    // never matched. Only knowable once the run is over.
    [Fact]
    public void Patterns_that_matched_nothing_are_reported_at_the_end()
    {
        var logFile = Path.Combine(Path.GetTempPath(), $"fo4hhs-{Guid.NewGuid():N}.log");
        try
        {
            using (var log = new PatcherLog(LogVerbosity.Quiet, logFile))
            {
                var blacklist = new RegexBlacklist("test", ["heels", "nothingmatchesthis"], false, LoadOrder, log);
                blacklist.Match(SomeMod, "Party Heels");
                blacklist.ReportUnused(log);
            }

            // The constructor also lists every pattern, so only the tail after the unused header
            // is evidence of anything.
            var written = File.ReadAllText(logFile);
            var marker = written.IndexOf("matched nothing this run", StringComparison.Ordinal);
            Assert.True(marker >= 0, "no unused-pattern report was written");

            var report = written[marker..];
            Assert.Contains("nothingmatchesthis", report, StringComparison.Ordinal);
            Assert.DoesNotContain("heels", report, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(logFile)) File.Delete(logFile);
        }
    }
}
