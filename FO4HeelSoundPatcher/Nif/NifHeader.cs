using System.Text;

namespace FO4HeelSoundPatcher.Nif;

/// <summary>
/// A minimal NIF header reader, used to reject meshes without opening them properly.
/// <para>
/// The header sits in the first few kilobytes and already lists every block <i>type</i> the file
/// uses and every string it contains. That is enough to answer "could this mesh possibly hold HHS
/// data" without parsing a megabyte of geometry.
/// </para>
/// </summary>
public static class NifHeader
{
    private const string ExtraDataBlockType = "NiFloatExtraData";
    private const string HhsExtraDataName = "HHS";

    /// <summary>
    /// Reports whether the header mentions both the <c>NiFloatExtraData</c> block type and the
    /// string <c>HHS</c>. Both present is a maybe; either missing is a definite no.
    /// <para>
    /// Returns true on any parse trouble, so an unusual but valid mesh still gets the full parse
    /// rather than being silently dropped. <paramref name="diagnostic"/> then says why.
    /// </para>
    /// </summary>
    public static bool CouldContainHhsExtraData(Stream stream, out string? diagnostic)
    {
        diagnostic = null;

        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            // "Gamebryo File Format, Version 20.2.0.7\n"
            var magic = new StringBuilder();
            for (var i = 0; i < 128; i++)
            {
                var c = reader.ReadByte();
                if (c == '\n') break;
                magic.Append((char)c);
            }

            if (!magic.ToString().StartsWith("Gamebryo File Format", StringComparison.Ordinal))
            {
                diagnostic = "not a Gamebryo nif, parsing anyway";
                return true;
            }

            var version = reader.ReadUInt32();
            if (version < 0x14020005)
            {
                diagnostic = "file version predates the block size table, parsing anyway";
                return true;
            }

            reader.ReadByte();                      // endianness
            reader.ReadUInt32();                    // user version
            var blockCount = reader.ReadUInt32();
            var bsVersion = reader.ReadUInt32();

            SkipExportString(reader);               // author
            if (bsVersion > 130) reader.ReadUInt32();
            SkipExportString(reader);               // process script
            SkipExportString(reader);               // export script
            if (bsVersion == 130) SkipExportString(reader);   // max filepath

            var blockTypeCount = reader.ReadUInt16();
            var hasExtraDataType = false;
            for (var i = 0; i < blockTypeCount; i++)
            {
                if (ReadSizedString(reader) == ExtraDataBlockType) hasExtraDataType = true;
            }

            if (!hasExtraDataType) return false;

            stream.Seek(blockCount * 2L, SeekOrigin.Current);   // block type index
            stream.Seek(blockCount * 4L, SeekOrigin.Current);   // block sizes

            var stringCount = reader.ReadUInt32();
            reader.ReadUInt32();                                // max string length
            for (var i = 0u; i < stringCount; i++)
            {
                if (string.Equals(ReadSizedString(reader), HhsExtraDataName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            diagnostic = $"header scan inconclusive ({ex.GetType().Name}), parsing fully";
            return true;
        }
    }

    /// <summary>Export info string: one length byte, then that many bytes including a terminator.</summary>
    private static void SkipExportString(BinaryReader reader)
    {
        var length = reader.ReadByte();
        if (length > 0) reader.BaseStream.Seek(length, SeekOrigin.Current);
    }

    /// <summary>Header string: uint32 length, then exactly that many characters, no terminator.</summary>
    private static string ReadSizedString(BinaryReader reader)
    {
        var length = reader.ReadUInt32();
        if (length == 0) return string.Empty;
        if (length > 4096) throw new InvalidDataException($"implausible string length {length}");
        return Encoding.ASCII.GetString(reader.ReadBytes((int)length));
    }
}
