using System.Text;
using FO4HeelSoundPatcher.Nif;

namespace FO4HeelSoundPatcher.Tests;

public class NifHeaderTests
{
    [Fact]
    public void A_mesh_with_the_block_type_and_the_name_is_a_candidate()
    {
        using var nif = new NifHeaderBuilder()
            .WithBlockType("NiFloatExtraData")
            .WithString("HHS")
            .Build();

        Assert.True(NifHeader.CouldContainHhsExtraData(nif, out var diagnostic));
        Assert.Null(diagnostic);
    }

    // This is the case that matters for speed: the overwhelming majority of meshes, rejected
    // without reading past the header.
    [Fact]
    public void A_plain_mesh_is_rejected()
    {
        using var nif = new NifHeaderBuilder().Build();

        Assert.False(NifHeader.CouldContainHhsExtraData(nif, out var diagnostic));
        Assert.Null(diagnostic);
    }

    [Fact]
    public void The_block_type_alone_is_not_enough()
    {
        using var nif = new NifHeaderBuilder().WithBlockType("NiFloatExtraData").Build();

        Assert.False(NifHeader.CouldContainHhsExtraData(nif, out _));
    }

    [Fact]
    public void The_string_alone_is_not_enough()
    {
        // A node or shape could legitimately be named HHS without any float extra data.
        using var nif = new NifHeaderBuilder().WithString("HHS").Build();

        Assert.False(NifHeader.CouldContainHhsExtraData(nif, out _));
    }

    [Fact]
    public void The_extra_data_name_is_matched_case_insensitively()
    {
        using var nif = new NifHeaderBuilder()
            .WithBlockType("NiFloatExtraData")
            .WithString("hhs")
            .Build();

        Assert.True(NifHeader.CouldContainHhsExtraData(nif, out _));
    }

    // Fallout 4 VR uses BS version 132, which inserts an extra uint32 into the header. Getting
    // this wrong desyncs the parse and would silently reject every VR mesh.
    [Fact]
    public void Bs_version_132_headers_parse()
    {
        using var nif = new NifHeaderBuilder { BsVersion = 132 }
            .WithBlockType("NiFloatExtraData")
            .WithString("HHS")
            .Build();

        Assert.True(NifHeader.CouldContainHhsExtraData(nif, out var diagnostic));
        Assert.Null(diagnostic);
    }

    [Fact]
    public void Bs_version_132_without_hhs_data_is_still_rejected()
    {
        using var nif = new NifHeaderBuilder { BsVersion = 132 }.Build();

        Assert.False(NifHeader.CouldContainHhsExtraData(nif, out _));
    }

    // Content that is not a NIF at all cannot be parsed as one, so it is rejected rather than
    // handed to the full parser. Archive entries that fail to decompress look exactly like this,
    // and passing them on cost one failed parse and one warning per file.
    [Fact]
    public void Content_that_is_not_a_nif_is_rejected_with_a_reason()
    {
        using var notANif = new MemoryStream(Encoding.ASCII.GetBytes("this is not a mesh at all\n"));

        Assert.False(NifHeader.CouldContainHhsExtraData(notANif, out var diagnostic));
        Assert.Contains("not a NIF", diagnostic);
    }

    // The older magic still has to be recognised, or a legitimate mesh would be dropped.
    [Fact]
    public void A_NetImmerse_header_is_recognised_as_a_nif()
    {
        using var nif = new NifHeaderBuilder { Magic = "NetImmerse File Format, Version 4.0.0.2" }.Build();

        // Rejected on its contents, not for being unrecognised - so no complaint about the header.
        Assert.False(NifHeader.CouldContainHhsExtraData(nif, out var diagnostic));
        Assert.Null(diagnostic);
    }

    [Fact]
    public void A_truncated_header_falls_through_to_the_full_parser()
    {
        using var full = new NifHeaderBuilder().WithBlockType("NiFloatExtraData").Build();
        using var truncated = new MemoryStream(full.ToArray()[..45]);

        Assert.True(NifHeader.CouldContainHhsExtraData(truncated, out var diagnostic));
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void An_older_file_version_falls_through_to_the_full_parser()
    {
        // Before 20.2.0.5 there is no block size table to seek past.
        using var nif = new NifHeaderBuilder { Version = 0x14000005 }.Build();

        Assert.True(NifHeader.CouldContainHhsExtraData(nif, out var diagnostic));
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void The_scanner_does_not_consume_the_stream_for_the_full_parser()
    {
        using var nif = new NifHeaderBuilder()
            .WithBlockType("NiFloatExtraData")
            .WithString("HHS")
            .Build();

        NifHeader.CouldContainHhsExtraData(nif, out _);

        // The reader rewinds and hands the same stream to Nifly, so it has to still be readable.
        nif.Position = 0;
        Assert.Equal((byte)'G', (byte)nif.ReadByte());
    }
}
