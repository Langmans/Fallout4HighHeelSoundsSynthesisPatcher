using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

// Synthesis builds its settings UI by reflecting over this class and persists it as json. Public
// fields are the shape that whole system is written around, so CA1051 does not apply here.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design", "CA1051:Do not declare visible instance fields",
    Justification = "Synthesis' reflection-driven settings UI expects public fields.",
    Scope = "namespaceanddescendants", Target = "~N:FO4HeelSoundPatcher")]

namespace FO4HeelSoundPatcher;

public enum LogVerbosity
{
    /// <summary>Only warnings, errors and the final summary.</summary>
    Quiet,

    /// <summary>One line per patched record, plus warnings and the summary.</summary>
    Normal,

    /// <summary>Also one line per skipped record, with the reason.</summary>
    Detailed,

    /// <summary>Everything, including per-file lookups and cache hits.</summary>
    Debug,
}

public class Settings
{
    [SynthesisOrder]
    [SynthesisSettingName("Sound")]
    public SoundSettings Sound = new();

    [SynthesisOrder]
    [SynthesisSettingName("Detection")]
    public DetectionSettings Detection = new();

    [SynthesisOrder]
    [SynthesisSettingName("Filtering")]
    public FilterSettings Filtering = new();

    [SynthesisOrder]
    [SynthesisSettingName("Logging")]
    public LogSettings Logging = new();
}

public class SoundSettings
{
    [SynthesisOrder]
    [SynthesisSettingName("Heel footstep set")]
    [SynthesisTooltip(
        "The FootstepSet that gets assigned to matching ArmorAddon records.\n\n" +
        "Default is HHS_HeelFootstepSet (0026D8) from HighHeelSounds.esm, the master resource from " +
        "'High Heel Sounds' (Nexus mod 45345). The generated patch will list that plugin as a master.\n\n" +
        "You can point this at any other FootstepSet, for example one from HHFootsteps.esp.")]
    public IFormLinkGetter<IFootstepSetGetter> HeelFootstepSet =
        FormKey.Factory("0026D8:HighHeelSounds.esm").ToLinkGetter<IFootstepSetGetter>();

    [SynthesisOrder]
    [SynthesisSettingName("Only patch addons without a footstep set")]
    [SynthesisTooltip(
        "On: ArmorAddon records that already have some footstep set are left alone.\n" +
        "Off (default): any existing footstep set is overwritten with the heel set.\n\n" +
        "Leave this off. Nearly every Fallout 4 armor addon already points at the vanilla " +
        "DefaultFootstepSetXXX, so turning it on skips almost everything. It is here for the case " +
        "where another patcher has already assigned deliberate footstep sets you want to keep.\n\n" +
        "Addons that already point at the configured heel set are skipped either way.")]
    public bool OnlyIfFootstepUnset = false;
}

public class DetectionSettings
{
    [SynthesisOrder]
    [SynthesisSettingName("Read HO3 / HHSOutfit3 script property")]
    [SynthesisTooltip(
        "Read the heel height from the 'HHSHeight' float property of the HHSOutfit3 script " +
        "attached to an Armor record. This is the HO3 method (Nexus mod 82318).")]
    public bool EnableHo3Script = true;

    [SynthesisOrder]
    [SynthesisSettingName("Read HHS .txt files")]
    [SynthesisTooltip(
        "Read the heel height from a .txt file sitting next to the mesh with the same base name, " +
        "containing a line like 'Height=13.1'. This is the classic HHS method.")]
    public bool EnableHhsTxt = true;

    [SynthesisOrder]
    [SynthesisSettingName("Read HHS .json files")]
    [SynthesisTooltip(
        "Read heel heights from json files in Data\\F4SE\\Plugins\\HHS. Both keying styles are " +
        "supported: by mesh path ('key'/'value') and by plugin + FormID ('formid'/'gender'/'value').")]
    public bool EnableHhsJson = true;

    [SynthesisOrder]
    [SynthesisSettingName("Read HHS extra data inside meshes")]
    [SynthesisTooltip(
        "Read the heel height from a NiFloatExtraData block named 'HHS' inside the .nif itself.\n\n" +
        "This is the slowest source because meshes have to be opened. Meshes are only opened when " +
        "their header actually mentions NiFloatExtraData, and results are cached per path.")]
    public bool EnableHhsNif = true;

    [SynthesisOrder]
    [SynthesisSettingName("Minimum heel height")]
    [SynthesisTooltip(
        "Heels lower than this get no sound. HO3 deliberately uses HHSHeight=0 for flat shoes, so " +
        "keep this above 0 unless you want flat shoes to click too.\n\n" +
        "For reference: HO3 calls 0-13 the normal range.")]
    public float MinimumHeelHeight = 5.0f;

