using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;

namespace FO4HeelSoundPatcher.Tests;

/// <summary>
/// Exercises the loose-file half of the locator against a real temporary folder. The BA2 half needs
/// actual archives and is covered by running the patcher against a game install instead.
/// </summary>
public sealed class DataAssetLocatorTests : IDisposable
{
    private readonly string _dataFolder =
        Path.Combine(Path.GetTempPath(), "fo4hhs-tests-" + Guid.NewGuid().ToString("N"));

    private readonly DataAssetLocator _locator;

    public DataAssetLocatorTests()
    {
        Directory.CreateDirectory(_dataFolder);
        _locator = new DataAssetLocator(
            _dataFolder, new PatcherLog(LogVerbosity.Quiet, logFilePath: null), loadOrder: []);
    }

    private void WriteFile(string relativePath, string contents)
    {
        var full = Path.Combine(_dataFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    [Fact]
    public void A_loose_file_is_found()
    {
        WriteFile(@"meshes\vtaw\shoes.txt", "Height=12.5");

        Assert.True(_locator.TryReadAllText(@"meshes\vtaw\shoes.txt", out var text, out var origin));
        Assert.Equal("Height=12.5", text);
        Assert.Equal("loose", origin);
    }

    [Fact]
    public void Lookup_is_case_insensitive_and_separator_agnostic()
    {
        WriteFile(@"meshes\vtaw\shoes.txt", "Height=12.5");

        Assert.True(_locator.Exists(@"Meshes\VTAW\Shoes.txt"));
        Assert.True(_locator.Exists("meshes/vtaw/shoes.txt"));
    }

    [Fact]
    public void A_missing_file_reports_false_rather_than_throwing()
    {
        Assert.False(_locator.Exists(@"meshes\nope.txt"));
        Assert.False(_locator.TryReadAllText(@"meshes\nope.txt", out _, out _));
    }

    [Fact]
    public void ListFiles_returns_direct_children_with_the_extension()
    {
        WriteFile(@"f4se\plugins\hhs\one.json", "{}");
        WriteFile(@"f4se\plugins\hhs\two.json", "{}");
        WriteFile(@"f4se\plugins\hhs\notes.txt", "ignored");
        WriteFile(@"f4se\plugins\hhs\nested\three.json", "{}");

        var files = _locator.ListFiles(@"F4SE\Plugins\HHS", ".json");

        Assert.Equal(
            [@"f4se\plugins\hhs\one.json", @"f4se\plugins\hhs\two.json"],
            files);
    }

    [Fact]
    public void ListFiles_on_a_missing_folder_is_empty_rather_than_an_error()
    {
        Assert.Empty(_locator.ListFiles(@"F4SE\Plugins\HHS", ".json"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataFolder)) Directory.Delete(_dataFolder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }
}
