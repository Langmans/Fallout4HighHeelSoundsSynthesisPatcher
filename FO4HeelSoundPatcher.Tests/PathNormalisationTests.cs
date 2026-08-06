using FO4HeelSoundPatcher.Assets;

namespace FO4HeelSoundPatcher.Tests;

public class PathNormalisationTests
{
    [Theory]
    [InlineData(@"DX\Pornstar\shoes.nif", @"dx\pornstar\shoes.nif")]
    [InlineData("DX/Pornstar/shoes.nif", @"dx\pornstar\shoes.nif")]
    [InlineData(@"\DX\shoes.nif", @"dx\shoes.nif")]
    [InlineData(@"  DX\shoes.nif  ", @"dx\shoes.nif")]
    public void Normalize_lowercases_and_uses_backslashes(string input, string expected)
    {
        Assert.Equal(expected, DataAssetLocator.Normalize(input));
    }

    // Record paths are relative to meshes\, but mods are inconsistent about it. Getting this wrong
    // is what makes the Skyrim reference patcher miss archived and prefixed meshes entirely.
    [Theory]
    [InlineData(@"vtaw\dress\shoes.nif", @"meshes\vtaw\dress\shoes.nif")]
    [InlineData(@"meshes\vtaw\dress\shoes.nif", @"meshes\vtaw\dress\shoes.nif")]
    [InlineData(@"Meshes\Vtaw\Shoes.nif", @"meshes\vtaw\shoes.nif")]
    [InlineData("vtaw/dress/shoes.nif", @"meshes\vtaw\dress\shoes.nif")]
    public void ToMeshDataPath_adds_the_meshes_prefix_only_when_missing(string input, string expected)
    {
        Assert.Equal(expected, DataAssetLocator.ToMeshDataPath(input));
    }

    // HHS' own File::GetRelativeDir strips a leading data\ rather than nesting under meshes\.
    [Theory]
    [InlineData(@"data\meshes\vtaw\shoes.nif", @"meshes\vtaw\shoes.nif")]
    [InlineData(@"Data\Meshes\Vtaw\Shoes.nif", @"meshes\vtaw\shoes.nif")]
    public void ToMeshDataPath_strips_a_leading_data_folder(string input, string expected)
    {
        Assert.Equal(expected, DataAssetLocator.ToMeshDataPath(input));
    }
}
