using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.Tests;

public sealed class TypingEngineTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    [Fact]
    public void Constructor_EmptyText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TypingEngine(string.Empty));
    }

    [Fact]
    public void FreeFlow_AllCorrectKeystrokes_CompletesSessionWithFullAccuracy()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("fj fj", stopOnError: false, timeProvider: clock);

        foreach (char c in "fj fj")
        {
            Assert.True(engine.ProcessKeystroke(c));
            clock.Advance(TimeSpan.FromMilliseconds(200));
        }

        // Total elapsed between first keystroke and completion = 4 * 200ms (Wait: advance 1s before final read or 250ms per keystroke = 1000ms)
        var snapshot = engine.GetSnapshot();
        Assert.True(snapshot.IsCompleted);
        Assert.Equal(5, snapshot.CursorIndex);
        Assert.Equal(5, snapshot.TotalKeystrokes);
        Assert.Equal(5, snapshot.CorrectKeystrokes);
        Assert.Equal(0, snapshot.Mistakes);
        Assert.Equal(0, snapshot.UncorrectedErrors);
        Assert.Equal(100.0, snapshot.LiveAccuracyPercentage);
    }

    [Fact]
    public void FreeFlow_MistakeAndBackspace_FixesUncorrectedErrorWithoutCountingBackspaceAsCorrect()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("fj", stopOnError: false, timeProvider: clock);

        engine.ProcessKeystroke('f');
        clock.Advance(TimeSpan.FromMilliseconds(500));
        engine.ProcessKeystroke('x'); // mistake on 'j'
        clock.Advance(TimeSpan.FromMilliseconds(500));

        var afterMistake = engine.GetSnapshot();
        Assert.True(afterMistake.IsCompleted);
        Assert.Equal(1, afterMistake.Mistakes);
        Assert.Equal(1, afterMistake.UncorrectedErrors);
        Assert.Equal(1, afterMistake.CorrectKeystrokes);
        Assert.Equal(2, afterMistake.TotalKeystrokes);

        // Wait: test backspace BEFORE completion on longer string
        var engine2 = new TypingEngine("fjk", stopOnError: false, timeProvider: clock);
        engine2.ProcessKeystroke('f');
        clock.Advance(TimeSpan.FromMilliseconds(300));
        engine2.ProcessKeystroke('x'); // mistake on 'j'
        clock.Advance(TimeSpan.FromMilliseconds(300));

        Assert.True(engine2.ProcessBackspace());
        var afterBackspace = engine2.GetSnapshot();
        Assert.Equal(1, afterBackspace.CursorIndex);
        Assert.Equal(1, afterBackspace.CorrectKeystrokes);
        Assert.Equal(2, afterBackspace.TotalKeystrokes);
        Assert.Equal(1, afterBackspace.Mistakes);
        Assert.Equal(0, afterBackspace.UncorrectedErrors);
        Assert.Equal(TypingCharacterState.Current, afterBackspace.CharacterStates[1]);

        clock.Advance(TimeSpan.FromMilliseconds(400));
        engine2.ProcessKeystroke('j');
        clock.Advance(TimeSpan.FromMilliseconds(400));
        engine2.ProcessKeystroke('k');

        var done = engine2.GetSnapshot();
        Assert.True(done.IsCompleted);
        Assert.Equal(4, done.TotalKeystrokes);
        Assert.Equal(3, done.CorrectKeystrokes);
        Assert.Equal(1, done.Mistakes);
        Assert.Equal(0, done.UncorrectedErrors);
        Assert.Equal(75.0, done.LiveAccuracyPercentage);
    }

    [Fact]
    public void StrictStopOnError_HaltsCursorOnMistakeUntilCorrectKeyTyped()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("fj", stopOnError: true, timeProvider: clock);

        engine.ProcessKeystroke('x'); // wrong for 'f'
        var halted = engine.GetSnapshot();
        Assert.Equal(0, halted.CursorIndex);
        Assert.Equal(1, halted.Mistakes);
        Assert.Equal(1, halted.TotalKeystrokes);
        Assert.Equal(0, halted.CorrectKeystrokes);

        clock.Advance(TimeSpan.FromSeconds(1));
        engine.ProcessKeystroke('f'); // recovers at cursor 0
        var advanced = engine.GetSnapshot();
        Assert.Equal(1, advanced.CursorIndex);
        Assert.Equal(1, advanced.Mistakes);
        Assert.Equal(1, advanced.CorrectKeystrokes);
        Assert.Equal(2, advanced.TotalKeystrokes);
    }

    [Fact]
    public void BuildResult_TracksPerKeyLatencyAndErrorRate()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("fjfj", stopOnError: false, timeProvider: clock);

        engine.ProcessKeystroke('f'); // latency sample N/A on very first keystroke (or 0)
        clock.Advance(TimeSpan.FromMilliseconds(400));
        engine.ProcessKeystroke('k'); // mistake on 'j' (400ms)
        clock.Advance(TimeSpan.FromMilliseconds(300));
        engine.ProcessKeystroke('f'); // correct 'f' (300ms)
        clock.Advance(TimeSpan.FromMilliseconds(500));
        engine.ProcessKeystroke('j'); // correct 'j' (500ms)

        SessionResult result = engine.BuildResult();
        result.Validate();

        Assert.Equal(4, result.TotalKeystrokes);
        Assert.Equal(3, result.CorrectCharacters);
        Assert.Equal(1, result.Mistakes);
        Assert.True(result.KeyBreakdown.TryGetValue('F', out var fMetric));
        Assert.True(result.KeyBreakdown.TryGetValue('J', out var jMetric));
        Assert.Equal(2, fMetric!.TotalKeystrokes);
        Assert.Equal(2, fMetric.CorrectKeystrokes);
        Assert.Equal(0, fMetric.IncorrectKeystrokes);
        Assert.Equal(2, jMetric!.TotalKeystrokes);
        Assert.Equal(1, jMetric.CorrectKeystrokes);
        Assert.Equal(1, jMetric.IncorrectKeystrokes);
        Assert.Equal(50.0, jMetric.ErrorRatePercentage);
        Assert.Equal(450.0, jMetric.AverageLatencyMs);
    }

    [Fact]
    public void ProcessBackspace_AtStartOrAfterCompletion_ReturnsFalse()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("a", stopOnError: false, timeProvider: clock);

        Assert.False(engine.ProcessBackspace());
        engine.ProcessKeystroke('a');
        Assert.True(engine.GetSnapshot().IsCompleted);
        Assert.False(engine.ProcessBackspace());
    }

    [Fact]
    public void Reset_ClearsSessionStateBackToZero()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("abc", stopOnError: false, timeProvider: clock);

        engine.ProcessKeystroke('a');
        clock.Advance(TimeSpan.FromSeconds(1));
        engine.Reset();

        var snapshot = engine.GetSnapshot();
        Assert.Equal(0, snapshot.CursorIndex);
        Assert.Equal(0, snapshot.TotalKeystrokes);
        Assert.Equal(0, snapshot.CorrectKeystrokes);
        Assert.Equal(0, snapshot.Mistakes);
        Assert.False(snapshot.IsCompleted);
        Assert.Equal(100.0, snapshot.LiveAccuracyPercentage);
    }

    [Fact]
    public void ProcessKeystroke_ControlCharsAndPostCompletion_AreIgnored()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("ab", stopOnError: false, timeProvider: clock);

        Assert.False(engine.ProcessKeystroke('\0'));
        Assert.False(engine.ProcessKeystroke('\r'));
        Assert.True(engine.ProcessKeystroke('a'));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(engine.ProcessKeystroke('b'));
        Assert.True(engine.GetSnapshot().IsCompleted);
        Assert.False(engine.ProcessKeystroke('c'));
        Assert.Equal(2, engine.GetSnapshot().TotalKeystrokes);
    }

    [Fact]
    public void LiveAndResultWpm_AccurateAcrossMultiSecondSession()
    {
        var clock = new FakeTimeProvider();
        // 25 chars = 5 words. Over 12 seconds (0.2 min) => 25.0 WPM
        string text = "abcde fghij klmno pqrst u";
        var engine = new TypingEngine(text, stopOnError: false, timeProvider: clock);

        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0)
            {
                clock.Advance(TimeSpan.FromMilliseconds(500)); // 24 * 500ms = 12 seconds
            }

            Assert.True(engine.ProcessKeystroke(text[i]));
        }

        SessionResult result = engine.BuildResult();
        Assert.Equal(TimeSpan.FromSeconds(12), result.Duration);
        Assert.Equal(25.0, result.GrossWpm);
        Assert.Equal(25.0, result.NetWpm);
        Assert.Equal(100.0, result.AccuracyPercentage);
    }
}
