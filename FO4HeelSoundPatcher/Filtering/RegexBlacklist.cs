using System.Text.RegularExpressions;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Filtering;

/// <summary>
/// A list of user supplied regexes, compiled once up front.
/// <para>
/// Accepts both plain .NET patterns (<c>\bboots$</c>) and the /pattern/flags form
/// (<c>/\bboots$/i</c>) that people are used to from other tools. An entry may also be scoped to a
/// single plugin by prefixing it (<c>IceStormsShoes.esl:/_NCS$/i</c>), which keeps a pattern narrow
/// enough to be worded loosely.
/// </para>
/// <para>
/// Everything here fails loudly. A pattern that will never do anything - because it does not
/// compile, because its plugin is not in the load order, or because nothing matched it - is
/// reported rather than quietly ignored, since a filter that silently does nothing looks exactly
/// like a filter that works.
/// </para>
/// </summary>
public sealed class RegexBlacklist
{
    private readonly List<Entry> _patterns = new();

    private sealed class Entry(ModKey? plugin, Regex regex, string original)
    {
        public ModKey? Plugin { get; } = plugin;
        public Regex Regex { get; } = regex;
        public string Original { get; } = original;
        public int Matches { get; set; }
    }

    public string Name { get; }

    public bool IsEmpty => _patterns.Count == 0;

    public RegexBlacklist(
        string name,
        IEnumerable<string> patterns,
        bool caseSensitive,
        IReadOnlySet<ModKey> loadOrder,
        PatcherLog log)
    {
        Name = name;

        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var trimmed = raw.Trim();
            var (plugin, remainder) = SplitPluginScope(trimmed);
            var (body, options) = Parse(remainder, caseSensitive);

            try
            {
                _patterns.Add(new Entry(plugin, new Regex(body, options | RegexOptions.CultureInvariant), trimmed));
                log.Debug($"{name}: compiled '{trimmed}' -> /{body}/ [{options}]");
            }
            catch (ArgumentException ex)
            {
                log.Warn($"{name}: invalid regex '{trimmed}' ignored - {ex.Message}");
                continue;
            }

            // A scope naming a plugin that is not loaded can never match, and is almost always a
            // typo or a leftover from a mod that has since been removed.
            if (plugin is { } scope && !loadOrder.Contains(scope))
            {
                log.Warn(
                    $"{name}: '{trimmed}' is limited to '{scope}', which is not in the load order, " +
                    "so this entry will never match anything");
            }
        }

        Report(log);
    }

    /// <summary>Lists the active entries and what each one is limited to.</summary>
    private void Report(PatcherLog log)
    {
        if (_patterns.Count == 0) return;

        var scoped = _patterns.Count(entry => entry.Plugin is not null);
        log.Info(
            $"{Name}: {_patterns.Count} pattern(s)" +
            (scoped > 0 ? $", {scoped} limited to one plugin" : string.Empty));

        foreach (var entry in _patterns)
        {
            var scope = entry.Plugin is { } plugin ? $"only records from {plugin}" : "any plugin";
            log.Info($"    {entry.Original}  ->  {scope}");
        }
    }

    /// <summary>
    /// Reports entries that matched nothing. Called once the run is over, since it is the only
    /// point at which this is knowable. Not necessarily a mistake - you might filter something you
    /// have not installed - but it is the other way a pattern can quietly do nothing.
    /// </summary>
    public void ReportUnused(PatcherLog log)
    {
        var unused = _patterns.Where(entry => entry.Matches == 0).ToList();
        if (unused.Count == 0) return;

        log.Info($"{Name}: {unused.Count} pattern(s) matched nothing this run:");
        foreach (var entry in unused)
        {
            log.Info($"    {entry.Original}");
        }
    }

    /// <summary>
    /// Splits an optional <c>Plugin.esp:</c> prefix off the front.
    /// <para>
    /// A regex may contain colons, so the text before the first one only counts as a scope when it
    /// actually parses as a plugin filename. That keeps <c>(?:foo)</c> and the like working
    /// untouched, at the cost of a pattern genuinely starting with something like
    /// <c>thing.esp:</c> needing to be escaped.
    /// </para>
    /// </summary>
    private static (ModKey? Plugin, string Pattern) SplitPluginScope(string raw)
    {
        var colon = raw.IndexOf(':');
        if (colon <= 0) return (null, raw);

        var candidate = raw[..colon];
        return ModKey.TryFromFileName(candidate, out var modKey)
            ? ((ModKey?)modKey, raw[(colon + 1)..].Trim())
            : (null, raw);
    }

    /// <summary>Splits /pattern/flags into its parts; anything else is used verbatim.</summary>
    private static (string Body, RegexOptions Options) Parse(string raw, bool caseSensitive)
    {
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        var text = raw.Trim();

        // Needs a closing slash somewhere after the first character to be delimiter syntax.
        if (text.Length >= 2 && text[0] == '/')
        {
            var close = text.LastIndexOf('/');
            if (close > 0)
            {
                var body = text[1..close];
                foreach (var flag in text[(close + 1)..])
                {
                    switch (flag)
                    {
                        case 'i': options |= RegexOptions.IgnoreCase; break;
                        case 'm': options |= RegexOptions.Multiline; break;
                        case 's': options |= RegexOptions.Singleline; break;
                        case 'x': options |= RegexOptions.IgnorePatternWhitespace; break;
                    }
                }

                return (body, options);
            }
        }

        return (text, options);
    }

    /// <summary>
    /// Returns the entry that matched, or null when nothing did.
    /// <paramref name="source"/> is the plugin the record originates from, which plugin-scoped
    /// entries are checked against.
    /// </summary>
    public string? Match(ModKey source, string? value)
    {
        if (value is null || _patterns.Count == 0) return null;

        foreach (var entry in _patterns)
        {
            if (entry.Plugin is { } plugin && plugin != source) continue;
            if (!entry.Regex.IsMatch(value)) continue;

            entry.Matches++;
            return entry.Original;
        }

        return null;
    }
}
