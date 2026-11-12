using Microsoft.Data.Sqlite;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

[Collection("SqliteTests")]
public sealed class LearningEngineTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset initialUtcNow)
        {
            _utcNow = initialUtcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    [Fact]
    public async Task SqliteMigrationV1ToV3_PreservesExistingData_AndPersistsNewTablesAndSettings()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"typeforge_v1_upgrade_{Guid.NewGuid():N}.db");
        try
        {
            // Seed a legacy v1 database manually
            await using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE Sessions (
                        Id TEXT PRIMARY KEY,
                        SessionType INTEGER NOT NULL,
                        LessonId TEXT NULL,
                        StartedAtUtc TEXT NOT NULL,
                        DurationMs REAL NOT NULL,
                        GrossWpm REAL NOT NULL,
                        NetWpm REAL NOT NULL,
                        AccuracyPercentage REAL NOT NULL,
                        CorrectCharacters INTEGER NOT NULL,
                        TotalKeystrokes INTEGER NOT NULL,
                        Mistakes INTEGER NOT NULL,
                        UncorrectedErrors INTEGER NOT NULL
                    );
                    CREATE TABLE KeyStatistics (
                        Character TEXT PRIMARY KEY,
                        TotalKeystrokes INTEGER NOT NULL,
                        CorrectKeystrokes INTEGER NOT NULL,
                        IncorrectKeystrokes INTEGER NOT NULL,
                        AverageLatencyMs REAL NOT NULL
                    );
                    CREATE TABLE LessonProgress (
                        LessonId TEXT PRIMARY KEY,
                        IsUnlocked INTEGER NOT NULL,
                        IsCompleted INTEGER NOT NULL,
                        Stars INTEGER NOT NULL,
                        BestNetWpm REAL NOT NULL,
                        BestAccuracy REAL NOT NULL
                    );
                    CREATE TABLE AdaptiveKeyStates (
                        Letter TEXT PRIMARY KEY,
                        IsUnlocked INTEGER NOT NULL,
                        Samples INTEGER NOT NULL,
                        CorrectCount INTEGER NOT NULL,
                        AverageLatencyMs REAL NOT NULL
                    );
                    CREATE TABLE Settings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL
                    );
                    INSERT INTO KeyStatistics (Character, TotalKeystrokes, CorrectKeystrokes, IncorrectKeystrokes, AverageLatencyMs)
                    VALUES ('F', 42, 40, 2, 185.5);
                    PRAGMA user_version = 1;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var repo = new SqliteTypeForgeRepository(tempDbPath);
            await repo.InitializeAsync();

            var keyStats = await repo.GetKeyStatisticsAsync();
            Assert.True(keyStats.TryGetValue('F', out var fMetric));
            Assert.Equal(42, fMetric!.TotalKeystrokes);
            Assert.Equal(40, fMetric.CorrectKeystrokes);

            // Verify v3 BigramStatistics CRUD
            await repo.SaveBigramStatisticsAsync(
            [
                new BigramMetric("TH", TotalCount: 30, ErrorCount: 3, AverageLatencyMs: 210.0)
            ]);
            var bigrams = await repo.GetBigramStatisticsAsync();
            Assert.True(bigrams.TryGetValue("TH", out var thMetric));
            Assert.Equal(30, thMetric!.TotalCount);
            Assert.Equal(3, thMetric.ErrorCount);

            // Verify v3 KeyReviewSchedule CRUD
            var reviewedAt = new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
            var dueTime = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
            await repo.SaveKeyReviewSchedulesAsync(
            [
                new KeyReviewScheduleRecord(
                    Character: 'E',
                    LastReviewedAt: reviewedAt,
                    IntervalDays: 3.0,
                    EaseFactor: 2.15,
                    DueAt: dueTime)
            ]);
            var schedules = await repo.GetKeyReviewSchedulesAsync();
            Assert.Single(schedules);
            Assert.True(schedules.TryGetValue('E', out var eSchedule));
            Assert.Equal('E', eSchedule!.Character);
            Assert.Equal(3.0, eSchedule.IntervalDays);
            Assert.Equal(dueTime, eSchedule.DueAt);

            // Verify v3 PlacementResults CRUD
            var placementRecord = new PlacementResultRecord(
                PlacementId: "placement_test_01",
                TakenAt: new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero),
                NetWpm: 34.5,
                AccuracyPercentage: 96.2,
                RecommendedTrack: "speed_building");
            await repo.SavePlacementResultAsync(placementRecord);
            var placements = await repo.GetPlacementResultsAsync();
            Assert.Single(placements);
            Assert.Equal("speed_building", placements[0].RecommendedTrack);
            Assert.Equal(34.5, placements[0].NetWpm);

            // Verify v3 UserSettings fields round-trip
            var settings = await repo.GetSettingsAsync();
            settings.PaceCaretMode = PaceCaretMode.FixedWpm;
            settings.PaceTargetWpm = 75;
            settings.KeyboardFadeStage = KeyboardFadeStage.UnlabeledWithBumps;
            settings.FingerGuideMode = FingerGuideMode.AlwaysOn;
            settings.ShowWpmInCourse = true;
            await repo.SaveSettingsAsync(settings);

            var reloadedSettings = await repo.GetSettingsAsync();
            Assert.Equal(PaceCaretMode.FixedWpm, reloadedSettings.PaceCaretMode);
            Assert.Equal(75, reloadedSettings.PaceTargetWpm);
            Assert.Equal(KeyboardFadeStage.UnlabeledWithBumps, reloadedSettings.KeyboardFadeStage);
            Assert.Equal(FingerGuideMode.AlwaysOn, reloadedSettings.FingerGuideMode);
            Assert.True(reloadedSettings.ShowWpmInCourse);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteMigrationV2ToV3_RollsBackCleanlyOnFault_PreservingUserVersionAndData()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"typeforge_v2_rollback_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDbPath);

            // Downgrade database to v2 after initial creation to simulate an existing v2 installation
            await repo.InitializeAsync();

            await using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO CourseExerciseProgress (ExerciseId, IsCompleted, Stars)
                    VALUES ('ft_01_01', 1, 3);
                    DROP TABLE BigramStatistics;
                    DROP TABLE KeyReviewSchedule;
                    DROP TABLE PlacementResults;
                    PRAGMA user_version = 2;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            // Inject fault during v3 migration: InitializeAsync must roll back the v3 transaction and NOT crash so the app still opens
            var faultyRepo = new SqliteTypeForgeRepository(tempDbPath)
            {
                MigrationStepFaultHook = targetVersion =>
                {
                    if (targetVersion == 3)
                    {
                        throw new InvalidOperationException("Simulated disk error during v3 migration.");
                    }
                }
            };

            await faultyRepo.InitializeAsync();

            // Verify user_version rolled back to 2, BigramStatistics was rolled back, and CourseExerciseProgress data is intact
            await using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                await using var verCmd = conn.CreateCommand();
                verCmd.CommandText = "PRAGMA user_version;";
                int versionAfterFailure = Convert.ToInt32(await verCmd.ExecuteScalarAsync());
                Assert.Equal(2, versionAfterFailure);
            }

            // Subsequent clean initialization succeeds and preserves v2 data
            var cleanRepo = new SqliteTypeForgeRepository(tempDbPath);
            await cleanRepo.InitializeAsync();
            await using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT IsCompleted FROM CourseExerciseProgress WHERE ExerciseId = 'ft_01_01';";
                int isCompleted = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                Assert.Equal(1, isCompleted);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public void BigramTracker_TracksLatenciesDropsOutliersCountsErrorsAndResetsOnBackspace()
    {
        var tracker = new BigramTracker();

        // Empty and 1-character input produce no bigrams
        Assert.Empty(tracker.GetMetrics());
        tracker.RecordKeystroke('t', isCorrect: true, latencyMs: null);
        Assert.Empty(tracker.GetMetrics());

        // Valid transition 't' -> 'h' at 200ms
        tracker.RecordKeystroke('h', isCorrect: true, latencyMs: 200.0);
        var metrics = tracker.GetMetrics();
        Assert.Single(metrics);
        Assert.True(metrics.TryGetValue("TH", out var thMetric));
        Assert.Equal(1, thMetric!.TotalCount);
        Assert.Equal(0, thMetric.ErrorCount);
        Assert.Equal(200.0, thMetric.AverageLatencyMs);

        // Outlier transition > 2000ms is excluded from EMA latency update
        tracker.RecordKeystroke('e', isCorrect: true, latencyMs: 3500.0);
        Assert.True(tracker.GetMetrics().TryGetValue("HE", out var heMetric));
        Assert.Equal(1, heMetric!.TotalCount);
        Assert.Equal(0.0, heMetric.AverageLatencyMs);

        // Space resets predecessor; next letter starts fresh
        tracker.RecordKeystroke(' ', isCorrect: true, latencyMs: 120.0);
        tracker.RecordKeystroke('t', isCorrect: true, latencyMs: 140.0);

        // Mistake on second letter ('h') increments ErrorCount for "TH" without updating latency EMA, and resets predecessor
        tracker.RecordKeystroke('h', isCorrect: false, latencyMs: 500.0);
        var thAfterErr = tracker.GetMetrics()["TH"];
        Assert.Equal(2, thAfterErr.TotalCount);
        Assert.Equal(1, thAfterErr.ErrorCount);
        Assert.Equal(200.0, thAfterErr.AverageLatencyMs);

        // Backspace clears predecessor so next keystroke does not form a phantom bigram
        tracker.RecordKeystroke('i', isCorrect: true, latencyMs: 150.0);
        tracker.RecordBackspace();
        tracker.RecordKeystroke('n', isCorrect: true, latencyMs: 150.0);
        Assert.False(tracker.GetMetrics().ContainsKey("IN"));
    }

    [Fact]
    public void TypingEngine_IntegratesBigramTrackerAcrossKeystrokes()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero));
        var engine = new TypingEngine("the", stopOnError: false, timeProvider: fakeTime);

        engine.ProcessKeystroke('t');
        fakeTime.Advance(TimeSpan.FromMilliseconds(180));
        engine.ProcessKeystroke('h');
        fakeTime.Advance(TimeSpan.FromMilliseconds(220));
        engine.ProcessKeystroke('e');

        var bigrams = engine.GetBigramBreakdown();
        Assert.Equal(2, bigrams.Count);
        Assert.True(bigrams.TryGetValue("TH", out var th) && Math.Abs(th.AverageLatencyMs - 180.0) < 0.01);
        Assert.True(bigrams.TryGetValue("HE", out var he) && Math.Abs(he.AverageLatencyMs - 220.0) < 0.01);
    }

    [Fact]
    public void WeaknessScorer_AppliesMinSampleGuardDeterministicTieBreakAndZeroVarianceHandling()
    {
        // Under-sampled keys (< 20 occurrences) must be ignored
        var underSampled = new List<KeyMetric>
        {
            new('Q', 19, 5, 14, 600),
            new('Z', 10, 2, 8, 700)
        };
        Assert.Empty(WeaknessScorer.RankKeys(underSampled));

        // Identical error rates & latencies (zero variance) must not divide by zero and must sort alphabetically
        var identical = new List<KeyMetric>
        {
            new('K', 30, 27, 3, 250),
            new('D', 30, 27, 3, 250)
        };
        var rankedIdentical = WeaknessScorer.RankKeys(identical);
        Assert.Equal(2, rankedIdentical.Count);
        Assert.Equal('D', rankedIdentical[0].Target);
        Assert.Equal('K', rankedIdentical[1].Target);

        // Blending error rate (65%) and latency z-score (35%) ranks high-error/slow key first
        var mixed = new List<KeyMetric>
        {
            new('F', 50, 50, 0, 160),
            new('J', 50, 49, 1, 175),
            new('R', 50, 40, 10, 390)
        };
        var rankedMixed = WeaknessScorer.RankKeys(mixed);
        Assert.Equal('R', rankedMixed[0].Target);
        Assert.True(rankedMixed[0].Score > rankedMixed[1].Score);
    }

    [Fact]
    public void GenerateWeightedPracticeText_HitsWeakTargetsRoughlyHalfTheTime_AndNeverEmitsLockedLetters()
    {
        // Unlock stages 1..4 (f, j, d, k, s, l, a, g, h)
        var states = AdaptiveEngine.CreateStatesWithUnlockedStage(4);
        var unlockedUpperSet = states.Where(s => s.IsUnlocked).Select(s => char.ToUpperInvariant(s.Character)).ToHashSet();
        var unlockedLowerSet = states.Where(s => s.IsUnlocked).Select(s => char.ToLowerInvariant(s.Character)).ToHashSet();

        var dictionary = new List<string>
        {
            "fall", "hall", "dash", "glad", "half", "flag", "ask", "had", "sad", "lad", "jag", "jak",
            "fade", "tree", "water" // words with locked letters ('e', 't', 'r', 'w') must be filtered out
        };

        var keyMetricsDict = new Dictionary<char, KeyMetric>
        {
            ['G'] = new KeyMetric('G', 40, 28, 12, 390),
            ['F'] = new KeyMetric('F', 40, 40, 0, 170)
        };
        var bigramMetricsDict = new Dictionary<string, BigramMetric>
        {
            ["GL"] = new BigramMetric("GL", TotalCount: 30, ErrorCount: 8, AverageLatencyMs: 360),
            ["HA"] = new BigramMetric("HA", TotalCount: 30, ErrorCount: 6, AverageLatencyMs: 340)
        };
        var rng = new Random(42);

        int totalWords = 0;
        int weakMatchedWords = 0;

        for (int i = 0; i < 1000; i++)
        {
            string drill = AdaptiveEngine.GenerateWeightedPracticeText(
                unlockedUpperSet,
                keyMetricsDict,
                bigramMetricsDict,
                AdaptiveWordMode.NaturalDictionaryWords,
                dictionary,
                wordCount: 20,
                0.50,
                random: rng);

            string[] words = drill.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                // Strictly zero locked letters
                foreach (char c in word)
                {
                    Assert.Contains(char.ToLowerInvariant(c), unlockedLowerSet);
                }

                totalWords++;
                if (word.Contains('g', StringComparison.OrdinalIgnoreCase) ||
                    word.Contains("gl", StringComparison.OrdinalIgnoreCase) ||
                    word.Contains("ha", StringComparison.OrdinalIgnoreCase))
                {
                    weakMatchedWords++;
                }
            }
        }

        double weakRatio = (double)weakMatchedWords / totalWords;
        // Over 20,000 generated words, ratio should sit close to ~50% (within 0.45..0.55)
        Assert.InRange(weakRatio, 0.45, 0.55);
    }

    [Fact]
    public void ReviewScheduler_GrowsIntervalOnMastery_AndResetsOnFailure()
    {
        var startUtc = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(startUtc);
        var scheduler = new ReviewScheduler(fakeTime);

        var fastMetric = new KeyMetric('F', 25, 25, 0, 220);

        // 1st mastery -> 1 day interval
        var s1 = scheduler.EvaluateKeySession(null, 'F', fastMetric, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);
        Assert.Equal(1.0, s1.IntervalDays);
        Assert.Equal(startUtc.AddDays(1.0), s1.DueAt);

        // 2nd consecutive mastery -> 3 days interval
        fakeTime.Advance(TimeSpan.FromDays(1));
        var s2 = scheduler.EvaluateKeySession(s1, 'F', fastMetric, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);
        Assert.Equal(3.0, s2.IntervalDays);

        // 3rd consecutive mastery -> 3 * easeFactor days
        fakeTime.Advance(TimeSpan.FromDays(3));
        var s3 = scheduler.EvaluateKeySession(s2, 'F', fastMetric, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);
        Assert.True(s3.IntervalDays > 6.0);

        // Failure (< 95% accuracy) resets interval to 0.5 days (12h)
        var failingMetric = new KeyMetric('F', 25, 20, 5, 220);
        var sReset = scheduler.EvaluateKeySession(s3, 'F', failingMetric, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);
        Assert.Equal(0.5, sReset.IntervalDays);

        // GetDueKeys returns keys whose DueAt <= now
        Assert.Empty(scheduler.GetDueKeys([sReset]));
        fakeTime.Advance(TimeSpan.FromHours(13));
        Assert.Single(scheduler.GetDueKeys([sReset]));
    }

    [Theory]
    [InlineData(10.0, 98.0, "fast_touch", 1, "ft_01_02", 1)]
    [InlineData(25.0, 88.0, "fast_touch", 1, "ft_01_02", 1)]
    [InlineData(15.0, 90.0, "fast_touch", 6, "ft_06_01", 6)]
    [InlineData(29.9, 97.0, "fast_touch", 6, "ft_06_01", 6)]
    [InlineData(35.0, 92.0, "fast_touch", 6, "ft_06_01", 6)]
    [InlineData(30.0, 95.0, "speed_building", 1, "sb_01_01", 13)]
    [InlineData(55.0, 99.0, "speed_building", 1, "sb_01_01", 13)]
    public void PlacementEngine_EvaluatesBoundaryThresholdsDeterministically(
        double netWpm,
        double accuracy,
        string expectedCourseId,
        int expectedUnit,
        string expectedExerciseId,
        int expectedStage)
    {
        var engine = new PlacementEngine();
        var rec = engine.Evaluate(netWpm, accuracy);

        Assert.Equal(expectedCourseId, rec.RecommendedCourseId);
        Assert.Equal(expectedUnit, rec.StartingUnitNumber);
        Assert.Equal(expectedExerciseId, rec.StartingExerciseId);
        Assert.Equal(expectedStage, rec.UnlockedAdaptiveStage);
    }

    [Fact]
    public void PlacementEngine_MalformedJson_FallsBackSafelyToLessonOneOnInvalidRules()
    {
        var engine = PlacementEngine.FromJsonSafe("{ invalid json ");
        var rec = engine.Evaluate(netWpm: 8.0, accuracyPercentage: 80.0);

        Assert.Equal("fast_touch", rec.RecommendedCourseId);
        Assert.Equal(1, rec.StartingUnitNumber);
        Assert.Equal("ft_01_02", rec.StartingExerciseId);
        Assert.Equal(1, rec.UnlockedAdaptiveStage);
    }

    [Fact]
    public void SessionPlanner_BuildsDailyPlan_AttachesPrecedingTipAndSkipsEmptyOrCompletedSteps()
    {
        var states = AdaptiveEngine.CreateInitialStates();
        var tracks = EmbeddedContentLoader.LoadCourseTracksSafe();
        var fastTouch = tracks.First(t => t.TrackId == "fast_touch");
        var dictionary = EmbeddedContentLoader.LoadDictionaryWords();
        var planner = new SessionPlanner();

        // 1. Brand-new user: Lesson 1 starts with ft_01_01 (Tip) followed by ft_01_02 (NewKeys drill).
        // Plan should include Warmup + CourseStep (ft_01_02 with CompanionTipExerciseId == "ft_01_01"),
        // and skip WeaknessReview & DueKeyReview when no stats/schedules exist.
        var newUserPlan = planner.BuildPlan(
            adaptiveStates: states,
            activeTrack: fastTouch,
            completedExerciseIds: new HashSet<string>(),
            keyMetrics: [],
            bigramMetrics: [],
            reviewSchedules: [],
            dictionaryPool: dictionary,
            dailyGoalMinutes: 15,
            rng: new Random(1));

        Assert.Equal(2, newUserPlan.Steps.Count);
        Assert.Equal(PlannedStepKind.Warmup, newUserPlan.Steps[0].Kind);
        Assert.Equal(PlannedStepKind.CourseStep, newUserPlan.Steps[1].Kind);
        Assert.Equal("ft_01_02", newUserPlan.Steps[1].CourseExerciseId);
        Assert.Equal("ft_01_01", newUserPlan.Steps[1].CompanionTipExerciseId);
        Assert.False(string.IsNullOrWhiteSpace(newUserPlan.Steps[1].TipBannerText));
        Assert.True(newUserPlan.TotalDuration <= TimeSpan.FromMinutes(15));

        // 2. User with weak keys, due reviews, and all course exercises completed:
        // CourseStep is skipped, while Warmup + WeaknessReview + DueKeyReview are included.
        var allCompleted = fastTouch.Units
            .SelectMany(u => u.Exercises)
            .Select(e => e.ExerciseId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var weakMetrics = new List<KeyMetric>
        {
            new('F', 40, 30, 10, 380),
            new('J', 40, 39, 1, 170)
        };

        var dueSchedules = new List<KeyReviewScheduleRecord>
        {
            new(
                Character: 'F',
                LastReviewedAt: new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero),
                IntervalDays: 1.0,
                EaseFactor: 2.0,
                DueAt: new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero))
        };

        var veteranPlan = planner.BuildPlan(
            adaptiveStates: states,
            activeTrack: fastTouch,
            completedExerciseIds: allCompleted,
            keyMetrics: weakMetrics,
            bigramMetrics: [],
            reviewSchedules: dueSchedules,
            dictionaryPool: dictionary,
            dailyGoalMinutes: 15,
            utcNow: new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc),
            rng: new Random(2));

        Assert.Equal(3, veteranPlan.Steps.Count);
        Assert.Equal(PlannedStepKind.Warmup, veteranPlan.Steps[0].Kind);
        Assert.Equal(PlannedStepKind.WeaknessReview, veteranPlan.Steps[1].Kind);
        Assert.Equal(PlannedStepKind.DueKeyReview, veteranPlan.Steps[2].Kind);
    }

    [Fact]
    public void FingerMap_MapsEveryStandardQwertyKeyToExactlyOneFinger_AndBuildsReachHint()
    {
        Assert.Equal(48, FingerMap.AllStandardQwertyKeys.Count);

        foreach (char key in FingerMap.AllStandardQwertyKeys)
        {
            Assert.True(FingerMap.TryGetFinger(key, out FingerId finger));
            Assert.True(Enum.IsDefined(finger));

            if (char.IsLetter(key))
            {
                char lower = char.ToLowerInvariant(key);
                Assert.Equal(finger, FingerMap.GetFinger(lower));
            }

            string hint = FingerMap.BuildReachHint(key);
            Assert.False(string.IsNullOrWhiteSpace(hint));
        }

        Assert.Equal(FingerId.LeftIndex, FingerMap.GetFinger('F'));
        Assert.Equal(FingerId.RightIndex, FingerMap.GetFinger('J'));
        Assert.Equal(FingerId.Thumb, FingerMap.GetFinger(' '));
        Assert.Equal(FingerId.LeftPinky, FingerMap.GetFinger('A'));
        Assert.Equal(FingerId.RightPinky, FingerMap.GetFinger(';'));
    }

    [Fact]
    public void CourseExercise_AccuracyFirstMode_HidesWpmOnEarlyLessonsUntilAccuracyTargetMetOnce()
    {
        var tracks = EmbeddedContentLoader.LoadCourseTracksSafe();
        var fastTouch = tracks.First(t => t.TrackId == "fast_touch");

        var unit1Drill = fastTouch.Units.First(u => u.UnitNumber == 1).Exercises.First(e => e.ExerciseId == "ft_01_02");
        var unit5Drill = fastTouch.Units.First(u => u.UnitNumber == 5).Exercises.First(e => e.Kind != CourseStepKind.Tip);

        // Early units (1-4) default ShowWpm = false; later units default ShowWpm = true
        Assert.False(unit1Drill.ShowWpm);
        Assert.True(unit5Drill.ShowWpm);

        // Before meeting accuracy target, WPM is hidden unless user overrides ShowWpmInCourse
        Assert.False(unit1Drill.ShouldDisplayWpm(userAlwaysShowWpmInCourse: false));
        Assert.True(unit1Drill.ShouldDisplayWpm(userAlwaysShowWpmInCourse: true));

        // Accuracy-first pass rule: early lesson passes on accuracy alone even with low WPM
        Assert.True(unit1Drill.EvaluatePass(netWpm: 6.0, accuracyPercentage: unit1Drill.MinimumAccuracy));

        // Once BestAccuracy meets MinimumAccuracy, WPM becomes visible on subsequent runs
        unit1Drill.BestAccuracy = unit1Drill.MinimumAccuracy;
        Assert.True(unit1Drill.ShouldDisplayWpm(userAlwaysShowWpmInCourse: false));
    }

    [Fact]
    public void CourseExercise_KeyboardFadeStage_FollowsProgressionAndRespectsUserOverride()
    {
        var tracks = EmbeddedContentLoader.LoadCourseTracksSafe();
        var fastTouch = tracks.First(t => t.TrackId == "fast_touch");

        var unit1NewKeys = fastTouch.Units.First(u => u.UnitNumber == 1).Exercises.First(e => e.Kind == CourseStepKind.NewKeys);
        var unit3WordDrill = fastTouch.Units.First(u => u.UnitNumber == 3).Exercises.First(e => e.Kind == CourseStepKind.WordDrill);
        var unit6Paragraph = fastTouch.Units.First(u => u.UnitNumber == 6).Exercises.First(e => e.Kind == CourseStepKind.ParagraphDrill);

        Assert.Equal(KeyboardFadeStage.FullLabels, unit1NewKeys.DefaultKeyboardFadeStage);
        Assert.Equal(KeyboardFadeStage.UnlabeledWithBumps, unit3WordDrill.DefaultKeyboardFadeStage);
        Assert.Equal(KeyboardFadeStage.Hidden, unit6Paragraph.DefaultKeyboardFadeStage);

        // User override to Hidden or UnlabeledWithBumps takes precedence over FullLabels
        Assert.Equal(
            KeyboardFadeStage.UnlabeledWithBumps,
            unit1NewKeys.GetEffectiveKeyboardFadeStage(KeyboardFadeStage.UnlabeledWithBumps));
        Assert.Equal(
            KeyboardFadeStage.Hidden,
            unit3WordDrill.GetEffectiveKeyboardFadeStage(KeyboardFadeStage.Hidden));
    }

    [Fact]
    public async Task UserSettings_FreshDatabaseDefaultsDailyGoalTo15Minutes_AndPreservesSavedGoalOnUpgrade()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"typeforge_goal_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDbPath);
            await repo.InitializeAsync();

            UserSettings fresh = await repo.GetSettingsAsync();
            Assert.Equal(15, fresh.DailyGoalMinutes);

            fresh.DailyGoalMinutes = 45;
            await repo.SaveSettingsAsync(fresh);

            UserSettings reloaded = await repo.GetSettingsAsync();
            Assert.Equal(45, reloaded.DailyGoalMinutes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public void PaceCaretCalculator_GetGhostIndex_AndResolveTargetWpm_FollowExactFormulaAndClampSafely()
    {
        // At 60 WPM: ms per char = 12000 / 60 = 200 ms/char -> 5 chars per second
        Assert.Equal(0, PaceCaretCalculator.GetGhostIndex(TimeSpan.Zero, 60.0, 100));
        Assert.Equal(5, PaceCaretCalculator.GetGhostIndex(TimeSpan.FromSeconds(1), 60.0, 100));
        Assert.Equal(30, PaceCaretCalculator.GetGhostIndex(TimeSpan.FromSeconds(6), 60.0, 100));

        // Clamps to textLength - 1
        Assert.Equal(99, PaceCaretCalculator.GetGhostIndex(TimeSpan.FromSeconds(60), 60.0, 100));

        // Guards against zero/negative inputs
        Assert.Equal(0, PaceCaretCalculator.GetGhostIndex(TimeSpan.FromSeconds(5), 0.0, 100));
        Assert.Equal(0, PaceCaretCalculator.GetGhostIndex(TimeSpan.FromSeconds(5), 60.0, 0));

        var sessions = new List<SessionResult>
        {
            new() { NetWpm = 52.4, AccuracyPercentage = 96.0 },
            new() { NetWpm = 74.8, AccuracyPercentage = 98.0 }
        };

        Assert.Equal(0.0, PaceCaretCalculator.ResolveTargetWpm(PaceCaretMode.Off, 60, sessions));
        Assert.Equal(65.0, PaceCaretCalculator.ResolveTargetWpm(PaceCaretMode.FixedWpm, 65, sessions));
        Assert.Equal(74.8, PaceCaretCalculator.ResolveTargetWpm(PaceCaretMode.PersonalBest, 60, sessions));
        Assert.Equal(52.4, PaceCaretCalculator.ResolveTargetWpm(PaceCaretMode.LastSession, 60, sessions));
    }

    [Fact]
    public void AdaptiveEngine_GenerateBigramDrillText_TargetsSlowestPairsAndOmitsLockedKeys()
    {
        var unlocked = new HashSet<char> { 'T', 'H', 'E', 'R', 'A', 'N', 'I', 'S' };
        var bigrams = new Dictionary<string, BigramMetric>
        {
            ["TH"] = new BigramMetric("TH", TotalCount: 30, ErrorCount: 6, AverageLatencyMs: 380),
            ["HE"] = new BigramMetric("HE", TotalCount: 30, ErrorCount: 4, AverageLatencyMs: 340),
            ["AN"] = new BigramMetric("AN", TotalCount: 30, ErrorCount: 0, AverageLatencyMs: 150)
        };
        var dictionary = new List<string> { "the", "then", "than", "their", "thin", "rain", "near", "star", "water" };

        string drill = AdaptiveEngine.GenerateBigramDrillText(
            unlocked,
            bigrams,
            dictionary,
            wordCount: 80,
            random: new Random(7));

        string[] words = drill.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(80, words.Length);

        int matchingBigramWords = 0;
        foreach (string w in words)
        {
            Assert.DoesNotContain("w", w, StringComparison.OrdinalIgnoreCase);
            if (w.Contains("th", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("he", StringComparison.OrdinalIgnoreCase))
            {
                matchingBigramWords++;
            }
        }

        Assert.True(matchingBigramWords >= 40);
    }

    [Fact]
    public void FatigueDetector_TriggersAfterTwoConsecutiveDropWindowsPastThreeMinutes_OrAtTwentyMinutes_AndFiresOncePerSession()
    {
        var detector = new FatigueDetector();

        // Window 1 (30 strokes at 100% accuracy) establishes 100% baseline
        for (int i = 0; i < 30; i++)
        {
            Assert.False(detector.RecordKeystroke(isCorrect: true, elapsed: TimeSpan.FromSeconds(45)));
        }
        Assert.Equal(100.0, detector.BaselineAccuracy);

        // Window 2 & 3 at 80% accuracy (24/30 correct -> 20pt drop) BEFORE 3 minutes elapsed -> does NOT trigger yet
        for (int w = 0; w < 2; w++)
        {
            for (int i = 0; i < 30; i++)
            {
                bool ok = i < 24;
                Assert.False(detector.RecordKeystroke(ok, elapsed: TimeSpan.FromMinutes(2)));
            }
        }

        // Next window at 80% accuracy AFTER 3 minutes elapsed -> triggers once!
        bool triggered = false;
        for (int i = 0; i < 30; i++)
        {
            bool ok = i < 24;
            if (detector.RecordKeystroke(ok, elapsed: TimeSpan.FromMinutes(3.5)))
            {
                triggered = true;
            }
        }

        Assert.True(triggered);
        Assert.True(detector.IsBreakSuggested);
        Assert.True(detector.HasTriggeredThisSession);

        // Dismissing hides the banner and subsequent fatigued windows in the same session never re-trigger
        detector.Dismiss();
        Assert.False(detector.IsBreakSuggested);
        for (int i = 0; i < 60; i++)
        {
            Assert.False(detector.RecordKeystroke(isCorrect: false, elapsed: TimeSpan.FromMinutes(5)));
        }

        // Resetting for a new session allows the 20-minute cap to trigger
        detector.Reset();
        Assert.True(detector.EvaluateElapsed(TimeSpan.FromMinutes(20)));
        Assert.True(detector.IsBreakSuggested);
    }

    [Fact]
    public void InsightEngine_SurfacesPrimaryInsightAndAction_AndNeutralFallbackWhenDataIsSparse()
    {
        var sparseSession = new SessionResult
        {
            SessionId = Guid.NewGuid(),
            NetWpm = 42.0,
            AccuracyPercentage = 97.5,
            TotalKeystrokes = 100
        };

        // 1. Sparse history -> neutral summary fallback
        SessionInsight fallback = InsightEngine.Evaluate(sparseSession);
        Assert.True(fallback.IsNeutralFallback);
        Assert.Equal(InsightActionKind.NextDrill, fallback.RecommendedAction);

        // 2. Slow/error-prone bigram -> recommends DrillSlowestBigrams
        var bigrams = new Dictionary<string, BigramMetric>
        {
            ["TH"] = new BigramMetric("TH", TotalCount: 30, ErrorCount: 5, AverageLatencyMs: 360),
            ["HE"] = new BigramMetric("HE", TotalCount: 30, ErrorCount: 0, AverageLatencyMs: 170)
        };
        SessionInsight bigramInsight = InsightEngine.Evaluate(sparseSession, aggregateBigrams: bigrams);
        Assert.False(bigramInsight.IsNeutralFallback);
        Assert.Equal(InsightActionKind.DrillSlowestBigrams, bigramInsight.RecommendedAction);
        Assert.Contains("TH", bigramInsight.PrimaryInsight);

        // 3. Low accuracy session (< 90%) -> coaches precision first
        var lowAccSession = new SessionResult
        {
            SessionId = Guid.NewGuid(),
            NetWpm = 48.0,
            AccuracyPercentage = 85.0,
            TotalKeystrokes = 80,
            KeyBreakdown = new Dictionary<char, KeyMetric>
            {
                ['R'] = new KeyMetric('R', 15, 9, 6, 310)
            }
        };
        SessionInsight lowAccInsight = InsightEngine.Evaluate(lowAccSession);
        Assert.False(lowAccInsight.IsNeutralFallback);
        Assert.Equal(InsightActionKind.ReviewWeakKeys, lowAccInsight.RecommendedAction);
    }

    [Fact]
    public void EmbeddedContentLoader_LoadRealWorldPools_ValidatesFourPools_AndRejectsMalformedJson()
    {
        IReadOnlyList<RealWorldPoolModel> pools = EmbeddedContentLoader.LoadRealWorldPools();
        Assert.Equal(4, pools.Count);
        Assert.Contains(pools, p => p.Id == "quotes" && p.Title == "Quotes");
        Assert.Contains(pools, p => p.Id == "code_snippets" && p.Title == "Code snippets");
        Assert.Contains(pools, p => p.Id == "numbers" && p.Title == "Numbers");
        Assert.Contains(pools, p => p.Id == "punctuation" && p.Title == "Punctuation");

        foreach (RealWorldPoolModel pool in pools)
        {
            Assert.NotEmpty(pool.Items);
            string generated = TestTextGenerator.GenerateRealWorldPoolPrompt(pool, targetCharCount: 200, rng: new Random(1));
            Assert.True(generated.Length >= 200);
        }

        Assert.Throws<InvalidDataException>(() => EmbeddedContentLoader.ParseRealWorldPools("{ bad json"));
        Assert.Throws<InvalidDataException>(() => EmbeddedContentLoader.ParseRealWorldPools("{\"pools\":[]}"));
    }

    [Fact]
    public async Task SpacedReviewHook_PersistsKeyReviewSchedule_AndFeedsDueKeysIntoSessionPlanner()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"typeforge_review_hook_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDb);
            await repo.InitializeAsync();

            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));
            var scheduler = new ReviewScheduler(clock);

            // Simulate post-session review evaluation for practiced keys 'F' (failed) and 'J' (mastered)
            var metricF = new KeyMetric('F', 20, 15, 5, 620);
            var metricJ = new KeyMetric('J', 20, 20, 0, 280);

            KeyReviewScheduleRecord updatedF = scheduler.EvaluateKeySession(null, 'F', metricF, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);
            KeyReviewScheduleRecord updatedJ = scheduler.EvaluateKeySession(null, 'J', metricJ, targetAccuracyPercentage: 95.0, maxLatencyMs: 500.0);

            await repo.SaveKeyReviewSchedulesAsync([updatedF, updatedJ]);

            // Advance clock 13 hours so 'F' (12-hour failure interval) is due while 'J' (24-hour mastery interval) is not due
            clock.Advance(TimeSpan.FromHours(13));

            var persisted = await repo.GetKeyReviewSchedulesAsync();
            var states = AdaptiveEngine.CreateInitialStates();
            var tracks = EmbeddedContentLoader.LoadCourseTracksSafe();
            var fastTouch = tracks.First(t => t.TrackId == "fast_touch");
            var dictionary = EmbeddedContentLoader.LoadDictionaryWords();
            var planner = new SessionPlanner();

            var plan = planner.BuildPlan(
                adaptiveStates: states,
                activeTrack: fastTouch,
                completedExerciseIds: new HashSet<string>(),
                keyMetrics: [],
                bigramMetrics: [],
                reviewSchedules: persisted.Values.ToArray(),
                dictionaryPool: dictionary,
                dailyGoalMinutes: 15,
                utcNow: clock.GetUtcNow().UtcDateTime,
                rng: new Random(3));

            var dueStep = plan.Steps.FirstOrDefault(s => s.Kind == PlannedStepKind.DueKeyReview);
            Assert.NotNull(dueStep);
            Assert.Contains("F", dueStep!.Subtitle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("J", dueStep.Subtitle, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Fact]
    public void ThemePaletteTokens_VerifyContrastThresholds_InDarkAndLight_AndMatchAppXaml()
    {
        IReadOnlyList<string> darkViolations = ThemePaletteTokens.ValidateContrastThresholds(ThemePaletteTokens.Dark);
        IReadOnlyList<string> lightViolations = ThemePaletteTokens.ValidateContrastThresholds(ThemePaletteTokens.Light);

        Assert.Empty(darkViolations);
        Assert.Empty(lightViolations);

        // Verify disabled/locked >= 3.0:1 and muted >= 4.5:1 explicitly against Card and Canvas in both themes
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Dark.TextMutedHex, ThemePaletteTokens.Dark.CardHex) >= 4.5);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Dark.TextMutedHex, ThemePaletteTokens.Dark.CanvasHex) >= 4.5);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Dark.TextDisabledHex, ThemePaletteTokens.Dark.CardHex) >= 3.0);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Dark.TextDisabledHex, ThemePaletteTokens.Dark.CanvasHex) >= 3.0);

        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Light.TextMutedHex, ThemePaletteTokens.Light.CardHex) >= 4.5);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Light.TextMutedHex, ThemePaletteTokens.Light.CanvasHex) >= 4.5);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Light.TextDisabledHex, ThemePaletteTokens.Light.CardHex) >= 3.0);
        Assert.True(ThemePaletteTokens.CalculateContrastRatio(ThemePaletteTokens.Light.TextDisabledHex, ThemePaletteTokens.Light.CanvasHex) >= 3.0);
    }

    [Fact]
    public void StatisticsAggregator_BuildWeeklyTrend_ComputesSevenChronologicalDays_WithAveragesAndEmptyDays()
    {
        var today = new DateOnly(2026, 11, 12);
        var sessions = new List<SessionResult>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                StartedAt = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero),
                NetWpm = 40.0,
                AccuracyPercentage = 96.0
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                StartedAt = new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero),
                NetWpm = 50.0,
                AccuracyPercentage = 98.0
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                StartedAt = new DateTimeOffset(2026, 11, 12, 11, 0, 0, TimeSpan.Zero),
                NetWpm = 55.0,
                AccuracyPercentage = 99.0
            }
        };

        IReadOnlyList<DailyTrendPoint> trend = StatisticsAggregator.BuildWeeklyTrend(sessions, today);
        Assert.Equal(7, trend.Count);
        Assert.Equal(new DateOnly(2026, 11, 6), trend[0].Date);
        Assert.Equal(today, trend[6].Date);

        // Day with 0 sessions
        Assert.Equal(0, trend[0].SessionCount);
        Assert.Equal(0.0, trend[0].AverageNetWpm);

        // 2026-11-10 has 2 sessions (40 & 50 WPM -> 45.0 WPM; 96% & 98% -> 97.0%)
        DailyTrendPoint nov10 = trend.Single(p => p.Date == new DateOnly(2026, 11, 10));
        Assert.Equal(2, nov10.SessionCount);
        Assert.Equal(45.0, nov10.AverageNetWpm);
        Assert.Equal(97.0, nov10.AverageAccuracyPercentage);

        // Today (2026-11-12) has 1 session
        Assert.Equal(1, trend[6].SessionCount);
        Assert.Equal(55.0, trend[6].AverageNetWpm);
        Assert.Equal(99.0, trend[6].AverageAccuracyPercentage);
    }

    [Fact]
    public async Task SqliteMigrationV1ToV3_RealLegacyDatabase_PreservesAllUserDataAndUpgradesToV3()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"typeforge_v1_upgrade_{Guid.NewGuid():N}.db");
        try
        {
            await using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    """
                    PRAGMA user_version = 1;
                    CREATE TABLE Lessons (
                        LessonId TEXT PRIMARY KEY,
                        LevelId INTEGER NOT NULL,
                        Title TEXT NOT NULL,
                        Description TEXT NOT NULL,
                        Content TEXT NOT NULL,
                        MinimumWpm INTEGER NOT NULL,
                        MinimumAccuracy REAL NOT NULL,
                        OrderIndex INTEGER NOT NULL
                    );
                    CREATE TABLE LessonProgress (
                        LessonId TEXT PRIMARY KEY,
                        IsUnlocked INTEGER NOT NULL,
                        IsCompleted INTEGER NOT NULL,
                        BestWpm REAL NOT NULL,
                        BestAccuracy REAL NOT NULL,
                        Stars INTEGER NOT NULL,
                        CompletedAt TEXT NULL
                    );
                    CREATE TABLE PracticeSessions (
                        SessionId TEXT PRIMARY KEY,
                        SessionType TEXT NOT NULL,
                        LessonId TEXT NULL,
                        StartedAt TEXT NOT NULL,
                        DurationSeconds REAL NOT NULL,
                        GrossWpm REAL NOT NULL,
                        NetWpm REAL NOT NULL,
                        Accuracy REAL NOT NULL,
                        CorrectCharacters INTEGER NOT NULL,
                        TotalKeystrokes INTEGER NOT NULL,
                        Mistakes INTEGER NOT NULL,
                        UncorrectedErrors INTEGER NOT NULL
                    );
                    CREATE TABLE KeyStatistics (
                        Character TEXT PRIMARY KEY,
                        TotalKeystrokes INTEGER NOT NULL,
                        CorrectKeystrokes INTEGER NOT NULL,
                        IncorrectKeystrokes INTEGER NOT NULL,
                        AverageLatencyMs REAL NOT NULL
                    );
                    CREATE TABLE AdaptiveProgress (
                        Character TEXT PRIMARY KEY,
                        IsUnlocked INTEGER NOT NULL,
                        StageIndex INTEGER NOT NULL
                    );
                    CREATE TABLE Settings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL
                    );

                    INSERT INTO Settings (Key, Value) VALUES ('DailyGoalMinutes', '45');
                    INSERT INTO KeyStatistics (Character, TotalKeystrokes, CorrectKeystrokes, IncorrectKeystrokes, AverageLatencyMs)
                    VALUES ('F', 120, 115, 5, 195.5);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var repo = new SqliteTypeForgeRepository(tempDbPath);
            await repo.InitializeAsync();

            var settings = await repo.GetSettingsAsync();
            Assert.Equal(45, settings.DailyGoalMinutes);

            var keyStats = await repo.GetKeyStatisticsAsync();
            Assert.True(keyStats.TryGetValue('F', out var fMetric));
            Assert.Equal(120, fMetric!.TotalKeystrokes);

            await using (var verifyConn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await verifyConn.OpenAsync();
                await using var vCmd = verifyConn.CreateCommand();
                vCmd.CommandText = "PRAGMA user_version;";
                long version = (long)(await vCmd.ExecuteScalarAsync())!;
                Assert.Equal(3, (int)version);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public void CoursePassGate_BlocksBelowMinAccuracyOrAboveMaxMistakes_AndRespectsConfig()
    {
        var exercise = new CourseExercise(
            "ft_01_02",
            "fast_touch",
            1,
            2,
            "1.2 Home Row",
            CourseStepKind.NewKeys,
            "2 min",
            "Focus on accuracy",
            "fff jjj ddd kkk",
            minimumNetWpm: 10.0,
            minimumAccuracy: 85.0);

        // Gate enabled (90% min accuracy, 5 max mistakes):
        // Fails when accuracy < 90% even with few mistakes
        Assert.False(exercise.EvaluatePass(25.0, 89.9, mistakes: 2, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));
        Assert.Equal(0, exercise.CalculateStars(25.0, 89.9, mistakes: 2, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));

        // Fails when mistakes > 5 even with high accuracy
        Assert.False(exercise.EvaluatePass(25.0, 95.0, mistakes: 6, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));
        Assert.Equal(0, exercise.CalculateStars(25.0, 95.0, mistakes: 6, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));

        // Passes at exact boundary (90.0% and 5 mistakes)
        Assert.True(exercise.EvaluatePass(25.0, 90.0, mistakes: 5, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));
        Assert.True(exercise.CalculateStars(25.0, 90.0, mistakes: 5, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5) >= 1);

        // Stricter lesson requirement (95%) still enforced when settingsMinAccuracy is 90%
        var strictExercise = new CourseExercise(
            "ft_05_02",
            "fast_touch",
            5,
            2,
            "5.2 Precision",
            CourseStepKind.KeyDrill,
            "3 min",
            "",
            "asdf jkl;",
            minimumNetWpm: 15.0,
            minimumAccuracy: 95.0);
        Assert.False(strictExercise.EvaluatePass(30.0, 92.0, mistakes: 1, passGateEnabled: true, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));

        // Gate disabled: falls back to lesson MinimumAccuracy (85%) and ignores mistake count
        Assert.True(exercise.EvaluatePass(25.0, 86.0, mistakes: 8, passGateEnabled: false, settingsMinAccuracy: 90.0, settingsMaxMistakes: 5));
    }

    [Fact]
    public async Task CoursePassGateSettings_RoundTripThroughSqlite()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"tf_gate_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDbPath);
            await repo.InitializeAsync();

            var settings = await repo.GetSettingsAsync();
            Assert.True(settings.CoursePassGateEnabled);
            Assert.Equal(90.0, settings.CourseMinAccuracy);
            Assert.Equal(5, settings.CourseMaxMistakes);

            settings.CoursePassGateEnabled = false;
            settings.CourseMinAccuracy = 94.0;
            settings.CourseMaxMistakes = 2;
            await repo.SaveSettingsAsync(settings);

            var reloaded = await repo.GetSettingsAsync();
            Assert.False(reloaded.CoursePassGateEnabled);
            Assert.Equal(94.0, reloaded.CourseMinAccuracy);
            Assert.Equal(2, reloaded.CourseMaxMistakes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public void FinalLessonExercise_Requires100PercentOnAllUnitFocusKeys()
    {
        var ex1 = new CourseExercise("ft_01_02", "fast_touch", 1, 1, "Intro", CourseStepKind.NewKeys, "2m", "", "fff jjj", 10, 85);
        var exFinal = new CourseExercise("ft_01_05", "fast_touch", 1, 2, "Final", CourseStepKind.ParagraphDrill, "3m", "", "asdf jkl; sad dad", 15, 90);
        var unit = new CourseUnit(1, "Home Row", "asdfjkl;", "Keep fingers on home row", [ex1, exFinal]);

        Assert.False(unit.IsFinalExercise(ex1));
        Assert.True(unit.IsFinalExercise(exFinal));
        Assert.Equal(['a', 's', 'd', 'f', 'j', 'k', 'l', ';'], unit.GetFocusKeyCharacters());

        var breakdownWithFocusMiss = new Dictionary<char, KeyMetric>
        {
            ['a'] = new KeyMetric('a', 10, 10, 0, 120),
            ['s'] = new KeyMetric('s', 10, 10, 0, 120),
            ['d'] = new KeyMetric('d', 10, 10, 0, 120),
            ['f'] = new KeyMetric('f', 10, 9, 1, 120),
            ['j'] = new KeyMetric('j', 10, 10, 0, 120),
            ['k'] = new KeyMetric('k', 10, 10, 0, 120),
            ['l'] = new KeyMetric('l', 10, 10, 0, 120),
            [';'] = new KeyMetric(';', 10, 10, 0, 120)
        };

        IReadOnlyList<char> missed = CourseUnit.EvaluateMissedFocusKeys(unit, exFinal, breakdownWithFocusMiss);
        Assert.Equal(['f'], missed);

        var cleanBreakdown = new Dictionary<char, KeyMetric>(breakdownWithFocusMiss)
        {
            ['f'] = new KeyMetric('f', 10, 10, 0, 120)
        };
        Assert.Empty(CourseUnit.EvaluateMissedFocusKeys(unit, exFinal, cleanBreakdown));
    }

    [Fact]
    public void BigramDrillGenerator_StrictlyOmitsUnlearnedLetters()
    {
        var unlockedHomeRow = new HashSet<char> { 'A', 'S', 'D', 'F', 'J', 'K', 'L' };
        var bigrams = new Dictionary<string, BigramMetric>
        {
            ["fj"] = new BigramMetric("fj", 30, 2, 420.0),
            ["dk"] = new BigramMetric("dk", 30, 1, 380.0),
            ["th"] = new BigramMetric("th", 30, 10, 900.0) // 't' and 'h' are locked
        };

        string drill = AdaptiveEngine.GenerateBigramDrillText(
            unlockedHomeRow,
            bigrams,
            dictionaryPool: ["the", "hall", "sad", "ask", "lad", "fall", "flask", "salad"],
            wordCount: 40,
            random: new Random(42));

        Assert.False(string.IsNullOrWhiteSpace(drill));
        foreach (char ch in drill)
        {
            if (ch == ' ')
            {
                continue;
            }

            Assert.Contains(char.ToUpperInvariant(ch), unlockedHomeRow);
        }
    }

    [Theory]
    [InlineData(false, "fff jjj ddd", false)]
    [InlineData(true, "fff jjj ddd", true)]
    [InlineData(true, "Hello world", false)]
    [InlineData(true, "4567 1230 !@#", false)]
    [InlineData(true, "", false)]
    [InlineData(true, null, false)]
    public void CapsLockGuard_WarnsOnlyWhenCapsOnAndPromptRequiresLowercaseOnly(
        bool isCapsOn,
        string? prompt,
        bool expected)
    {
        Assert.Equal(expected, CapsLockGuard.ShouldShowWarning(isCapsOn, prompt));
    }

    [Fact]
    public void CalculateConsistencyPercentage_ReturnsHundredForEmptyOrSingleSample()
    {
        Assert.Equal(100.0, MetricsCalculator.CalculateConsistencyPercentage(Array.Empty<double>()));
        Assert.Equal(100.0, MetricsCalculator.CalculateConsistencyPercentage(new[] { 150.0 }));
        Assert.Equal(100.0, MetricsCalculator.CalculateConsistencyPercentage(new[] { 150.0, 2500.0 }));
    }

    [Fact]
    public void CalculateConsistencyPercentage_SteadyBeatsErraticAndIgnoresOutlierPauses()
    {
        var steady = MetricsCalculator.CalculateConsistencyPercentage(new[] { 120.0, 122.0, 118.0, 121.0, 5000.0 });
        var erratic = MetricsCalculator.CalculateConsistencyPercentage(new[] { 50.0, 450.0, 60.0, 480.0 });

        Assert.True(steady > 95.0);
        Assert.True(erratic < 50.0);
        Assert.True(steady > erratic);
    }

    [Fact]
    public void TypingEngine_GetMissedWordsAndBuildMissedWordsDrillText_TracksDistinctMistakenWords()
    {
        var engine = new TypingEngine("alpha beta gamma");

        // Type "alpha " cleanly
        foreach (var ch in "alpha ")
        {
            engine.ProcessKeystroke(ch);
        }

        // Miss 'b' in "beta", then fix and finish "beta "
        engine.ProcessKeystroke('x');
        engine.ProcessBackspace();
        foreach (var ch in "beta ")
        {
            engine.ProcessKeystroke(ch);
        }

        // Type "gamma" cleanly
        foreach (var ch in "gamma")
        {
            engine.ProcessKeystroke(ch);
        }

        var missed = engine.GetMissedWords();
        Assert.Single(missed);
        Assert.Equal("beta", missed[0]);

        var drill = TypingEngine.BuildMissedWordsDrillText(missed, targetWordCount: 5);
        Assert.Equal("beta beta beta beta beta", drill);
        Assert.Equal(string.Empty, TypingEngine.BuildMissedWordsDrillText(Array.Empty<string>()));
    }

    [Fact]
    public async Task ProfileService_CreatesDefaultAndMigratesLegacyDatabase()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"tf_prof_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            string legacyDb = Path.Combine(tempDir, "typeforge.db");
            var legacyRepo = new SqliteTypeForgeRepository(legacyDb);
            await legacyRepo.InitializeAsync();
            await legacyRepo.SaveSettingsAsync(new UserSettings { DailyGoalMinutes = 35 });

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var service = new ProfileService(tempDir);
            service.Initialize();

            Assert.Single(service.Profiles);
            Assert.Equal("default", service.ActiveProfile.Id);
            Assert.False(File.Exists(legacyDb));
            Assert.True(File.Exists(service.GetDatabasePath("default")));

            var migratedRepo = service.CreateRepositoryForActiveProfile();
            UserSettings loaded = await migratedRepo.GetSettingsAsync();
            Assert.Equal(35, loaded.DailyGoalMinutes);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProfileService_CreateRenameDelete_AndBuildRankings()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"tf_rank_{Guid.NewGuid():N}");
        try
        {
            var service = new ProfileService(tempDir);
            service.Initialize();

            UserProfile alex = service.CreateProfile("Alex", "#34D399");
            Assert.Equal(2, service.Profiles.Count);

            service.RenameProfile(alex.Id, "Alex M");
            Assert.Equal("Alex M", service.Profiles.First(p => p.Id == alex.Id).Name);

            var defaultRepo = new SqliteTypeForgeRepository(service.GetDatabasePath("default"));
            await defaultRepo.InitializeAsync();
            await defaultRepo.RecordPracticeSessionAsync(new SessionResult
            {
                SessionType = SessionType.AdaptivePractice,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                Duration = TimeSpan.FromMinutes(1),
                GrossWpm = 45.0,
                NetWpm = 42.0,
                AccuracyPercentage = 95.0,
                CorrectCharacters = 38,
                TotalKeystrokes = 40,
                Mistakes = 2,
                UncorrectedErrors = 0
            });

            var alexRepo = new SqliteTypeForgeRepository(service.GetDatabasePath(alex.Id));
            await alexRepo.InitializeAsync();
            await alexRepo.RecordPracticeSessionAsync(new SessionResult
            {
                SessionType = SessionType.AdaptivePractice,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                Duration = TimeSpan.FromMinutes(1),
                GrossWpm = 72.0,
                NetWpm = 70.0,
                AccuracyPercentage = 98.0,
                CorrectCharacters = 69,
                TotalKeystrokes = 70,
                Mistakes = 1,
                UncorrectedErrors = 0
            });

            IReadOnlyList<ProfileRankingEntry> rankings = await service.BuildRankingsAsync(totalCourseExercises: 10);
            Assert.Equal(2, rankings.Count);
            Assert.Equal(alex.Id, rankings[0].Profile.Id);
            Assert.Equal(1, rankings[0].Rank);
            Assert.Equal(70.0, rankings[0].AverageNetWpm);
            Assert.Equal("default", rankings[1].Profile.Id);
            Assert.Equal(2, rankings[1].Rank);

            service.DeleteProfile(alex.Id);
            Assert.Single(service.Profiles);
            Assert.False(File.Exists(service.GetDatabasePath(alex.Id)));
            Assert.Throws<InvalidOperationException>(() => service.DeleteProfile("default"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SqliteTypeForgeRepository_ResetProgressAsync_ClearsProgressButPreservesSettings()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"tf_reset_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDb);
            await repo.InitializeAsync();
            await repo.SaveSettingsAsync(new UserSettings { DailyGoalMinutes = 45 });
            await repo.RecordPracticeSessionAsync(new SessionResult
            {
                SessionType = SessionType.AdaptivePractice,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                Duration = TimeSpan.FromMinutes(1),
                GrossWpm = 55.0,
                NetWpm = 52.0,
                AccuracyPercentage = 96.0,
                CorrectCharacters = 48,
                TotalKeystrokes = 50,
                Mistakes = 2,
                UncorrectedErrors = 0
            });

            Assert.Single(await repo.GetRecentSessionsAsync());

            await repo.ResetProgressAsync();

            Assert.Empty(await repo.GetRecentSessionsAsync());
            UserSettings preserved = await repo.GetSettingsAsync();
            Assert.Equal(45, preserved.DailyGoalMinutes);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Theory]
    [InlineData(MetronomeTargetMode.Fixed, 50, 80.0, 50)]
    [InlineData(MetronomeTargetMode.Auto, 40, 0.0, 40)]
    [InlineData(MetronomeTargetMode.Auto, 40, 62.4, 67)]
    [InlineData(MetronomeTargetMode.Auto, 40, 150.0, 120)]
    [InlineData(MetronomeTargetMode.Fixed, 5, 0.0, 20)]
    public void MetronomeCalculator_ResolveTargetWpm_HandlesFixedAutoAndClamping(
        MetronomeTargetMode mode,
        int fixedWpm,
        double avgWpm,
        int expected)
    {
        Assert.Equal(expected, MetronomeCalculator.ResolveTargetWpm(mode, fixedWpm, avgWpm));
    }

    [Theory]
    [InlineData(60, MetronomeBeatMode.PerKeystroke, 200.0)]
    [InlineData(40, MetronomeBeatMode.PerKeystroke, 300.0)]
    [InlineData(60, MetronomeBeatMode.PerWord, 1000.0)]
    [InlineData(30, MetronomeBeatMode.PerWord, 2000.0)]
    public void MetronomeCalculator_GetBeatIntervalMs_ComputesCharAndWordBeats(
        int targetWpm,
        MetronomeBeatMode beatMode,
        double expectedMs)
    {
        Assert.Equal(expectedMs, MetronomeCalculator.GetBeatIntervalMs(targetWpm, beatMode), precision: 3);
    }

    [Fact]
    public async Task SqliteTypeForgeRepository_PersistsMetronomeSettings()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"tf_met_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteTypeForgeRepository(tempDb);
            await repo.InitializeAsync();

            var settings = new UserSettings
            {
                MetronomeEnabled = true,
                MetronomeBeatMode = MetronomeBeatMode.PerWord,
                MetronomeTargetMode = MetronomeTargetMode.Auto,
                MetronomeTargetWpm = 75,
                MetronomeAudioEnabled = false,
                MetronomeVisualEnabled = true
            };
            await repo.SaveSettingsAsync(settings);

            UserSettings loaded = await repo.GetSettingsAsync();
            Assert.True(loaded.MetronomeEnabled);
            Assert.Equal(MetronomeBeatMode.PerWord, loaded.MetronomeBeatMode);
            Assert.Equal(MetronomeTargetMode.Auto, loaded.MetronomeTargetMode);
            Assert.Equal(75, loaded.MetronomeTargetWpm);
            Assert.False(loaded.MetronomeAudioEnabled);
            Assert.True(loaded.MetronomeVisualEnabled);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }
}
