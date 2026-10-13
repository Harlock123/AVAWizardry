using Wizardry.Audio;

namespace Wizardry.Core.Tests;

public sealed class AudioBufferTests
{
    [Theory]
    [InlineData("auto", false, 0)]
    [InlineData("auto", true, 4096)]
    [InlineData("small", true, 0)]
    [InlineData("medium", true, 1024)]
    [InlineData("large", true, 2048)]
    [InlineData("xlarge", false, 4096)]
    [InlineData("nonsense", true, 4096)]
    public void Each_choice_sets_its_period(string choice, bool vm, int frames) =>
        Assert.Equal(frames, AudioBuffer.PeriodFrames(choice, vm));

    [Fact]
    public void Extra_large_is_offered() =>
        Assert.Contains(AudioBuffer.Choices, c => c.Id == AudioBuffer.ExtraLarge);
}
