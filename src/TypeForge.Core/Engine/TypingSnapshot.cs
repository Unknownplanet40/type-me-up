namespace TypeForge.Core.Engine;

/// <summary>
/// Represents the evaluation state of a single character position in a typing prompt.
/// </summary>
public enum TypingCharacterState
{
    /// <summary>Character has not been reached yet.</summary>
    Pending = 0,

    /// <summary>Character is at the active cursor position.</summary>
    Current = 1,

    /// <summary>Character was typed accurately.</summary>
    Correct = 2,

    /// <summary>Character was typed with a mistake.</summary>
    Incorrect = 3
}

/// <summary>
/// Immutable point-in-time view of an active or completed typing session.
/// </summary>
public sealed record TypingSnapshot(
    string TargetText,
    string TypedText,
    int CursorIndex,
    int TotalKeystrokes,
    int CorrectKeystrokes,
    int Mistakes,
    int UncorrectedErrors,
    TimeSpan Elapsed,
    double LiveGrossWpm,
    double LiveNetWpm,
    double LiveAccuracyPercentage,
    bool IsCompleted,
    IReadOnlyList<TypingCharacterState> CharacterStates);
