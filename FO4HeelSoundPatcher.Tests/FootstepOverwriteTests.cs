using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Tests;

/// <summary>
/// Whether an armor piece's existing footstep set counts as a deliberate choice.
/// <para>
/// This is what stops the patcher clobbering mods that ship their own heel sounds and wire them up
/// themselves. v1.1.0 did exactly that, because the only distinction it drew was "has a set" versus
/// "has none" — and nearly all armor has one, the vanilla placeholder.
/// </para>
/// </summary>
public class FootstepOverwriteTests
{
    private static ArmorAddon AddonWith(FormKey? footstepSet)
    {
        var addon = new ArmorAddon(FormKey.Factory("000800:Test.esp"), Fallout4Release.Fallout4);
        if (footstepSet is { } key) addon.FootstepSound.SetTo(key);
        return addon;
    }

    [Fact]
    public void No_footstep_set_is_not_deliberate()
    {
        Assert.False(FootstepSets.HasDeliberateSet(AddonWith(null)));
    }

    [Fact]
    public void The_vanilla_placeholder_is_not_deliberate()
    {
        // DefaultFootstepSetXXX is what armor carries when nobody chose anything.
        Assert.False(FootstepSets.HasDeliberateSet(AddonWith(FootstepSets.VanillaDefault)));
    }

    [Fact]
    public void A_set_from_another_mod_is_deliberate()
    {
        // Modelled on IceStorm's AutumnHighHeelsFootstepSet, which the patcher used to overwrite.
        var ownSound = FormKey.Factory("000821:IceStormsShoeSounds.esl");

        Assert.True(FootstepSets.HasDeliberateSet(AddonWith(ownSound)));
    }

    [Fact]
    public void A_vanilla_set_that_is_not_the_placeholder_is_deliberate()
    {
        // FSTBarefootFootstepSet is a real decision about how the piece sounds, unlike the
        // placeholder, so it must not be treated as free to replace.
        var barefoot = FormKey.Factory("021468:Fallout4.esm");

        Assert.True(FootstepSets.HasDeliberateSet(AddonWith(barefoot)));
    }

    [Fact]
    public void The_placeholder_points_at_the_record_it_claims_to()
    {
        // Read out of Fallout4.esm: DefaultFootstepSetXXX. If this drifts the whole rule inverts.
        Assert.Equal("Fallout4.esm", FootstepSets.VanillaDefault.ModKey.FileName);
        Assert.Equal(0x03E091u, FootstepSets.VanillaDefault.ID);
    }

    [Fact]
    public void The_default_policy_preserves_a_mod_s_own_sounds()
    {
        Assert.Equal(FootstepOverwrite.UnlessDeliberate, new SoundSettings().Overwrite);
    }
}
