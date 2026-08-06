using System.Globalization;
using System.Numerics;
using Mutagen.Bethesda.Fallout4;

namespace FO4HeelSoundPatcher.Tests;

public class HeelBipedSlotTests
{
    // HeelBipedSlot is a hand-written copy of Mutagen's enum, which exists only because Synthesis'
    // settings UI cannot resolve an enum from another assembly inside a list. A copy can drift, so
    // pin every member against the original.
    [Fact]
    public void Every_slot_maps_to_the_flag_of_the_same_name()
    {
        foreach (var slot in Enum.GetValues<HeelBipedSlot>())
        {
            var expectedName = slot.ToString().Split('_', 2)[1];
            if (expectedName == "Unnamed") continue;   // Unnamed54..58 have numbered flag names

            var flag = Enum.Parse<BipedObjectFlag>(expectedName);
            Assert.Equal(flag, slot.ToFlag());
        }
    }

    [Fact]
    public void Slot_numbers_in_the_names_match_the_bit_positions()
    {
        // Fallout 4 biped slots are numbered from 30, one per bit.
        foreach (var slot in Enum.GetValues<HeelBipedSlot>())
        {
            var declaredNumber = int.Parse(slot.ToString()[4..6], CultureInfo.InvariantCulture);
            var bitIndex = BitOperations.TrailingZeroCount((uint)slot);

            Assert.Equal(declaredNumber, 30 + bitIndex);
        }
    }

    [Fact]
    public void The_enum_covers_all_thirty_two_slots()
    {
        Assert.Equal(32, Enum.GetValues<HeelBipedSlot>().Length);
    }

    [Fact]
    public void ToFlags_ors_the_selection_together()
    {
        var combined = new[] { HeelBipedSlot.Slot33_Body, HeelBipedSlot.Slot39_LeftLegUnderArmor }.ToFlags();

        Assert.Equal(BipedObjectFlag.Body | BipedObjectFlag.LeftLegUnderArmor, combined);
    }

    [Fact]
    public void ToFlags_of_nothing_is_zero()
    {
        Assert.Equal((BipedObjectFlag)0, Array.Empty<HeelBipedSlot>().ToFlags());
    }

    [Fact]
    public void The_default_selection_is_body_and_the_leg_slots()
    {
        var defaults = new DetectionSettings().HeelSlots.ToFlags();

        Assert.Equal(
            BipedObjectFlag.Body
            | BipedObjectFlag.LeftLegUnderArmor | BipedObjectFlag.RightLegUnderArmor
            | BipedObjectFlag.LeftLegArmor | BipedObjectFlag.RightLegArmor,
            defaults);
    }
}
