using FO4HeelSoundPatcher.Assets;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Tests;

/// <summary>
/// Which archives get searched. Mutagen's no-argument lookup only returns ini-listed archives, so
/// getting this wrong silently ignores every mod archive in the load order - which is exactly what
/// happened before these existed.
/// </summary>
public sealed class ArchiveCollectionTests : IDisposable
{
    private readonly string _dataFolder =
        Path.Combine(Path.GetTempPath(), "fo4hhs-archives-" + Guid.NewGuid().ToString("N"));

    public ArchiveCollectionTests() => Directory.CreateDirectory(_dataFolder);

    private void WriteArchive(string name) => File.WriteAllBytes(Path.Combine(_dataFolder, name), [0]);

    private List<string> Collect(params string[] plugins) =>
        DataAssetLocator
            .CollectArchives(_dataFolder, plugins.Select(p => ModKey.FromFileName(p)))
            .Select(path => path.Name.String)
            .ToList();

    [Fact]
    public void An_archive_named_after_a_plugin_is_found()
    {
        WriteArchive("SomeMod.ba2");

        Assert.Equal(["SomeMod.ba2"], Collect("SomeMod.esp"));
    }

    // This is the form nearly every mod actually ships.
    [Fact]
    public void The_type_suffix_form_is_found()
    {
        WriteArchive("SomeMod - Main.ba2");
        WriteArchive("SomeMod - Textures.ba2");

        Assert.Equal(2, Collect("SomeMod.esp").Count);
    }

    [Fact]
    public void An_archive_belonging_to_no_enabled_plugin_is_ignored()
    {
        WriteArchive("SomeMod - Main.ba2");
        WriteArchive("NotInLoadOrder - Main.ba2");

        Assert.Equal(["SomeMod - Main.ba2"], Collect("SomeMod.esp"));
    }

    [Fact]
    public void Matching_ignores_case()
    {
        WriteArchive("somemod - main.ba2");

        Assert.Single(Collect("SomeMod.esp"));
    }

    // A later plugin's assets win in game, so its archives have to be indexed last.
    [Fact]
    public void Archives_come_back_in_load_order()
    {
        WriteArchive("Second - Main.ba2");
        WriteArchive("First - Main.ba2");

        Assert.Equal(
            ["First - Main.ba2", "Second - Main.ba2"],
            Collect("First.esp", "Second.esp"));
    }

    [Fact]
    public void A_plugin_named_after_part_of_an_archive_does_not_match()
    {
        // "Some" must not claim "SomeMod - Main.ba2".
        WriteArchive("SomeMod - Main.ba2");

        Assert.Empty(Collect("Some.esp"));
    }

    [Fact]
    public void Non_archive_files_are_ignored()
    {
        WriteArchive("SomeMod - Main.bsa");
        WriteArchive("SomeMod.esp");

        Assert.Empty(Collect("SomeMod.esp"));
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
