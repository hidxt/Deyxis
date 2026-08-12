using Deyxis.Providers.Lyrics;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class LrcParserTests
{
    [Fact]
    public void Parses_each_timestamp_tag_into_a_timed_line()
    {
        var timeline = LrcParser.Parse("[00:01.25][00:02.50]Hello");

        Assert.Collection(
            timeline.Lines,
            line =>
            {
                Assert.Equal(TimeSpan.FromMilliseconds(1250), line.Timestamp);
                Assert.Equal("Hello", line.Text);
            },
            line =>
            {
                Assert.Equal(TimeSpan.FromMilliseconds(2500), line.Timestamp);
                Assert.Equal("Hello", line.Text);
            });
    }

    [Fact]
    public void Skips_metadata_and_malformed_timestamp_tags()
    {
        var timeline = LrcParser.Parse("[ar:Example Artist]\n[00:03.5]Valid\n[00:61.00]Invalid seconds\n[01:02]Whole seconds\n[bad]Ignored");

        Assert.Collection(
            timeline.Lines,
            line =>
            {
                Assert.Equal(TimeSpan.FromSeconds(3.5), line.Timestamp);
                Assert.Equal("Valid", line.Text);
            },
            line =>
            {
                Assert.Equal(TimeSpan.FromSeconds(62), line.Timestamp);
                Assert.Equal("Whole seconds", line.Text);
            });
    }

    [Fact]
    public void Sorts_equal_timestamps_stably()
    {
        var timeline = LrcParser.Parse("[00:03.00]Third\n[00:01.00]First\n[00:01.00]Also first");

        Assert.Collection(
            timeline.Lines,
            line => Assert.Equal("First", line.Text),
            line => Assert.Equal("Also first", line.Text),
            line => Assert.Equal("Third", line.Text));
    }

    [Fact]
    public void Selects_the_latest_line_at_or_before_the_requested_position()
    {
        var timeline = LrcParser.Parse("[00:01.00]One\n[00:03.00]Three");

        Assert.Null(timeline.GetLineAt(TimeSpan.Zero));
        Assert.Equal("One", timeline.GetLineAt(TimeSpan.FromSeconds(1))?.Text);
        Assert.Equal("One", timeline.GetLineAt(TimeSpan.FromSeconds(2.99))?.Text);
        Assert.Equal("Three", timeline.GetLineAt(TimeSpan.FromSeconds(3))?.Text);
    }
}