    [SynthesisOrder]
    [SynthesisSettingName("Maximum heel height (0 = no limit)")]
    [SynthesisTooltip("Heels higher than this get no sound. Set to 0 to disable the upper bound.")]
    public float MaximumHeelHeight = 0f;

    [SynthesisOrder]
    [SynthesisSettingName("Heel biped slots")]
    [SynthesisTooltip(
        "Only used when the heel height was found on the Armor record itself (the HO3 script) " +
        "rather than on a specific mesh. In that case only the ArmorAddons covering one of these " +
        "slots get the sound.\n\n" +
        "Fallout 4 has no dedicated feet slot, so heels normally live in Body (33) or the leg slots.")]
    public List<HeelBipedSlot> HeelSlots = new()
    {
        HeelBipedSlot.Slot33_Body,
        HeelBipedSlot.Slot39_LeftLegUnderArmor,
        HeelBipedSlot.Slot40_RightLegUnderArmor,
        HeelBipedSlot.Slot44_LeftLegArmor,
        HeelBipedSlot.Slot45_RightLegArmor,
    };

    [SynthesisOrder]
    [SynthesisSettingName("Fall back to all addons when no slot matches")]
    [SynthesisTooltip(
        "If an Armor has a heel height but none of its ArmorAddons cover one of the slots above, " +
        "patch every addon that has a world model instead of skipping the armor entirely.")]
    public bool FallbackToAllAddons = true;

    [SynthesisOrder]
    [SynthesisSettingName("Check female world model")]
    [SynthesisTooltip("Look for .txt / .nif / json-by-mesh data using the female world model path.")]
    public bool CheckFemaleModel = true;

    [SynthesisOrder]
    [SynthesisSettingName("Check male world model")]
    [SynthesisTooltip("Also look using the male world model path when the female one yields nothing.")]
    public bool CheckMaleModel = true;
}

public class FilterSettings
{
    [SynthesisOrder]
    [SynthesisSettingName("Armor name blacklist (regex)")]
    [SynthesisTooltip(
        "Regular expressions matched against the armor's display name (FULL). A match means no sound.\n\n" +
        "Plain .NET regex works (\\bboots$) and so does /pattern/flags notation (/\\bboots$/i).\n" +
        "Supported flags: i (ignore case), m (multiline), s (dot matches newline), x (ignore whitespace).\n\n" +
        "Invalid patterns are reported as a warning in the log and then ignored.")]
    public List<string> ArmorNameBlacklist = new();

    [SynthesisOrder]
    [SynthesisSettingName("Editor ID blacklist (regex)")]
    [SynthesisTooltip("Same as the name blacklist, but matched against the armor's Editor ID.")]
    public List<string> EditorIdBlacklist = new();

    [SynthesisOrder]
    [SynthesisSettingName("Match regexes case sensitively")]
    [SynthesisTooltip(
        "Off (default): every pattern gets IgnoreCase.\n" +
        "On: patterns are case sensitive unless they carry an explicit /i flag.")]
    public bool RegexCaseSensitive = false;

    [SynthesisOrder]
    [SynthesisSettingName("Plugin blacklist")]
    [SynthesisTooltip("Armor records originating from these plugins are never patched.")]
    public List<ModKey> ModBlacklist = new();

    [SynthesisOrder]
    [SynthesisSettingName("Armor blacklist")]
    [SynthesisTooltip("Individual Armor records that are never patched.")]
    public List<IFormLinkGetter<IArmorGetter>> ArmorBlacklist = new();
}

public class LogSettings
{
    [SynthesisOrder]
    [SynthesisSettingName("Verbosity")]
    [SynthesisTooltip(
        "Quiet: warnings, errors and the summary only.\n" +
        "Normal: also one line per patched record.\n" +
        "Detailed: also one line per skipped record, with the reason.\n" +
        "Debug: everything, including individual file lookups.")]
    public LogVerbosity Verbosity = LogVerbosity.Normal;

    [SynthesisOrder]
    [SynthesisSettingName("Write a log file")]
    [SynthesisTooltip(
        "Write the full log next to the generated patch. The file always contains Debug level " +
        "detail regardless of the verbosity above, so there is something to read after a bad run.")]
    public bool WriteLogFile = true;

    [SynthesisOrder]
    [SynthesisSettingName("Log file name")]
    public string LogFileName = "HeelSoundPatcher.log";

    [SynthesisOrder]
    [SynthesisSettingName("Dry run")]
    [SynthesisTooltip(
        "Do all the detection and logging but write no records at all. Useful for checking what " +
        "would happen before committing to it.")]
    public bool DryRun = false;
}
