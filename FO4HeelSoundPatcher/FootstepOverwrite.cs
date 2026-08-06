using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher;

public static class FootstepSets
{
    /// <summary>
    /// Fallout 4's placeholder footstep set. The overwhelming majority of armor carries it simply
    /// because nobody changed it, so it is the one set that is safe to replace by default.
    /// </summary>
    public static readonly FormKey VanillaDefault = FormKey.Factory("03E091:Fallout4.esm");

    /// <summary>
    /// Whether the patcher may take over a piece's footstep set.
    /// <para>
    /// Everything the patcher is willing to replace comes from the user's list, including "no
    /// footstep set at all", which is <see cref="FormKey.Null"/> in it. Special-casing that in code
    /// would bury the same kind of decision the list exists to expose. Anything not listed is a
    /// choice its author made, and mods shipping their own heel sounds rely on it being kept.
    /// </para>
    /// </summary>
    public static bool MayReplace(
        IFormLinkNullableGetter<IFootstepSetGetter> existing,
        IReadOnlySet<FormKey> replaceable,
        bool replaceAnything)
    {
        return replaceAnything || replaceable.Contains(existing.FormKey);
    }
}
