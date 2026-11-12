using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Thread-safe delegating repository that allows hot-swapping the active profile's SQLite repository at runtime.
/// </summary>
public sealed class SwitchableRepository : ITypeForgeRepository
{
    private ITypeForgeRepository _inner;

    /// <summary>Initializes the wrapper with an initial repository instance.</summary>
    public SwitchableRepository(ITypeForgeRepository initialRepository)
    {
        _inner = initialRepository ?? throw new ArgumentNullException(nameof(initialRepository));
    }

    /// <summary>Replaces the underlying repository instance used for subsequent operations.</summary>
    public void SetRepository(ITypeForgeRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        Volatile.Write(ref _inner, repository);
    }

    private ITypeForgeRepository Inner => Volatile.Read(ref _inner);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => Inner.InitializeAsync(ct);

    /// <inheritdoc />
    public Task<int> GetSchemaVersionAsync(CancellationToken ct = default) => Inner.GetSchemaVersionAsync(ct);

    /// <inheritdoc />
    public Task SeedCurriculumIfEmptyAsync(IReadOnlyList<CourseLevel> levels, CancellationToken ct = default) => Inner.SeedCurriculumIfEmptyAsync(levels, ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadLessonProgressAsync(CancellationToken ct = default) => Inner.LoadLessonProgressAsync(ct);

    /// <inheritdoc />
    public Task SaveLessonProgressAsync(LessonProgressRecord progress, CancellationToken ct = default) => Inner.SaveLessonProgressAsync(progress, ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadExerciseProgressAsync(CancellationToken ct = default) => Inner.LoadExerciseProgressAsync(ct);

    /// <inheritdoc />
    public Task SaveExerciseProgressAsync(LessonProgressRecord progress, CancellationToken ct = default) => Inner.SaveExerciseProgressAsync(progress, ct);

    /// <inheritdoc />
    public Task RecordPracticeSessionAsync(SessionResult result, CancellationToken ct = default) => Inner.RecordPracticeSessionAsync(result, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionResult>> GetRecentSessionsAsync(int limit = 50, CancellationToken ct = default) => Inner.GetRecentSessionsAsync(limit, ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<char, KeyMetric>> GetKeyStatisticsAsync(CancellationToken ct = default) => Inner.GetKeyStatisticsAsync(ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<AdaptiveKeyState>> GetAdaptiveProgressAsync(CancellationToken ct = default) => Inner.GetAdaptiveProgressAsync(ct);

    /// <inheritdoc />
    public Task SaveAdaptiveProgressAsync(IEnumerable<AdaptiveKeyState> states, CancellationToken ct = default) => Inner.SaveAdaptiveProgressAsync(states, ct);

    /// <inheritdoc />
    public Task<UserSettings> GetSettingsAsync(CancellationToken ct = default) => Inner.GetSettingsAsync(ct);

    /// <inheritdoc />
    public Task SaveSettingsAsync(UserSettings settings, CancellationToken ct = default) => Inner.SaveSettingsAsync(settings, ct);

    /// <inheritdoc />
    public Task<TimeSpan> GetTotalDurationForDateAsync(DateOnly date, CancellationToken ct = default) => Inner.GetTotalDurationForDateAsync(date, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<DateOnly>> GetPracticeDatesAsync(CancellationToken ct = default) => Inner.GetPracticeDatesAsync(ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, BigramMetric>> GetBigramStatisticsAsync(CancellationToken ct = default) => Inner.GetBigramStatisticsAsync(ct);

    /// <inheritdoc />
    public Task SaveBigramStatisticsAsync(IEnumerable<BigramMetric> bigrams, CancellationToken ct = default) => Inner.SaveBigramStatisticsAsync(bigrams, ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<char, KeyReviewScheduleRecord>> GetKeyReviewSchedulesAsync(CancellationToken ct = default) => Inner.GetKeyReviewSchedulesAsync(ct);

    /// <inheritdoc />
    public Task SaveKeyReviewSchedulesAsync(IEnumerable<KeyReviewScheduleRecord> schedules, CancellationToken ct = default) => Inner.SaveKeyReviewSchedulesAsync(schedules, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<PlacementResultRecord>> GetPlacementResultsAsync(CancellationToken ct = default) => Inner.GetPlacementResultsAsync(ct);

    /// <inheritdoc />
    public Task SavePlacementResultAsync(PlacementResultRecord placement, CancellationToken ct = default) => Inner.SavePlacementResultAsync(placement, ct);

    /// <inheritdoc />
    public Task ResetProgressAsync(CancellationToken ct = default) => Inner.ResetProgressAsync(ct);
}
