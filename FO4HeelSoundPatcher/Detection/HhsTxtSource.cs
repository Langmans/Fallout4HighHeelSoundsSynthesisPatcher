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
                _log.Debug($"txt height {parsed:0.00} from {txtPath} ({origin})");
                return parsed;
            }

            _log.Warn($"could not parse '{line}' in {txtPath}");
            return null;
        }

        _log.Warn($"{txtPath} exists but contains no 'Height=' line");
        return null;
    }
}
