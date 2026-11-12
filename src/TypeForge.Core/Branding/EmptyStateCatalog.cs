namespace TypeForge.Core.Branding;

/// <summary>
/// Identifies a canonical empty-state or non-modal illustration scenario in TypeForge.
/// </summary>
public enum EmptyStateKind
{
    /// <summary>No practice or typing sessions have been recorded yet.</summary>
    NoStats,

    /// <summary>No weak keys meet the minimum sample guard yet.</summary>
    NoWeakKeys,

    /// <summary>No timed typing test sessions have been completed yet.</summary>
    NoTestHistory,

    /// <summary>Fatigue detector triggered a short break suggestion.</summary>
    BreakReminder,
}

/// <summary>
/// Immutable empty-state copy and illustration stem metadata.
/// </summary>
/// <param name="Kind">Empty-state scenario identifier.</param>
/// <param name="AssetStem">File stem under Assets/Illustrations (no scale qualifier, no extension).</param>
/// <param name="Title">Short calm heading without marketing tone.</param>
/// <param name="Message">One-sentence explanation.</param>
/// <param name="ActionLabel">Optional primary action button label.</param>
public sealed record EmptyStateContent(
    EmptyStateKind Kind,
    string AssetStem,
    string Title,
    string Message,
    string? ActionLabel);

/// <summary>
/// Pure catalog mapping <see cref="EmptyStateKind"/> values to illustration asset stems and UI copy.
/// </summary>
public static class EmptyStateCatalog
{
    private static readonly IReadOnlyDictionary<EmptyStateKind, EmptyStateContent> Items =
        new Dictionary<EmptyStateKind, EmptyStateContent>
        {
            [EmptyStateKind.NoStats] = new(
                EmptyStateKind.NoStats, "empty-stats",
                "No sessions yet",
                "Finish a practice session to see your speed and accuracy trend.",
                "Start practice"),
            [EmptyStateKind.NoWeakKeys] = new(
                EmptyStateKind.NoWeakKeys, "empty-weak-keys",
                "No weak keys found",
                "Weak keys show up after about 20 keystrokes per key.",
                null),
            [EmptyStateKind.NoTestHistory] = new(
                EmptyStateKind.NoTestHistory, "empty-test-history",
                "No tests yet",
                "Complete a timed test to see your results here.",
                "Start a test"),
            [EmptyStateKind.BreakReminder] = new(
                EmptyStateKind.BreakReminder, "break-reminder",
                "Time for a short break",
                "Your accuracy has dropped. A few minutes away from the keyboard helps.",
                "Keep typing"),
        };

    /// <summary>Gets all registered empty-state entries.</summary>
    public static IReadOnlyCollection<EmptyStateContent> All => Items.Values.ToArray();

    /// <summary>
    /// Returns the registered <see cref="EmptyStateContent"/> for <paramref name="kind"/>.
    /// </summary>
    public static EmptyStateContent Get(EmptyStateKind kind) =>
        Items.TryGetValue(kind, out EmptyStateContent? content)
            ? content
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No empty-state content registered.");
}
