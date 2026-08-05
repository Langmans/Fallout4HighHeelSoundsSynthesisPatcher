using System.Text.RegularExpressions;
using FO4HeelSoundPatcher.Logging;

namespace FO4HeelSoundPatcher.Filtering;

/// <summary>
/// A list of user supplied regexes, compiled once up front.
/// <para>
/// Accepts both plain .NET patterns (<c>\bboots$</c>) and the /pattern/flags form
/// (<c>/\bboots$/i</c>) that people are used to from other tools. A pattern that fails to compile
/// is reported and dropped rather than taking the whole run down.
/// </para>
/// </summary>
public sealed class RegexBlacklist
{
    private readonly List<(Regex Regex, string Original)> _patterns = new();

    public string Name { get; }

    public bool IsEmpty => _patterns.Count == 0;

    public RegexBlacklist(string name, IEnumerable<string> patterns, bool caseSensitive, PatcherLog log)
    {
        Name = name;

        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var (body, options) = Parse(raw, caseSensitive);
            try
            {
                _patterns.Add((new Regex(body, options | RegexOptions.CultureInvariant), raw.Trim()));
                log.Debug($"{name}: compiled '{raw.Trim()}' -> /{body}/ [{options}]");
            }
            catch (ArgumentException ex)
            {
                log.Warn($"{name}: invalid regex '{raw.Trim()}' ignored - {ex.Message}");
            }
        }
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

    /// <summary>Returns the pattern that matched, or null when nothing matched.</summary>
    public string? Match(string? value)
    {
        if (value is null || _patterns.Count == 0) return null;

        foreach (var (regex, original) in _patterns)
        {
            if (regex.IsMatch(value)) return original;
        }

        return null;
    }
}
