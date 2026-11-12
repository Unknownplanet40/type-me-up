using System.Globalization;
using Microsoft.Data.Sqlite;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Persisted progression record for a single structured curriculum lesson.
/// </summary>
public sealed record LessonProgressRecord(
    string LessonId,
    bool IsUnlocked,
    bool IsCompleted,
    double BestWpm,
    double BestAccuracy,
    int Stars,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Defines local persistence operations for sessions, key metrics, curriculum progress, adaptive locks, and settings.
/// </summary>
public interface ITypeForgeRepository
{
    /// <summary>Creates the database directory, opens SQLite, and executes pending schema migrations.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Returns the current SQLite <c>PRAGMA user_version</c> schema version.</summary>
    Task<int> GetSchemaVersionAsync(CancellationToken ct = default);

    /// <summary>Seeds embedded course lessons and initial unlock rows when the Lessons table is empty.</summary>
    Task SeedCurriculumIfEmptyAsync(IReadOnlyList<CourseLevel> levels, CancellationToken ct = default);

    /// <summary>Loads persisted lesson progression keyed by lesson ID.</summary>
    Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadLessonProgressAsync(CancellationToken ct = default);

    /// <summary>Upserts progression state for a single course lesson.</summary>
    Task SaveLessonProgressAsync(LessonProgressRecord progress, CancellationToken ct = default);

    /// <summary>Loads persisted multi-track exercise progression keyed by exercise ID.</summary>
    Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadExerciseProgressAsync(CancellationToken ct = default);

    /// <summary>Upserts progression state for a single multi-track course exercise.</summary>
    Task SaveExerciseProgressAsync(LessonProgressRecord progress, CancellationToken ct = default);

    /// <summary>Atomically records a completed session and updates cumulative key statistics without altering course or adaptive locks.</summary>
    Task RecordPracticeSessionAsync(SessionResult result, CancellationToken ct = default);

    /// <summary>Loads recent completed sessions ordered newest first.</summary>
    Task<IReadOnlyList<SessionResult>> GetRecentSessionsAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>Loads aggregated per-key accuracy and latency metrics keyed by character.</summary>
    Task<IReadOnlyDictionary<char, KeyMetric>> GetKeyStatisticsAsync(CancellationToken ct = default);

    /// <summary>Loads persisted adaptive letter unlock states ordered by stage and character.</summary>
    Task<IReadOnlyList<AdaptiveKeyState>> GetAdaptiveProgressAsync(CancellationToken ct = default);

    /// <summary>Persists all 26 adaptive letter unlock states inside a transaction.</summary>
    Task SaveAdaptiveProgressAsync(IEnumerable<AdaptiveKeyState> states, CancellationToken ct = default);

    /// <summary>Loads persisted user settings, or returns defaults when none are stored.</summary>
    Task<UserSettings> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Saves validated user settings as key-value pairs.</summary>
    Task SaveSettingsAsync(UserSettings settings, CancellationToken ct = default);

    /// <summary>Returns total active practice duration recorded on the specified UTC calendar date.</summary>
    Task<TimeSpan> GetTotalDurationForDateAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>Returns distinct UTC dates on which at least one practice session occurred, newest first.</summary>
    Task<IReadOnlyList<DateOnly>> GetPracticeDatesAsync(CancellationToken ct = default);

    /// <summary>Loads aggregated bigram transition metrics keyed by 2-character bigram.</summary>
    Task<IReadOnlyDictionary<string, BigramMetric>> GetBigramStatisticsAsync(CancellationToken ct = default);

    /// <summary>Upserts bigram transition metrics inside a transaction.</summary>
    Task SaveBigramStatisticsAsync(IEnumerable<BigramMetric> bigrams, CancellationToken ct = default);

    /// <summary>Loads persisted spaced-repetition review schedules keyed by character.</summary>
    Task<IReadOnlyDictionary<char, KeyReviewScheduleRecord>> GetKeyReviewSchedulesAsync(CancellationToken ct = default);

    /// <summary>Upserts spaced-repetition review schedules inside a transaction.</summary>
    Task SaveKeyReviewSchedulesAsync(IEnumerable<KeyReviewScheduleRecord> schedules, CancellationToken ct = default);

    /// <summary>Loads recorded placement test results ordered newest first.</summary>
    Task<IReadOnlyList<PlacementResultRecord>> GetPlacementResultsAsync(CancellationToken ct = default);

    /// <summary>Persists a completed placement test evaluation.</summary>
    Task SavePlacementResultAsync(PlacementResultRecord placement, CancellationToken ct = default);

    /// <summary>Clears all typing sessions, statistics, course progress, adaptive locks, placement results, and review schedules while preserving user settings.</summary>
    Task ResetProgressAsync(CancellationToken ct = default);
}

/// <summary>
/// Parameterized SQLite persistence implementation stored under local application data.
/// </summary>
public sealed class SqliteTypeForgeRepository : ITypeForgeRepository
{
    private const int CurrentSchemaVersion = 3;
    private readonly string _databasePath;
    private readonly string _connectionString;

    /// <summary>
    /// Optional hook invoked during schema migration steps before commit (used by unit tests to verify rollback resilience).
    /// </summary>
    public Action<int>? MigrationStepFaultHook { get; set; }

    /// <summary>
    /// Initializes a repository targeting the specified SQLite file path.
    /// </summary>
    public SqliteTypeForgeRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be empty.", nameof(databasePath));
        }

        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
    }

    /// <summary>
    /// Returns the default per-user offline database path at <c>%LOCALAPPDATA%\TypeForge\typeforge.db</c>.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TypeForge", "typeforge.db");
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        int version = await ReadUserVersionAsync(connection, transaction, ct).ConfigureAwait(false);
        if (version < 1)
        {
            const string ddl = """
                CREATE TABLE IF NOT EXISTS Lessons (
                    LessonId TEXT PRIMARY KEY,
                    LevelId INTEGER NOT NULL,
                    OrderIndex INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    TargetCharacters TEXT NOT NULL,
                    PracticeText TEXT NOT NULL,
                    Difficulty TEXT NOT NULL,
                    MinimumNetWpm REAL NOT NULL,
                    MinimumAccuracy REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS LessonProgress (
                    LessonId TEXT PRIMARY KEY,
                    IsUnlocked INTEGER NOT NULL DEFAULT 0,
                    IsCompleted INTEGER NOT NULL DEFAULT 0,
                    BestWpm REAL NOT NULL DEFAULT 0,
                    BestAccuracy REAL NOT NULL DEFAULT 0,
                    Stars INTEGER NOT NULL DEFAULT 0,
                    CompletedAt TEXT NULL,
                    FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS PracticeSessions (
                    SessionId TEXT PRIMARY KEY,
                    SessionType INTEGER NOT NULL,
                    LessonId TEXT NULL,
                    StartedAt TEXT NOT NULL,
                    DurationSeconds REAL NOT NULL,
                    NetWpm REAL NOT NULL,
                    GrossWpm REAL NOT NULL,
                    AccuracyPercentage REAL NOT NULL,
                    CorrectCharacters INTEGER NOT NULL,
                    TotalKeystrokes INTEGER NOT NULL,
                    Mistakes INTEGER NOT NULL,
                    UncorrectedErrors INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS KeyStatistics (
                    Character TEXT PRIMARY KEY,
                    TotalKeystrokes INTEGER NOT NULL DEFAULT 0,
                    CorrectKeystrokes INTEGER NOT NULL DEFAULT 0,
                    IncorrectKeystrokes INTEGER NOT NULL DEFAULT 0,
                    AverageLatencyMs REAL NOT NULL DEFAULT 0,
                    LastUpdated TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS AdaptiveProgress (
                    Character TEXT PRIMARY KEY,
                    IsUnlocked INTEGER NOT NULL DEFAULT 0,
                    StageIndex INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PracticeSessions_StartedAt ON PracticeSessions(StartedAt);
                CREATE INDEX IF NOT EXISTS IX_Lessons_LevelOrder ON Lessons(LevelId, OrderIndex);

                PRAGMA user_version = 1;
                """;

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        int afterV1 = await ReadUserVersionAsync(connection, null, ct).ConfigureAwait(false);
        if (afterV1 < 2)
        {
            await using SqliteTransaction v2Tx = connection.BeginTransaction();
            try
            {
                const string v2Ddl = """
                    CREATE TABLE IF NOT EXISTS CourseExerciseProgress (
                        ExerciseId TEXT PRIMARY KEY,
                        IsUnlocked INTEGER NOT NULL DEFAULT 0,
                        IsCompleted INTEGER NOT NULL DEFAULT 0,
                        BestWpm REAL NOT NULL DEFAULT 0,
                        BestAccuracy REAL NOT NULL DEFAULT 0,
                        Stars INTEGER NOT NULL DEFAULT 0,
                        CompletedAt TEXT NULL
                    );

                    PRAGMA user_version = 2;
                    """;

                await using var v2Cmd = connection.CreateCommand();
                v2Cmd.Transaction = v2Tx;
                v2Cmd.CommandText = v2Ddl;
                await v2Cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                MigrationStepFaultHook?.Invoke(2);
                await v2Tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await v2Tx.RollbackAsync(ct).ConfigureAwait(false);
                return;
            }
        }

        int postBaseVersion = await ReadUserVersionAsync(connection, null, ct).ConfigureAwait(false);
        if (postBaseVersion < 3)
        {
            await using SqliteTransaction v3Tx = connection.BeginTransaction();
            try
            {
                const string v3Ddl = """
                    CREATE TABLE IF NOT EXISTS BigramStatistics (
                        Bigram TEXT PRIMARY KEY,
                        TotalCount INTEGER NOT NULL DEFAULT 0,
                        ErrorCount INTEGER NOT NULL DEFAULT 0,
                        AverageLatencyMs REAL NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS KeyReviewSchedule (
                        Character TEXT PRIMARY KEY,
                        LastReviewedAt TEXT NULL,
                        IntervalDays REAL NOT NULL DEFAULT 1.0,
                        EaseFactor REAL NOT NULL DEFAULT 2.5,
                        DueAt TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS PlacementResults (
                        PlacementId TEXT PRIMARY KEY,
                        TakenAt TEXT NOT NULL,
                        NetWpm REAL NOT NULL,
                        Accuracy REAL NOT NULL,
                        RecommendedTrack TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_KeyReviewSchedule_DueAt ON KeyReviewSchedule(DueAt);
                    CREATE INDEX IF NOT EXISTS IX_BigramStatistics_AverageLatencyMs ON BigramStatistics(AverageLatencyMs);

                    PRAGMA user_version = 3;
                    """;

                await using var v3Cmd = connection.CreateCommand();
                v3Cmd.Transaction = v3Tx;
                v3Cmd.CommandText = v3Ddl;
                await v3Cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                MigrationStepFaultHook?.Invoke(3);
                await v3Tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await v3Tx.RollbackAsync(ct).ConfigureAwait(false);
                if (postBaseVersion < 1)
                {
                    throw;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        return await ReadUserVersionAsync(connection, null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SeedCurriculumIfEmptyAsync(IReadOnlyList<CourseLevel> levels, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(levels);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        const string insertLessonSql = """
            INSERT OR IGNORE INTO Lessons (
                LessonId, LevelId, OrderIndex, Title, Description,
                TargetCharacters, PracticeText, Difficulty, MinimumNetWpm, MinimumAccuracy
            ) VALUES (
                @LessonId, @LevelId, @OrderIndex, @Title, @Description,
                @TargetCharacters, @PracticeText, @Difficulty, @MinimumNetWpm, @MinimumAccuracy
            );
            """;

        const string insertProgressSql = """
            INSERT OR IGNORE INTO LessonProgress (
                LessonId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt
            ) VALUES (
                @LessonId, @IsUnlocked, 0, 0, 0, 0, NULL
            );
            """;

        bool firstLessonSeen = false;
        foreach (CourseLevel level in levels.OrderBy(l => l.LevelId))
        {
            foreach (LessonModel lesson in level.Lessons.OrderBy(l => l.OrderIndex))
            {
                bool unlockInitially = !firstLessonSeen;
                firstLessonSeen = true;

                await using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = insertLessonSql;
                    cmd.Parameters.AddWithValue("@LessonId", lesson.LessonId);
                    cmd.Parameters.AddWithValue("@LevelId", lesson.LevelId);
                    cmd.Parameters.AddWithValue("@OrderIndex", lesson.OrderIndex);
                    cmd.Parameters.AddWithValue("@Title", lesson.Title);
                    cmd.Parameters.AddWithValue("@Description", lesson.Description);
                    cmd.Parameters.AddWithValue("@TargetCharacters", lesson.TargetCharacters);
                    cmd.Parameters.AddWithValue("@PracticeText", lesson.PracticeText);
                    cmd.Parameters.AddWithValue("@Difficulty", lesson.Difficulty);
                    cmd.Parameters.AddWithValue("@MinimumNetWpm", lesson.MinimumNetWpm);
                    cmd.Parameters.AddWithValue("@MinimumAccuracy", lesson.MinimumAccuracy);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                await using (var progressCmd = connection.CreateCommand())
                {
                    progressCmd.Transaction = transaction;
                    progressCmd.CommandText = insertProgressSql;
                    progressCmd.Parameters.AddWithValue("@LessonId", lesson.LessonId);
                    progressCmd.Parameters.AddWithValue("@IsUnlocked", unlockInitially ? 1 : 0);
                    await progressCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadLessonProgressAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT LessonId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt
            FROM LessonProgress;
            """;

        var result = new Dictionary<string, LessonProgressRecord>(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string lessonId = reader.GetString(0);
            DateTimeOffset? completedAt = reader.IsDBNull(6)
                ? null
                : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            result[lessonId] = new LessonProgressRecord(
                LessonId: lessonId,
                IsUnlocked: reader.GetInt64(1) != 0,
                IsCompleted: reader.GetInt64(2) != 0,
                BestWpm: reader.GetDouble(3),
                BestAccuracy: reader.GetDouble(4),
                Stars: reader.GetInt32(5),
                CompletedAt: completedAt);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveLessonProgressAsync(LessonProgressRecord progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LessonProgress (
                LessonId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt
            ) VALUES (
                @LessonId, @IsUnlocked, @IsCompleted, @BestWpm, @BestAccuracy, @Stars, @CompletedAt
            )
            ON CONFLICT(LessonId) DO UPDATE SET
                IsUnlocked = excluded.IsUnlocked,
                IsCompleted = excluded.IsCompleted,
                BestWpm = excluded.BestWpm,
                BestAccuracy = excluded.BestAccuracy,
                Stars = excluded.Stars,
                CompletedAt = excluded.CompletedAt;
            """;

        cmd.Parameters.AddWithValue("@LessonId", progress.LessonId);
        cmd.Parameters.AddWithValue("@IsUnlocked", progress.IsUnlocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsCompleted", progress.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@BestWpm", progress.BestWpm);
        cmd.Parameters.AddWithValue("@BestAccuracy", progress.BestAccuracy);
        cmd.Parameters.AddWithValue("@Stars", progress.Stars);
        cmd.Parameters.AddWithValue(
            "@CompletedAt",
            progress.CompletedAt.HasValue
                ? progress.CompletedAt.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordPracticeSessionAsync(SessionResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Validate();

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        await using (var sessionCmd = connection.CreateCommand())
        {
            sessionCmd.Transaction = transaction;
            sessionCmd.CommandText = """
                INSERT INTO PracticeSessions (
                    SessionId, SessionType, LessonId, StartedAt, DurationSeconds,
                    NetWpm, GrossWpm, AccuracyPercentage, CorrectCharacters,
                    TotalKeystrokes, Mistakes, UncorrectedErrors
                ) VALUES (
                    @SessionId, @SessionType, @LessonId, @StartedAt, @DurationSeconds,
                    @NetWpm, @GrossWpm, @AccuracyPercentage, @CorrectCharacters,
                    @TotalKeystrokes, @Mistakes, @UncorrectedErrors
                );
                """;

            sessionCmd.Parameters.AddWithValue("@SessionId", result.SessionId.ToString("D"));
            sessionCmd.Parameters.AddWithValue("@SessionType", (int)result.SessionType);
            sessionCmd.Parameters.AddWithValue("@LessonId", string.IsNullOrWhiteSpace(result.LessonId) ? DBNull.Value : result.LessonId);
            sessionCmd.Parameters.AddWithValue("@StartedAt", result.StartedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            sessionCmd.Parameters.AddWithValue("@DurationSeconds", result.Duration.TotalSeconds);
            sessionCmd.Parameters.AddWithValue("@NetWpm", result.NetWpm);
            sessionCmd.Parameters.AddWithValue("@GrossWpm", result.GrossWpm);
            sessionCmd.Parameters.AddWithValue("@AccuracyPercentage", result.AccuracyPercentage);
            sessionCmd.Parameters.AddWithValue("@CorrectCharacters", result.CorrectCharacters);
            sessionCmd.Parameters.AddWithValue("@TotalKeystrokes", result.TotalKeystrokes);
            sessionCmd.Parameters.AddWithValue("@Mistakes", result.Mistakes);
            sessionCmd.Parameters.AddWithValue("@UncorrectedErrors", result.UncorrectedErrors);
            await sessionCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        string nowIso = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        foreach (KeyMetric keyMetric in result.KeyBreakdown.Values)
        {
            await using var keyCmd = connection.CreateCommand();
            keyCmd.Transaction = transaction;
            keyCmd.CommandText = """
                INSERT INTO KeyStatistics (
                    Character, TotalKeystrokes, CorrectKeystrokes, IncorrectKeystrokes, AverageLatencyMs, LastUpdated
                ) VALUES (
                    @Character, @TotalKeystrokes, @CorrectKeystrokes, @IncorrectKeystrokes, @AverageLatencyMs, @LastUpdated
                )
                ON CONFLICT(Character) DO UPDATE SET
                    AverageLatencyMs = CASE
                        WHEN (KeyStatistics.TotalKeystrokes + excluded.TotalKeystrokes) <= 0 THEN 0
                        ELSE ((KeyStatistics.AverageLatencyMs * KeyStatistics.TotalKeystrokes) + (excluded.AverageLatencyMs * excluded.TotalKeystrokes))
                             / (KeyStatistics.TotalKeystrokes + excluded.TotalKeystrokes)
                    END,
                    TotalKeystrokes = KeyStatistics.TotalKeystrokes + excluded.TotalKeystrokes,
                    CorrectKeystrokes = KeyStatistics.CorrectKeystrokes + excluded.CorrectKeystrokes,
                    IncorrectKeystrokes = KeyStatistics.IncorrectKeystrokes + excluded.IncorrectKeystrokes,
                    LastUpdated = excluded.LastUpdated;
                """;

            keyCmd.Parameters.AddWithValue("@Character", keyMetric.Character.ToString());
            keyCmd.Parameters.AddWithValue("@TotalKeystrokes", keyMetric.TotalKeystrokes);
            keyCmd.Parameters.AddWithValue("@CorrectKeystrokes", keyMetric.CorrectKeystrokes);
            keyCmd.Parameters.AddWithValue("@IncorrectKeystrokes", keyMetric.IncorrectKeystrokes);
            keyCmd.Parameters.AddWithValue("@AverageLatencyMs", keyMetric.AverageLatencyMs);
            keyCmd.Parameters.AddWithValue("@LastUpdated", nowIso);
            await keyCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionResult>> GetRecentSessionsAsync(int limit = 50, CancellationToken ct = default)
    {
        int clampedLimit = Math.Clamp(limit, 1, 500);
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT SessionId, SessionType, LessonId, StartedAt, DurationSeconds,
                   NetWpm, GrossWpm, AccuracyPercentage, CorrectCharacters,
                   TotalKeystrokes, Mistakes, UncorrectedErrors
            FROM PracticeSessions
            ORDER BY StartedAt DESC
            LIMIT @Limit;
            """;
        cmd.Parameters.AddWithValue("@Limit", clampedLimit);

        var list = new List<SessionResult>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new SessionResult
            {
                SessionId = Guid.Parse(reader.GetString(0)),
                SessionType = (SessionType)reader.GetInt32(1),
                LessonId = reader.IsDBNull(2) ? null : reader.GetString(2),
                StartedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Duration = TimeSpan.FromSeconds(reader.GetDouble(4)),
                NetWpm = reader.GetDouble(5),
                GrossWpm = reader.GetDouble(6),
                AccuracyPercentage = reader.GetDouble(7),
                CorrectCharacters = reader.GetInt32(8),
                TotalKeystrokes = reader.GetInt32(9),
                Mistakes = reader.GetInt32(10),
                UncorrectedErrors = reader.GetInt32(11)
            });
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<char, KeyMetric>> GetKeyStatisticsAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Character, TotalKeystrokes, CorrectKeystrokes, IncorrectKeystrokes, AverageLatencyMs
            FROM KeyStatistics;
            """;

        var map = new Dictionary<char, KeyMetric>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string charText = reader.GetString(0);
            if (string.IsNullOrEmpty(charText))
            {
                continue;
            }

            char key = charText[0];
            map[key] = new KeyMetric(
                character: key,
                totalKeystrokes: reader.GetInt32(1),
                correctKeystrokes: reader.GetInt32(2),
                incorrectKeystrokes: reader.GetInt32(3),
                averageLatencyMs: reader.GetDouble(4));
        }

        return map;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdaptiveKeyState>> GetAdaptiveProgressAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Character, IsUnlocked, StageIndex
            FROM AdaptiveProgress
            ORDER BY StageIndex ASC, Character ASC;
            """;

        var list = new List<AdaptiveKeyState>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string ch = reader.GetString(0);
            if (string.IsNullOrEmpty(ch))
            {
                continue;
            }

            list.Add(new AdaptiveKeyState(
                Character: char.ToUpperInvariant(ch[0]),
                IsUnlocked: reader.GetInt64(1) != 0,
                StageIndex: reader.GetInt32(2)));
        }

        return list.Count > 0 ? list : AdaptiveEngine.CreateInitialStates();
    }

    /// <inheritdoc />
    public async Task SaveAdaptiveProgressAsync(IEnumerable<AdaptiveKeyState> states, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(states);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        foreach (AdaptiveKeyState state in states)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO AdaptiveProgress (Character, IsUnlocked, StageIndex)
                VALUES (@Character, @IsUnlocked, @StageIndex)
                ON CONFLICT(Character) DO UPDATE SET
                    IsUnlocked = excluded.IsUnlocked,
                    StageIndex = excluded.StageIndex;
                """;

            cmd.Parameters.AddWithValue("@Character", char.ToUpperInvariant(state.Character).ToString());
            cmd.Parameters.AddWithValue("@IsUnlocked", state.IsUnlocked ? 1 : 0);
            cmd.Parameters.AddWithValue("@StageIndex", state.StageIndex);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings;";

        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            raw[reader.GetString(0)] = reader.GetString(1);
        }

        var settings = new UserSettings();
        if (raw.TryGetValue(nameof(UserSettings.Theme), out string? themeStr)
            && Enum.TryParse(themeStr, out ThemePreference theme))
        {
            settings.Theme = theme;
        }

        if (raw.TryGetValue(nameof(UserSettings.TypingFontSize), out string? fontStr)
            && int.TryParse(fontStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fontSize))
        {
            settings.TypingFontSize = Math.Clamp(fontSize, UserSettings.MinFontSize, UserSettings.MaxFontSize);
        }

        if (raw.TryGetValue(nameof(UserSettings.ShowVirtualKeyboard), out string? showKbd)
            && bool.TryParse(showKbd, out bool showKeyboard))
        {
            settings.ShowVirtualKeyboard = showKeyboard;
        }

        if (raw.TryGetValue(nameof(UserSettings.KeyboardColorMode), out string? colorModeStr)
            && Enum.TryParse(colorModeStr, out KeyboardColorMode colorMode))
        {
            settings.KeyboardColorMode = colorMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.SoundEnabled), out string? soundStr)
            && bool.TryParse(soundStr, out bool sound))
        {
            settings.SoundEnabled = sound;
        }

        if (raw.TryGetValue(nameof(UserSettings.StopOnError), out string? stopStr)
            && bool.TryParse(stopStr, out bool stopOnError))
        {
            settings.StopOnError = stopOnError;
        }

        if (raw.TryGetValue(nameof(UserSettings.AdaptiveProfile), out string? profStr)
            && Enum.TryParse(profStr, out AdaptiveDifficultyProfile profile))
        {
            settings.AdaptiveProfile = profile;
        }

        if (raw.TryGetValue(nameof(UserSettings.AdaptiveWordMode), out string? modeStr)
            && Enum.TryParse(modeStr, out AdaptiveWordMode wordMode))
        {
            settings.AdaptiveWordMode = wordMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.DailyGoalMinutes), out string? goalStr)
            && int.TryParse(goalStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int goalMinutes))
        {
            settings.DailyGoalMinutes = Math.Clamp(goalMinutes, UserSettings.MinDailyGoalMinutes, UserSettings.MaxDailyGoalMinutes);
        }

        if (raw.TryGetValue(nameof(UserSettings.OptimalDurationEnabled), out string? optStr)
            && bool.TryParse(optStr, out bool optEnabled))
        {
            settings.OptimalDurationEnabled = optEnabled;
        }

        if (raw.TryGetValue(nameof(UserSettings.SelectedCourseTrackId), out string? trackStr)
            && !string.IsNullOrWhiteSpace(trackStr))
        {
            settings.SelectedCourseTrackId = trackStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.ForgiveErrors), out string? forgiveStr)
            && bool.TryParse(forgiveStr, out bool forgiveErrors))
        {
            settings.ForgiveErrors = forgiveErrors;
        }

        if (raw.TryGetValue(nameof(UserSettings.SpaceSkipsWords), out string? skipStr)
            && bool.TryParse(skipStr, out bool spaceSkipsWords))
        {
            settings.SpaceSkipsWords = spaceSkipsWords;
        }

        if (raw.TryGetValue(nameof(UserSettings.TypingFontSelection), out string? fontSelStr)
            && !string.IsNullOrWhiteSpace(fontSelStr))
        {
            settings.TypingFontSelection = fontSelStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.WhitespaceDisplay), out string? wsStr)
            && Enum.TryParse(wsStr, out WhitespaceDisplayMode wsMode))
        {
            settings.WhitespaceDisplay = wsMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.CursorShape), out string? cursorShapeStr)
            && Enum.TryParse(cursorShapeStr, out CursorShapeMode cursorShape))
        {
            settings.CursorShape = cursorShape;
        }

        if (raw.TryGetValue(nameof(UserSettings.CursorMovement), out string? cursorMoveStr)
            && Enum.TryParse(cursorMoveStr, out CursorMovementMode cursorMove))
        {
            settings.CursorMovement = cursorMove;
        }

        if (raw.TryGetValue(nameof(UserSettings.SoundMode), out string? soundModeStr)
            && Enum.TryParse(soundModeStr, out SoundFeedbackMode soundMode))
        {
            settings.SoundMode = soundMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.SoundVolume), out string? soundVolStr)
            && int.TryParse(soundVolStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int soundVol))
        {
            settings.SoundVolume = Math.Clamp(soundVol, 0, 100);
        }

        if (raw.TryGetValue(nameof(UserSettings.SoundTheme), out string? soundThemeStr)
            && Enum.TryParse(soundThemeStr, out SoundTheme soundTheme))
        {
            settings.SoundTheme = soundTheme;
        }

        if (raw.TryGetValue(nameof(UserSettings.TestTextSource), out string? testSourceStr)
            && Enum.TryParse(testSourceStr, out TestTextSource testSource))
        {
            settings.TestTextSource = testSource;
        }

        if (raw.TryGetValue(nameof(UserSettings.TestDurationMode), out string? testDurStr)
            && Enum.TryParse(testDurStr, out TestDurationMode testDur))
        {
            settings.TestDurationMode = testDur;
        }

        if (raw.TryGetValue(nameof(UserSettings.CommonWordCount), out string? wordCountStr)
            && int.TryParse(wordCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wordCount))
        {
            settings.CommonWordCount = Math.Clamp(wordCount, UserSettings.MinCommonWordCount, UserSettings.MaxCommonWordCount);
        }

        if (raw.TryGetValue(nameof(UserSettings.SelectedLanguage), out string? langStr)
            && !string.IsNullOrWhiteSpace(langStr))
        {
            settings.SelectedLanguage = langStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.CustomWordListCsv), out string? customWordsStr))
        {
            settings.CustomWordListCsv = customWordsStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.SelectedBookId), out string? bookIdStr)
            && !string.IsNullOrWhiteSpace(bookIdStr))
        {
            settings.SelectedBookId = bookIdStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.SelectedBookParagraphIndex), out string? paraStr)
            && int.TryParse(paraStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int paraIdx))
        {
            settings.SelectedBookParagraphIndex = Math.Max(0, paraIdx);
        }

        if (raw.TryGetValue(nameof(UserSettings.CustomTestText), out string? customTestStr))
        {
            settings.CustomTestText = customTestStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.PaceCaretMode), out string? paceModeStr)
            && Enum.TryParse(paceModeStr, out PaceCaretMode paceMode)
            && Enum.IsDefined(paceMode))
        {
            settings.PaceCaretMode = paceMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.PaceTargetWpm), out string? paceWpmStr)
            && int.TryParse(paceWpmStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int paceWpm))
        {
            settings.PaceTargetWpm = Math.Clamp(paceWpm, UserSettings.MinPaceTargetWpm, UserSettings.MaxPaceTargetWpm);
        }

        if (raw.TryGetValue(nameof(UserSettings.KeyboardFadeStage), out string? fadeStr)
            && Enum.TryParse(fadeStr, out KeyboardFadeStage fadeStage)
            && Enum.IsDefined(fadeStage))
        {
            settings.KeyboardFadeStage = fadeStage;
        }

        if (raw.TryGetValue(nameof(UserSettings.FingerGuideMode), out string? fingerModeStr)
            && Enum.TryParse(fingerModeStr, out FingerGuideMode fingerMode)
            && Enum.IsDefined(fingerMode))
        {
            settings.FingerGuideMode = fingerMode;
        }

        if (raw.TryGetValue(nameof(UserSettings.ShowWpmInCourse), out string? showWpmStr)
            && bool.TryParse(showWpmStr, out bool showWpm))
        {
            settings.ShowWpmInCourse = showWpm;
        }

        if (raw.TryGetValue(nameof(UserSettings.SelectedRealWorldPoolId), out string? poolIdStr)
            && !string.IsNullOrWhiteSpace(poolIdStr))
        {
            settings.SelectedRealWorldPoolId = poolIdStr;
        }

        if (raw.TryGetValue(nameof(UserSettings.CoursePassGateEnabled), out string? gateEnabledStr)
            && bool.TryParse(gateEnabledStr, out bool gateEnabled))
        {
            settings.CoursePassGateEnabled = gateEnabled;
        }

        if (raw.TryGetValue(nameof(UserSettings.CourseMinAccuracy), out string? minAccStr)
            && double.TryParse(minAccStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double minAcc))
        {
            settings.CourseMinAccuracy = Math.Clamp(minAcc, UserSettings.MinCourseAccuracy, UserSettings.MaxCourseAccuracy);
        }

        if (raw.TryGetValue(nameof(UserSettings.CourseMaxMistakes), out string? maxMistakesStr)
            && int.TryParse(maxMistakesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxMistakes))
        {
            settings.CourseMaxMistakes = Math.Clamp(maxMistakes, UserSettings.MinCourseMaxMistakes, UserSettings.MaxCourseMaxMistakes);
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeEnabled), out string? metEnabledStr)
            && bool.TryParse(metEnabledStr, out bool metEnabled))
        {
            settings.MetronomeEnabled = metEnabled;
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeBeatMode), out string? metBeatStr)
            && Enum.TryParse(metBeatStr, out MetronomeBeatMode metBeat)
            && Enum.IsDefined(metBeat))
        {
            settings.MetronomeBeatMode = metBeat;
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeTargetMode), out string? metTargetStr)
            && Enum.TryParse(metTargetStr, out MetronomeTargetMode metTarget)
            && Enum.IsDefined(metTarget))
        {
            settings.MetronomeTargetMode = metTarget;
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeTargetWpm), out string? metWpmStr)
            && int.TryParse(metWpmStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int metWpm))
        {
            settings.MetronomeTargetWpm = Math.Clamp(metWpm, UserSettings.MinMetronomeWpm, UserSettings.MaxMetronomeWpm);
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeAudioEnabled), out string? metAudioStr)
            && bool.TryParse(metAudioStr, out bool metAudio))
        {
            settings.MetronomeAudioEnabled = metAudio;
        }

        if (raw.TryGetValue(nameof(UserSettings.MetronomeVisualEnabled), out string? metVisualStr)
            && bool.TryParse(metVisualStr, out bool metVisual))
        {
            settings.MetronomeVisualEnabled = metVisual;
        }

        return settings;
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(UserSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var pairs = new (string Key, string Value)[]
        {
            (nameof(UserSettings.Theme), settings.Theme.ToString()),
            (nameof(UserSettings.TypingFontSize), settings.TypingFontSize.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.ShowVirtualKeyboard), settings.ShowVirtualKeyboard.ToString()),
            (nameof(UserSettings.KeyboardColorMode), settings.KeyboardColorMode.ToString()),
            (nameof(UserSettings.SoundEnabled), settings.SoundEnabled.ToString()),
            (nameof(UserSettings.StopOnError), settings.StopOnError.ToString()),
            (nameof(UserSettings.AdaptiveProfile), settings.AdaptiveProfile.ToString()),
            (nameof(UserSettings.AdaptiveWordMode), settings.AdaptiveWordMode.ToString()),
            (nameof(UserSettings.DailyGoalMinutes), settings.DailyGoalMinutes.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.OptimalDurationEnabled), settings.OptimalDurationEnabled.ToString()),
            (nameof(UserSettings.SelectedCourseTrackId), settings.SelectedCourseTrackId),
            (nameof(UserSettings.ForgiveErrors), settings.ForgiveErrors.ToString()),
            (nameof(UserSettings.SpaceSkipsWords), settings.SpaceSkipsWords.ToString()),
            (nameof(UserSettings.TypingFontSelection), settings.TypingFontSelection),
            (nameof(UserSettings.WhitespaceDisplay), settings.WhitespaceDisplay.ToString()),
            (nameof(UserSettings.CursorShape), settings.CursorShape.ToString()),
            (nameof(UserSettings.CursorMovement), settings.CursorMovement.ToString()),
            (nameof(UserSettings.SoundMode), settings.SoundMode.ToString()),
            (nameof(UserSettings.SoundVolume), settings.SoundVolume.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.SoundTheme), settings.SoundTheme.ToString()),
            (nameof(UserSettings.TestTextSource), settings.TestTextSource.ToString()),
            (nameof(UserSettings.TestDurationMode), settings.TestDurationMode.ToString()),
            (nameof(UserSettings.CommonWordCount), settings.CommonWordCount.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.SelectedLanguage), settings.SelectedLanguage),
            (nameof(UserSettings.CustomWordListCsv), settings.CustomWordListCsv),
            (nameof(UserSettings.SelectedBookId), settings.SelectedBookId),
            (nameof(UserSettings.SelectedBookParagraphIndex), settings.SelectedBookParagraphIndex.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.CustomTestText), settings.CustomTestText),
            (nameof(UserSettings.PaceCaretMode), settings.PaceCaretMode.ToString()),
            (nameof(UserSettings.PaceTargetWpm), settings.PaceTargetWpm.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.KeyboardFadeStage), settings.KeyboardFadeStage.ToString()),
            (nameof(UserSettings.FingerGuideMode), settings.FingerGuideMode.ToString()),
            (nameof(UserSettings.ShowWpmInCourse), settings.ShowWpmInCourse.ToString()),
            (nameof(UserSettings.SelectedRealWorldPoolId), settings.SelectedRealWorldPoolId),
            (nameof(UserSettings.CoursePassGateEnabled), settings.CoursePassGateEnabled.ToString()),
            (nameof(UserSettings.CourseMinAccuracy), settings.CourseMinAccuracy.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.CourseMaxMistakes), settings.CourseMaxMistakes.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.MetronomeEnabled), settings.MetronomeEnabled.ToString()),
            (nameof(UserSettings.MetronomeBeatMode), settings.MetronomeBeatMode.ToString()),
            (nameof(UserSettings.MetronomeTargetMode), settings.MetronomeTargetMode.ToString()),
            (nameof(UserSettings.MetronomeTargetWpm), settings.MetronomeTargetWpm.ToString(CultureInfo.InvariantCulture)),
            (nameof(UserSettings.MetronomeAudioEnabled), settings.MetronomeAudioEnabled.ToString()),
            (nameof(UserSettings.MetronomeVisualEnabled), settings.MetronomeVisualEnabled.ToString())
        };

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        foreach ((string key, string value) in pairs)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO Settings (Key, Value) VALUES (@Key, @Value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
                """;
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TimeSpan> GetTotalDurationForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        string prefix = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "%";
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(DurationSeconds), 0)
            FROM PracticeSessions
            WHERE StartedAt LIKE @DatePrefix;
            """;
        cmd.Parameters.AddWithValue("@DatePrefix", prefix);

        object? scalar = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        double seconds = scalar is null or DBNull ? 0.0 : Convert.ToDouble(scalar, CultureInfo.InvariantCulture);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateOnly>> GetPracticeDatesAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT substr(StartedAt, 1, 10) AS PracticeDay
            FROM PracticeSessions
            ORDER BY PracticeDay DESC;
            """;

        var dates = new List<DateOnly>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (DateOnly.TryParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            {
                dates.Add(parsed);
            }
        }

        return dates;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, LessonProgressRecord>> LoadExerciseProgressAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT ExerciseId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt
            FROM CourseExerciseProgress;
            """;

        var result = new Dictionary<string, LessonProgressRecord>(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string exerciseId = reader.GetString(0);
            DateTimeOffset? completedAt = reader.IsDBNull(6)
                ? null
                : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            result[exerciseId] = new LessonProgressRecord(
                LessonId: exerciseId,
                IsUnlocked: reader.GetInt64(1) != 0,
                IsCompleted: reader.GetInt64(2) != 0,
                BestWpm: reader.GetDouble(3),
                BestAccuracy: reader.GetDouble(4),
                Stars: reader.GetInt32(5),
                CompletedAt: completedAt);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveExerciseProgressAsync(LessonProgressRecord progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO CourseExerciseProgress (
                ExerciseId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt
            ) VALUES (
                @ExerciseId, @IsUnlocked, @IsCompleted, @BestWpm, @BestAccuracy, @Stars, @CompletedAt
            )
            ON CONFLICT(ExerciseId) DO UPDATE SET
                IsUnlocked = excluded.IsUnlocked,
                IsCompleted = excluded.IsCompleted,
                BestWpm = excluded.BestWpm,
                BestAccuracy = excluded.BestAccuracy,
                Stars = excluded.Stars,
                CompletedAt = excluded.CompletedAt;
            """;

        cmd.Parameters.AddWithValue("@ExerciseId", progress.LessonId);
        cmd.Parameters.AddWithValue("@IsUnlocked", progress.IsUnlocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsCompleted", progress.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@BestWpm", progress.BestWpm);
        cmd.Parameters.AddWithValue("@BestAccuracy", progress.BestAccuracy);
        cmd.Parameters.AddWithValue("@Stars", progress.Stars);
        cmd.Parameters.AddWithValue(
            "@CompletedAt",
            progress.CompletedAt.HasValue
                ? progress.CompletedAt.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, BigramMetric>> GetBigramStatisticsAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Bigram, TotalCount, ErrorCount, AverageLatencyMs
            FROM BigramStatistics;
            """;

        var dict = new Dictionary<string, BigramMetric>(StringComparer.Ordinal);
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string bigram = reader.GetString(0);
            if (bigram.Length == 2)
            {
                dict[bigram] = new BigramMetric(
                    Bigram: bigram,
                    TotalCount: reader.GetInt32(1),
                    ErrorCount: reader.GetInt32(2),
                    AverageLatencyMs: reader.GetDouble(3));
            }
        }

        return dict;
    }

    /// <inheritdoc />
    public async Task SaveBigramStatisticsAsync(IEnumerable<BigramMetric> bigrams, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bigrams);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        const string sql = """
            INSERT INTO BigramStatistics (Bigram, TotalCount, ErrorCount, AverageLatencyMs)
            VALUES (@Bigram, @TotalCount, @ErrorCount, @AverageLatencyMs)
            ON CONFLICT(Bigram) DO UPDATE SET
                TotalCount = excluded.TotalCount,
                ErrorCount = excluded.ErrorCount,
                AverageLatencyMs = excluded.AverageLatencyMs;
            """;

        foreach (BigramMetric item in bigrams)
        {
            item.Validate();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@Bigram", item.Bigram);
            cmd.Parameters.AddWithValue("@TotalCount", item.TotalCount);
            cmd.Parameters.AddWithValue("@ErrorCount", item.ErrorCount);
            cmd.Parameters.AddWithValue("@AverageLatencyMs", item.AverageLatencyMs);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<char, KeyReviewScheduleRecord>> GetKeyReviewSchedulesAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Character, LastReviewedAt, IntervalDays, EaseFactor, DueAt
            FROM KeyReviewSchedule;
            """;

        var dict = new Dictionary<char, KeyReviewScheduleRecord>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string charStr = reader.GetString(0);
            if (charStr.Length == 0)
            {
                continue;
            }

            char ch = charStr[0];
            DateTimeOffset? lastReviewed = reader.IsDBNull(1)
                ? null
                : DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTimeOffset dueAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            dict[ch] = new KeyReviewScheduleRecord(
                Character: ch,
                LastReviewedAt: lastReviewed,
                IntervalDays: reader.GetDouble(2),
                EaseFactor: reader.GetDouble(3),
                DueAt: dueAt);
        }

        return dict;
    }

    /// <inheritdoc />
    public async Task SaveKeyReviewSchedulesAsync(IEnumerable<KeyReviewScheduleRecord> schedules, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schedules);

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        const string sql = """
            INSERT INTO KeyReviewSchedule (Character, LastReviewedAt, IntervalDays, EaseFactor, DueAt)
            VALUES (@Character, @LastReviewedAt, @IntervalDays, @EaseFactor, @DueAt)
            ON CONFLICT(Character) DO UPDATE SET
                LastReviewedAt = excluded.LastReviewedAt,
                IntervalDays = excluded.IntervalDays,
                EaseFactor = excluded.EaseFactor,
                DueAt = excluded.DueAt;
            """;

        foreach (KeyReviewScheduleRecord item in schedules)
        {
            item.Validate();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@Character", item.Character.ToString());
            cmd.Parameters.AddWithValue(
                "@LastReviewedAt",
                item.LastReviewedAt.HasValue
                    ? item.LastReviewedAt.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
                    : DBNull.Value);
            cmd.Parameters.AddWithValue("@IntervalDays", item.IntervalDays);
            cmd.Parameters.AddWithValue("@EaseFactor", item.EaseFactor);
            cmd.Parameters.AddWithValue("@DueAt", item.DueAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlacementResultRecord>> GetPlacementResultsAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT PlacementId, TakenAt, NetWpm, Accuracy, RecommendedTrack
            FROM PlacementResults
            ORDER BY TakenAt DESC;
            """;

        var list = new List<PlacementResultRecord>();
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new PlacementResultRecord(
                PlacementId: reader.GetString(0),
                TakenAt: DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                NetWpm: reader.GetDouble(2),
                AccuracyPercentage: reader.GetDouble(3),
                RecommendedTrack: reader.GetString(4)));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task SavePlacementResultAsync(PlacementResultRecord placement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        placement.Validate();

        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PlacementResults (PlacementId, TakenAt, NetWpm, Accuracy, RecommendedTrack)
            VALUES (@PlacementId, @TakenAt, @NetWpm, @Accuracy, @RecommendedTrack)
            ON CONFLICT(PlacementId) DO UPDATE SET
                TakenAt = excluded.TakenAt,
                NetWpm = excluded.NetWpm,
                Accuracy = excluded.Accuracy,
                RecommendedTrack = excluded.RecommendedTrack;
            """;

        cmd.Parameters.AddWithValue("@PlacementId", placement.PlacementId);
        cmd.Parameters.AddWithValue("@TakenAt", placement.TakenAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@NetWpm", placement.NetWpm);
        cmd.Parameters.AddWithValue("@Accuracy", placement.AccuracyPercentage);
        cmd.Parameters.AddWithValue("@RecommendedTrack", placement.RecommendedTrack);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetProgressAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                DELETE FROM PracticeSessions;
                DELETE FROM KeyStatistics;
                DELETE FROM BigramStatistics;
                DELETE FROM KeyReviewSchedule;
                DELETE FROM PlacementResults;
                DELETE FROM AdaptiveProgress;
                DELETE FROM CourseExerciseProgress;
                DELETE FROM LessonProgress;

                INSERT OR IGNORE INTO LessonProgress (LessonId, IsUnlocked, IsCompleted, BestWpm, BestAccuracy, Stars, CompletedAt)
                SELECT LessonId, 1, 0, 0, 0, 0, NULL
                FROM Lessons
                ORDER BY ROWID ASC
                LIMIT 1;
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return connection;
    }

    private static async Task<int> ReadUserVersionAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "PRAGMA user_version;";
        object? result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        int version = result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        return Math.Max(0, Math.Min(version, CurrentSchemaVersion));
    }
}
