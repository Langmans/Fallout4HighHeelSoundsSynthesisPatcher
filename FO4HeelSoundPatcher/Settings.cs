using FO4HeelSoundPatcher.Detection;
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
    [SynthesisSettingName("Footstep sets that may be replaced")]
    [SynthesisTooltip(
        "An armor piece whose current footstep set is listed here gets the heel set anyway. " +
        "Anything else is left alone, on the grounds that its author chose it deliberately.\n\n" +
        "Two entries by default:\n" +
        "  - the empty entry, meaning a piece with no footstep set at all\n" +
        "  - DefaultFootstepSetXXX, Fallout 4's placeholder, which nearly all armor carries " +
        "simply because nobody changed it\n\n" +
        "Add a set here to overrule it. For example, IceStorm's Shoes assigns its own " +
        "AutumnHighHeelsFootstepSet; listing that replaces it with your heel sound everywhere.\n\n" +
        "Remove an entry to protect it. Clearing the list entirely means nothing is ever taken " +
        "over, so nothing gets patched.\n\n" +
        "A piece already pointing at the configured heel set is skipped regardless.")]
    public List<IFormLinkGetter<IFootstepSetGetter>> ReplaceableFootstepSets =
    [
        FormLink<IFootstepSetGetter>.Null,
        FootstepSets.VanillaDefault.ToLinkGetter<IFootstepSetGetter>(),
    ];

    [SynthesisOrder]
    [SynthesisSettingName("Replace any footstep set")]
    [SynthesisTooltip(
        "Ignore the list above and take over every armor piece, whatever it points at.\n\n" +
        "Use this if you want one heel sound throughout and do not care what individual mods " +
        "chose. Off by default, because it silently discards those choices.")]
    public bool ReplaceAnyFootstepSet;
}

public class DetectionSettings
{
    [SynthesisOrder]
    [SynthesisSettingName("Use the default detection order")]
    [SynthesisTooltip(
        "On (default): consult the sources in the order HHS itself uses, and ignore the list below.\n" +
        "Off: use the list below instead.\n\n" +
        "Turning this back on is how you undo a custom order - the list is left as you had it.")]
    public bool UseDefaultSourceOrder = true;

    [SynthesisOrder]
    [SynthesisSettingName("Detection order")]
    [SynthesisTooltip(
        "Which places to look for a heel height, and in what order. The first source that has a " +
        "height for a piece of armor wins; the rest are not consulted for it.\n\n" +
        "Remove a source to stop reading it entirely. Leaving the list empty falls back to the " +
        "default order rather than detecting nothing.\n\n" +
        "Only used when 'Use the default detection order' is off.\n\n" +
        "  HhsJson    json files in Data\\F4SE\\Plugins\\HHS\n" +
        "  HhsNif     a NiFloatExtraData block named HHS inside the mesh\n" +
        "  HhsTxt     a .txt next to the mesh containing Height=13.1\n" +
        "  Ho3Script  the HHSHeight property of the HO3 HHSOutfit3 script\n\n" +
        "The default is HhsJson, HhsNif, HhsTxt, Ho3Script. The first three come from the mesh and " +
        "point at one specific armor piece; Ho3Script marks the whole armor, so where you put it " +
        "decides whether it overrides mesh data or only fills the gaps.\n\n" +
        "HhsNif is the slow one - it has to open meshes. Removing it is the way to speed up a run.")]
    public List<HeightSource> SourcePriority = HeightSourceOrder.Default.ToList();

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
    [SynthesisSettingName("Search inside BA2 archives")]
    [SynthesisTooltip(
        "On (default): look for txt, json and mesh files inside BA2 archives as well as loose on " +
        "disk. Off: loose files only.\n\n" +
        "Only turn this off if you know every mod in your load order ships its heel data loose. " +
        "HHS reads all three from archives too, so a mod that packs them will be raised in game " +
        "but get no sound from this patcher.\n\n" +
        "The archive contents are indexed once, on the first file that is not found loose.")]
    public bool SearchArchives = true;

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
        "An entry can be limited to one plugin by prefixing it:\n" +
        "  SomeMod.esp:/\\bboots$/i\n" +
        "Records from any other plugin then ignore that entry, which lets a loosely worded pattern " +
        "stay safe. The prefix only counts when it parses as a plugin filename, so a regex " +
        "containing a colon still works.\n\n" +
        "Invalid patterns are reported as a warning in the log and then ignored.")]
    public List<string> ArmorNameBlacklist = new();

    [SynthesisOrder]
    [SynthesisSettingName("Editor ID blacklist (regex)")]
    [SynthesisTooltip(
        "Same as the name blacklist, but matched against the armor's Editor ID, and it takes the " +
        "same optional plugin prefix.\n\n" +
        "Useful when a mod ships a deliberate no-sound variant of each item. IceStorm's Shoes, for " +
        "instance, gives every shoe an _NCS copy that the armor workbench switches to when you " +
        "pick vanilla footsteps; patching those would break that choice:\n" +
        "  IceStormsShoes.esl:/_NCS$/i")]
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

    [SynthesisOrder]
    [SynthesisSettingName("Armor addon Editor ID blacklist (regex)")]
    [SynthesisTooltip(
        "Regular expressions matched against the ArmorAddon's Editor ID. A match means that one " +
        "piece is skipped, while the rest of the armor is still patched.\n\n" +
        "The two blacklists above look at the Armor record. This one is needed when the difference " +
        "lives on the piece rather than the outfit - which is where the footstep set lives too.\n\n" +
        "IceStorm's Shoes is the case in point: every shoe has a second addon suffixed _NCS, \"no " +
        "custom sound\", which the armor workbench switches to when you pick vanilla footsteps. " +
        "The Armor record is named the same either way, so only the addon's Editor ID can tell " +
        "them apart:\n" +
        "  IceStormsShoes.esl:/_NCS$/i\n\n" +
        "Same syntax as the other two, including the plugin prefix. ArmorAddon records have no " +
        "display name, so there is no equivalent name filter.")]
    public List<string> AddonEditorIdBlacklist = new();

    [SynthesisOrder]
    [SynthesisSettingName("Armor addon blacklist")]
    [SynthesisTooltip("Individual ArmorAddon records that never get the heel set.")]
    public List<IFormLinkGetter<IArmorAddonGetter>> ArmorAddonBlacklist = new();
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
