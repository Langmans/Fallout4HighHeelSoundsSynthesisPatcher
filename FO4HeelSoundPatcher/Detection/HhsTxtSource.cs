using System.Globalization;
using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The classic HHS method: a .txt file next to the mesh with the same base name, containing a line
/// like <c>Height=13.1</c>.
/// <para>
/// So <c>meshes\some\path\heels.nif</c> is accompanied by <c>meshes\some\path\heels.txt</c>.
/// HHS also accepts a flat fallback in <c>Data\F4SE\Plugins\HHS\&lt;basename&gt;.txt</c>, keyed on
/// the file name alone, and checks it second (see <c>Text::GetHeightFromText</c> in the HHS
/// source). Both locations are tried here, in that order.
/// </para>
/// </summary>
public sealed class HhsTxtSource
{
    public const string SourceName = "HHS-txt";

    private readonly DataAssetLocator _assets;
    private readonly PatcherLog _log;
    private readonly Dictionary<string, float?> _cache = new(StringComparer.Ordinal);

    public HhsTxtSource(DataAssetLocator assets, PatcherLog log)
    {
        _assets = assets;
        _log = log;
    }

    private const string HhsF4seFolder = "f4se\\plugins\\hhs\\";

    public HeelHeight? TryGetHeight(string meshDataPath)
    {
        var besideMesh = Path.ChangeExtension(DataAssetLocator.Normalize(meshDataPath), ".txt");
        var inF4seFolder = HhsF4seFolder + Path.GetFileName(besideMesh);

        foreach (var txtPath in new[] { besideMesh, inF4seFolder })
        {
            if (!_cache.TryGetValue(txtPath, out var height))
            {
                height = Read(txtPath);
                _cache[txtPath] = height;
            }

            if (height.HasValue) return new HeelHeight(height.Value, SourceName, txtPath);
        }

        return null;
    }

    private float? Read(string txtPath)
    {
        if (!_assets.TryReadAllText(txtPath, out var text, out var origin)) return null;

        var height = ParseHeight(text, out var problem);

        if (problem is not null) _log.Warn($"{txtPath}: {problem}");
        else if (height.HasValue) _log.Debug($"txt height {Num.Height(height.Value)} from {txtPath} ({origin})");

        return height;
    }

    /// <summary>
    /// Pulls the height out of an HHS text file's contents.
    /// <para>
    /// HHS itself matches <c>height\s*=\s*(-?(?:\d*\.\d+|\d+))</c> case insensitively anywhere in
    /// the file. This is a little stricter - the key has to be the whole thing left of the
    /// <c>=</c> - but agrees on everything real files contain, and rejects a stray <c>xHeight=</c>
    /// that HHS would happily match.
    /// </para>
    /// <para>
    /// <paramref name="problem"/> is set when the file exists but is not usable, which is worth
    /// telling the user about; a file that simply has no height line is not an error.
    /// </para>
    /// </summary>
    public static float? ParseHeight(string text, out string? problem)
    {
        problem = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            if (!string.Equals(key, "Height", StringComparison.OrdinalIgnoreCase)) continue;

            var value = line[(separator + 1)..].Trim();

            // These files are written by mod authors on all sorts of machines, so parse with the
            // invariant culture and never with the current one. Accept a comma as decimal mark too.
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            problem = $"could not parse '{line}'";
            return null;
        }

        problem = "exists but contains no 'Height=' line";
        return null;
    }
}
