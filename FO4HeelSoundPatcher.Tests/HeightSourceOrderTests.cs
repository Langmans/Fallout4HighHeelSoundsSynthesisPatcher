using FO4HeelSoundPatcher.Detection;

namespace FO4HeelSoundPatcher.Tests;

public class HeightSourceOrderTests
{
    [Fact]
    public void The_default_order_matches_how_HHS_resolves_its_own_sources()
    {
        // json beats the mesh beats the txt file; HO3 last because it is armor-wide rather than
        // per-mesh. Changing this changes which height wins when a mod records more than one.
        Assert.Equal(
            [HeightSource.HhsJson, HeightSource.HhsNif, HeightSource.HhsTxt, HeightSource.Ho3Script],
            HeightSourceOrder.Default);
    }

    [Fact]
    public void The_default_order_covers_every_source()
    {
        Assert.Equal(Enum.GetValues<HeightSource>().Length, HeightSourceOrder.Default.Count);
    }

    [Fact]
    public void The_custom_list_is_ignored_while_the_default_is_selected()
    {
        var resolved = HeightSourceOrder.Resolve(
            useDefault: true, [HeightSource.Ho3Script], out var explanation);

        Assert.Equal(HeightSourceOrder.Default, resolved);
        Assert.Contains("default", explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_custom_order_is_used_as_given()
    {
        var custom = new[] { HeightSource.Ho3Script, HeightSource.HhsTxt };

        var resolved = HeightSourceOrder.Resolve(useDefault: false, custom, out _);

        Assert.Equal(custom, resolved);
    }

    // Omitting a source is how you turn it off, so it must not be quietly added back.
    [Fact]
    public void Omitted_sources_stay_omitted_and_are_named_in_the_explanation()
    {
        var resolved = HeightSourceOrder.Resolve(
            useDefault: false, [HeightSource.HhsTxt], out var explanation);

        Assert.Equal([HeightSource.HhsTxt], resolved);
        Assert.Contains("HhsJson", explanation, StringComparison.Ordinal);
        Assert.Contains("HhsNif", explanation, StringComparison.Ordinal);
        Assert.Contains("Ho3Script", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicates_are_dropped_keeping_the_first_position()
    {
        var resolved = HeightSourceOrder.Resolve(
            useDefault: false,
            [HeightSource.HhsTxt, HeightSource.HhsJson, HeightSource.HhsTxt],
            out _);

        Assert.Equal([HeightSource.HhsTxt, HeightSource.HhsJson], resolved);
    }

    // An empty list almost certainly means the user cleared it by accident. Detecting nothing at
    // all would be a silently useless run, so fall back rather than obey.
    [Fact]
    public void An_empty_custom_order_falls_back_to_the_default()
    {
        var resolved = HeightSourceOrder.Resolve(useDefault: false, [], out var explanation);

        Assert.Equal(HeightSourceOrder.Default, resolved);
        Assert.Contains("empty", explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HeightSource.HhsJson, true)]
    [InlineData(HeightSource.HhsNif, true)]
    [InlineData(HeightSource.HhsTxt, true)]
    [InlineData(HeightSource.Ho3Script, false)]
    public void Only_the_HO3_script_is_not_a_mesh_source(HeightSource source, bool expected)
    {
        Assert.Equal(expected, source.IsMeshSource());
    }


    [Fact]
    public void The_HO3_script_outranks_the_mesh_sources_only_when_it_leads()
    {
        Assert.True(HeightSourceOrder.Ho3OutranksMeshSources(
            [HeightSource.Ho3Script, HeightSource.HhsTxt]));

        Assert.False(HeightSourceOrder.Ho3OutranksMeshSources(
            [HeightSource.HhsTxt, HeightSource.Ho3Script]));
    }

    [Fact]
    public void The_default_order_lets_mesh_data_win_over_the_HO3_script()
    {
        Assert.False(HeightSourceOrder.Ho3OutranksMeshSources(HeightSourceOrder.Default));
    }

    [Fact]
    public void An_order_without_the_HO3_script_does_not_let_it_outrank_anything()
    {
        Assert.False(HeightSourceOrder.Ho3OutranksMeshSources([HeightSource.HhsTxt]));
        Assert.False(HeightSourceOrder.Ho3OutranksMeshSources([]));
    }

    [Fact]
    public void The_shipped_default_settings_produce_the_default_order()
    {
        var settings = new DetectionSettings();

        var resolved = HeightSourceOrder.Resolve(
            settings.UseDefaultSourceOrder, settings.SourcePriority, out _);

        Assert.Equal(HeightSourceOrder.Default, resolved);
        Assert.Equal(HeightSourceOrder.Default, settings.SourcePriority);
    }
}
