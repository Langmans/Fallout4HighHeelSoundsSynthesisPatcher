using System.Globalization;
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
    private const string HeightPropertyName = "HHSHeight";

    /// <summary>
    /// The script HO3 attaches. Its compiled name is <c>HHSOutfit3:HHSOutfit3</c>
    /// (<c>Scripts\HHSOutfit3\HHSOutfit3.pex</c>), so the VMAD entry can carry either the bare or
    /// the namespaced form.
    /// </summary>
    private const string Ho3ScriptName = "HHSOutfit3";

    /// <summary>
    /// Older/related scripts that may expose the same property. Unlike the HO3 script itself these
    /// are not verified, so a missing property on one of them is not worth warning about.
    /// </summary>
    private static readonly string[] RelatedScriptNames = ["hhsOutfit2", "HHSOutfit"];

    private readonly PatcherLog _log;

    public Ho3ScriptSource(PatcherLog log) => _log = log;

    public HeelHeight? TryGetHeight(IArmorGetter armor)
    {
        var scripts = armor.VirtualMachineAdapter?.Scripts;
        if (scripts is null || scripts.Count == 0) return null;

        foreach (var script in scripts)
        {
            var bareName = BareScriptName(script.Name);
            var isHo3 = string.Equals(bareName, Ho3ScriptName, StringComparison.OrdinalIgnoreCase);
            var isRelated = RelatedScriptNames.Any(
                candidate => string.Equals(bareName, candidate, StringComparison.OrdinalIgnoreCase));

            if (!isHo3 && !isRelated) continue;

            foreach (var property in script.Properties)
            {
                if (!string.Equals(property.Name, HeightPropertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // HO3 declares HHSHeight as a Float auto-property, but any numeric form converts
                // cleanly, so take whatever is there. The type is noted in the log only because it
                // is worth knowing if such an armor ever turns out to have no height in game -
                // whether the VM coerces a mismatched VMAD property type is not something this
                // patcher can check.
                switch (property)
                {
                    case IScriptFloatPropertyGetter floatProperty:
                        return new HeelHeight(floatProperty.Data, HeightSource.Ho3Script, $"script {script.Name}");

                    case IScriptIntPropertyGetter intProperty:
                        _log.Detail(
                            $"{armor.FormKey} '{armor.EditorID}': '{HeightPropertyName}' is stored as " +
                            $"an int ({intProperty.Data}) where {Ho3ScriptName} declares Float");
                        return new HeelHeight(intProperty.Data, HeightSource.Ho3Script, $"script {script.Name} (int)");

                    case IScriptStringPropertyGetter stringProperty
                        when float.TryParse(
                            stringProperty.Data, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                        _log.Detail(
                            $"{armor.FormKey} '{armor.EditorID}': '{HeightPropertyName}' is stored as " +
                            $"a string (\"{stringProperty.Data}\") where {Ho3ScriptName} declares Float");
                        return new HeelHeight(parsed, HeightSource.Ho3Script, $"script {script.Name} (string)");

                    default:
                        _log.Warn(
                            $"{armor.FormKey} '{armor.EditorID}': script '{script.Name}' has a " +
                            $"'{HeightPropertyName}' property of unusable type {property.GetType().Name}");
                        break;
                }
            }

            if (isHo3)
            {
                _log.Warn(
                    $"{armor.FormKey} '{armor.EditorID}': the {Ho3ScriptName} script is attached but " +
                    $"has no usable '{HeightPropertyName}' property");
            }
            else
            {
                _log.Detail(
                    $"{armor.FormKey} '{armor.EditorID}': script '{script.Name}' has no " +
                    $"'{HeightPropertyName}' property");
            }
        }

        return null;
    }

    /// <summary>xEdit shows these as "HHSOutfit3:HHSOutfit3"; take the part after the last colon.</summary>
    private static string BareScriptName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var colon = name.LastIndexOf(':');
        return colon >= 0 && colon < name.Length - 1 ? name[(colon + 1)..] : name;
    }
}
