using System.Text;

namespace FO4HeelSoundPatcher.Tests;

/// <summary>
/// Builds a synthetic Fallout 4 NIF header, so the header scanner can be tested without shipping
/// mesh fixtures. The layout mirrors a real file: version 20.2.0.7, user version 12, BS version 130.
/// </summary>
internal sealed class NifHeaderBuilder
{
    private readonly List<string> _blockTypes = ["NiNode", "BSTriShape"];
    private readonly List<string> _strings = ["Scene Root", "LLeg_Foot"];

    public uint Version { get; set; } = 0x14020007;
    public uint UserVersion { get; set; } = 12;
    public uint BsVersion { get; set; } = 130;
    public uint BlockCount { get; set; } = 2;
    public string Magic { get; set; } = "Gamebryo File Format, Version 20.2.0.7";

    public NifHeaderBuilder WithBlockType(string name)
    {
        _blockTypes.Add(name);
        return this;
    }

    public NifHeaderBuilder WithString(string value)
    {
        _strings.Add(value);
        return this;
    }

    public MemoryStream Build()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write((byte)'\n');
        writer.Write(Version);
        writer.Write((byte)1);              // little endian
        writer.Write(UserVersion);
        writer.Write(BlockCount);
        writer.Write(BsVersion);

        WriteExportString(writer, string.Empty);                    // author
        if (BsVersion > 130) writer.Write(0u);                      // unknown int
        WriteExportString(writer, "Exported using a test.");        // process script
        WriteExportString(writer, string.Empty);                    // export script
        if (BsVersion == 130) WriteExportString(writer, string.Empty);   // max filepath

        writer.Write((ushort)_blockTypes.Count);
        foreach (var blockType in _blockTypes) WriteSizedString(writer, blockType);

        for (var i = 0u; i < BlockCount; i++) writer.Write((ushort)0);   // block type index
        for (var i = 0u; i < BlockCount; i++) writer.Write(8u);          // block sizes

        writer.Write((uint)_strings.Count);
        writer.Write((uint)(_strings.Count == 0 ? 0 : _strings.Max(s => s.Length)));
        foreach (var value in _strings) WriteSizedString(writer, value);

        writer.Write(0u);                   // group count

        writer.Write(new byte[64]);         // stand-in for the block data

        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    /// <summary>Length byte, then that many bytes including a terminator.</summary>
    private static void WriteExportString(BinaryWriter writer, string value)
    {
        if (value.Length == 0)
        {
            writer.Write((byte)0);
            return;
        }

        writer.Write((byte)(value.Length + 1));
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }

    /// <summary>uint32 length, then exactly that many characters, no terminator.</summary>
    private static void WriteSizedString(BinaryWriter writer, string value)
    {
        writer.Write((uint)value.Length);
        writer.Write(Encoding.ASCII.GetBytes(value));
    }
}
