using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda.Fallout4;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// HO3 / HHSOutfit3 (Nexus mod 82318). The heel height lives on the Armor record itself as a float
/// property named <c>HHSHeight</c> on an attached <c>HHSOutfit3</c> Papyrus script, so no mesh or
/// side-car file is involved.
/// <para>
/// A HO3 patch attaches the script as <c>HHSOutfit3:HHSOutfit3</c> to the Armor record and gives it
/// two float properties: <c>HHSHeight</c> (what we want) and <c>GroundClipAllowance</c> (ignored
/// here). HO3 deliberately allows <c>HHSHeight = 0</c> to mark flat shoes.
/// </para>
/// </summary>
public sealed class Ho3ScriptSource
{
    public const string SourceName = "HO3";

    private const string HeightPropertyName = "HHSHeight";

    /// <summary>
    /// Script names that carry an HHSHeight property. The VMAD stores the script name, which for
    /// HO3 is plain "HHSOutfit3"; niston's older hhsOutfit2 uses the same property name.
    /// </summary>
    private static readonly string[] ScriptNames = ["HHSOutfit3", "hhsOutfit2", "HHSOutfit"];

    private readonly PatcherLog _log;

    public Ho3ScriptSource(PatcherLog log) => _log = log;

    public HeelHeight? TryGetHeight(IArmorGetter armor)
    {
        var scripts = armor.VirtualMachineAdapter?.Scripts;
        if (scripts is null || scripts.Count == 0) return null;

        foreach (var script in scripts)
        {
            if (!IsHeelScript(script.Name)) continue;

            foreach (var property in script.Properties)
            {
                if (!string.Equals(property.Name, HeightPropertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (property)
                {
                    case IScriptFloatPropertyGetter floatProperty:
                        return new HeelHeight(floatProperty.Data, SourceName, $"script {script.Name}");

                    // Some patches store a whole number as an int property instead.
                    case IScriptIntPropertyGetter intProperty:
                        return new HeelHeight(intProperty.Data, SourceName, $"script {script.Name} (int)");

                    default:
                        _log.Warn(
                            $"{armor.FormKey} '{armor.EditorID}': script '{script.Name}' has an " +
                            $"{HeightPropertyName} property of unexpected type {property.GetType().Name}");
                        break;
                }
            }

            _log.Warn(
                $"{armor.FormKey} '{armor.EditorID}': script '{script.Name}' is attached but has no " +
                $"usable '{HeightPropertyName}' property");
        }

        return null;
    }

    private static bool IsHeelScript(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        // xEdit shows these as "HHSOutfit3:HHSOutfit3"; take the part after the last colon.
        var bare = name;
        var colon = bare.LastIndexOf(':');
        if (colon >= 0 && colon < bare.Length - 1) bare = bare[(colon + 1)..];

        return ScriptNames.Any(candidate => string.Equals(bare, candidate, StringComparison.OrdinalIgnoreCase));
    }
}
