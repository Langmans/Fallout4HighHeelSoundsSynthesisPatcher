using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Tests;

/// <summary>
/// Which existing footstep sets the patcher is willing to take over.
/// <para>
/// This is what stops it clobbering mods that ship their own heel sounds and wire them up
/// themselves. v1.1.0 did exactly that, because the only distinction it drew was "has a set" versus
/// "has none" — and nearly all armor has one, the vanilla placeholder.
/// </para>
/// </summary>
public class FootstepOverwriteTests
{
    private static readonly FormKey OwnSound = FormKey.Factory("000821:IceStormsShoeSounds.esl");
    private static readonly FormKey Barefoot = FormKey.Factory("021468:Fallout4.esm");

    private static IFormLinkNullableGetter<IFootstepSetGetter> Existing(FormKey? set)
    {
        var addon = new ArmorAddon(FormKey.Factory("000800:Test.esp"), Fallout4Release.Fallout4);
        if (set is { } key) addon.FootstepSound.SetTo(key);
        return addon.FootstepSound;
    }

    private static HashSet<FormKey> Defaults() =>
        new SoundSettings().ReplaceableFootstepSets.Select(link => link.FormKey).ToHashSet();

    private static bool MayReplace(FormKey? existing, IReadOnlySet<FormKey>? replaceable = null) =>
        FootstepSets.MayReplace(Existing(existing), replaceable ?? Defaults(), replaceAnything: false);

    [Fact]
    public void A_piece_with_no_footstep_set_is_taken_over()
    {
        Assert.True(MayReplace(null));
    }

    [Fact]
    public void The_vanilla_placeholder_is_taken_over()
    {
        Assert.True(MayReplace(FootstepSets.VanillaDefault));
    }

    [Fact]
    public void A_set_from_another_mod_is_kept()
    {
        // IceStorm's AutumnHighHeelsFootstepSet, which the patcher used to overwrite.
        Assert.False(MayReplace(OwnSound));
    }

    [Fact]
    public void A_vanilla_set_that_is_not_the_placeholder_is_kept()
    {
        // FSTBarefootFootstepSet is a real decision about how the piece sounds, unlike the
        // placeholder, so it must not be treated as free to replace.
        Assert.False(MayReplace(Barefoot));
    }

    // The point of the list being a setting: a user who wants uniform heel sounds can add the
    // mod's own set and have it taken over after all.
    [Fact]
    public void Listing_a_mod_s_own_set_makes_it_replaceable()
    {
        var replaceable = Defaults().Append(OwnSound).ToHashSet();

        Assert.True(MayReplace(OwnSound, replaceable));
    }

    // "No footstep set" is an entry in the list rather than a special case in code, so removing it
    // has to actually protect those pieces.
    [Fact]
    public void Removing_the_empty_entry_protects_pieces_with_no_set()
    {
        var withoutNull = Defaults().Where(key => !key.IsNull).ToHashSet();

        Assert.False(MayReplace(null, withoutNull));
        Assert.True(MayReplace(FootstepSets.VanillaDefault, withoutNull));
    }

    [Fact]
    public void An_empty_list_takes_over_nothing()
    {
        var none = new HashSet<FormKey>();

        Assert.False(MayReplace(null, none));
        Assert.False(MayReplace(FootstepSets.VanillaDefault, none));
    }

    [Fact]
    public void Replace_anything_ignores_the_list()
    {
        var none = new HashSet<FormKey>();

        Assert.True(FootstepSets.MayReplace(Existing(OwnSound), none, replaceAnything: true));
    }

    [Fact]
    public void The_placeholder_points_at_the_record_it_claims_to()
    {
        // Read out of Fallout4.esm: DefaultFootstepSetXXX. If this drifts the whole rule inverts.
        Assert.Equal("Fallout4.esm", FootstepSets.VanillaDefault.ModKey.FileName);
        Assert.Equal(0x03E091u, FootstepSets.VanillaDefault.ID);
    }

    [Fact]
    public void The_shipped_default_covers_unset_and_the_placeholder_only()
    {
        Assert.Equal(
            [FormKey.Null, FootstepSets.VanillaDefault],
            new SoundSettings().ReplaceableFootstepSets.Select(link => link.FormKey));
    }
}
