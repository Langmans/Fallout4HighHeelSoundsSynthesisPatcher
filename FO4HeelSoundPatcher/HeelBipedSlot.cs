using Mutagen.Bethesda.Fallout4;

namespace FO4HeelSoundPatcher;

/// <summary>
/// The Fallout 4 biped slots, for the heel slot setting.
/// <para>
/// This deliberately mirrors <see cref="BipedObjectFlag"/> instead of using it. Synthesis builds
/// its settings UI by reflection, and for a list of enums it resolves the element type with
/// <c>param.Assembly.GetType(...)</c> against the <i>patcher's own</i> assembly. An enum that lives
/// in Mutagen therefore comes back null and the field renders as an unknown "?" control. Declaring
/// the enum here makes that lookup succeed.
/// </para>
/// <para>
/// Names carry the slot number because that is how the slots are labelled in xEdit and in mod
/// documentation, and the flag names alone ("Body", "LeftLegUnderArmor") are hard to line up with
/// them. Values match the corresponding <see cref="BipedObjectFlag"/> bits exactly, so the cast in
/// <see cref="HeelBipedSlotExt.ToFlag"/> is a straight reinterpretation: slot number 30 + bit index.
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "These names are what the user sees in the settings dropdown; the underscore " +
                    "separates the slot number from the slot name.")]
public enum HeelBipedSlot : uint
{
    Slot30_HairTop = 0x0000_0001,
    Slot31_HairLong = 0x0000_0002,
    Slot32_FaceGenHead = 0x0000_0004,
    Slot33_Body = 0x0000_0008,
    Slot34_LeftHand = 0x0000_0010,
    Slot35_RightHand = 0x0000_0020,
    Slot36_TorsoUnderArmor = 0x0000_0040,
    Slot37_LeftArmUnderArmor = 0x0000_0080,
    Slot38_RightArmUnderArmor = 0x0000_0100,
    Slot39_LeftLegUnderArmor = 0x0000_0200,
    Slot40_RightLegUnderArmor = 0x0000_0400,
    Slot41_TorsoArmor = 0x0000_0800,
    Slot42_LeftArmArmor = 0x0000_1000,
    Slot43_RightArmArmor = 0x0000_2000,
    Slot44_LeftLegArmor = 0x0000_4000,
    Slot45_RightLegArmor = 0x0000_8000,
    Slot46_Headband = 0x0001_0000,
    Slot47_Eyes = 0x0002_0000,
    Slot48_Beard = 0x0004_0000,
    Slot49_Mouth = 0x0008_0000,
    Slot50_Neck = 0x0010_0000,
    Slot51_Ring = 0x0020_0000,
    Slot52_Scalp = 0x0040_0000,
    Slot53_Decapitation = 0x0080_0000,
    Slot54_Unnamed = 0x0100_0000,
    Slot55_Unnamed = 0x0200_0000,
    Slot56_Unnamed = 0x0400_0000,
    Slot57_Unnamed = 0x0800_0000,
    Slot58_Unnamed = 0x1000_0000,
    Slot59_Shield = 0x2000_0000,
    Slot60_Pipboy = 0x4000_0000,
    Slot61_FX = 0x8000_0000,
}

public static class HeelBipedSlotExt
{
    public static BipedObjectFlag ToFlag(this HeelBipedSlot slot) => (BipedObjectFlag)slot;

    /// <summary>ORs a set of slots into the flag mask the ArmorAddon body template uses.</summary>
    public static BipedObjectFlag ToFlags(this IEnumerable<HeelBipedSlot> slots)
    {
        BipedObjectFlag combined = 0;
        foreach (var slot in slots) combined |= slot.ToFlag();
        return combined;
    }
}
