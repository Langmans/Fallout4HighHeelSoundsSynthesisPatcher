using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher;

/// <summary>How far to go when an armor piece already points at a footstep set.</summary>
public enum FootstepOverwrite
{
    /// <summary>
    /// Replace nothing that looks deliberate: patch a piece with no footstep set, or one still on
    /// the vanilla placeholder, and leave anything else as its author set it.
    /// </summary>
    UnlessDeliberate,

    /// <summary>Only patch a piece with no footstep set at all.</summary>
    OnlyWhenUnset,

    /// <summary>Replace whatever is there.</summary>
    Always,
}

public static class FootstepSets
{
    /// <summary>
    /// Fallout 4's placeholder footstep set, which the overwhelming majority of armor carries
    /// simply because nobody changed it. Treating it as "nothing set" is what makes
    /// <see cref="FootstepOverwrite.UnlessDeliberate"/> useful rather than a no-op.
    /// </summary>
    public static readonly FormKey VanillaDefault =
        FormKey.Factory("03E091:Fallout4.esm");

    /// <summary>
    /// True when the piece carries a footstep set its author chose.
    /// <para>
    /// Note that the vanilla barefoot and power armor sets do <i>not</i> count as placeholders:
    /// unlike DefaultFootstepSetXXX they are a real decision about how that piece should sound.
    /// </para>
    /// </summary>
    public static bool HasDeliberateSet(IArmorAddonGetter addon) =>
        !addon.FootstepSound.IsNull && addon.FootstepSound.FormKey != VanillaDefault;
}
