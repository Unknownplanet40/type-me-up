using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Represents a curated benchmark passage for fixed-duration timed tests.
/// </summary>
/// <param name="Id">Unique passage identifier.</param>
/// <param name="Title">Display title of the passage.</param>
/// <param name="Text">Clean passage prompt text.</param>
public sealed record TimedPassage(string Id, string Title, string Text);

/// <summary>
/// Loads and validates embedded curriculum, timed test, and dictionary JSON resources.
/// </summary>
public static class EmbeddedContentLoader
{
    private const string CurriculumResourceName = "TypeForge.Core.Resources.curriculum.json";
    private const string CoursesResourceName = "TypeForge.Core.Resources.courses.json";
    private const string PassagesResourceName = "TypeForge.Core.Resources.timed_passages.json";
    private const string DictionaryResourceName = "TypeForge.Core.Resources.english_words.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string[] FallbackDictionaryWords =
    [
        "a", "as", "ask", "all", "dad", "sad", "lad", "fall", "hall", "flask",
        "feel", "like", "side", "life", "rule", "true", "user", "line", "fast", "last",
        "mind", "word", "work", "type", "flow", "calm", "home", "read", "lead", "time"
    ];

    /// <summary>
    /// Loads and strictly validates the embedded progressive typing curriculum.
    /// </summary>
    public static IReadOnlyList<CourseLevel> LoadCurriculum()
    {
        string json = ReadEmbeddedText(CurriculumResourceName);
        return ParseCurriculum(json);
    }

    /// <summary>
    /// Parses and validates curriculum JSON, throwing <see cref="InvalidDataException"/> on malformed or invalid content.
    /// </summary>
    public static IReadOnlyList<CourseLevel> ParseCurriculum(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Curriculum JSON cannot be empty.");
        }

        CurriculumDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<CurriculumDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed curriculum JSON.", ex);
        }

        if (doc?.Levels is null || doc.Levels.Count == 0)
        {
            throw new InvalidDataException("Curriculum must contain at least one course level.");
        }

        var levels = new List<CourseLevel>(doc.Levels.Count);
        var seenLevelIds = new HashSet<int>();
        var seenLessonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LevelDto levelDto in doc.Levels.OrderBy(l => l.LevelId))
        {
            if (levelDto.LevelId <= 0 || !seenLevelIds.Add(levelDto.LevelId))
            {
                throw new InvalidDataException($"Invalid or duplicate levelId '{levelDto.LevelId}'.");
            }

            if (string.IsNullOrWhiteSpace(levelDto.Title))
            {
                throw new InvalidDataException($"Level {levelDto.LevelId} title cannot be empty.");
            }

            if (levelDto.Lessons is null || levelDto.Lessons.Count == 0)
            {
                throw new InvalidDataException($"Level {levelDto.LevelId} must contain at least one lesson.");
            }

            var lessons = new List<LessonModel>(levelDto.Lessons.Count);
            foreach (LessonDto lessonDto in levelDto.Lessons.OrderBy(l => l.OrderIndex))
            {
                if (string.IsNullOrWhiteSpace(lessonDto.LessonId) || !seenLessonIds.Add(lessonDto.LessonId))
                {
                    throw new InvalidDataException($"Invalid or duplicate lessonId '{lessonDto.LessonId}'.");
                }

                if (lessonDto.OrderIndex <= 0 ||
                    string.IsNullOrWhiteSpace(lessonDto.Title) ||
                    string.IsNullOrWhiteSpace(lessonDto.PracticeText) ||
                    lessonDto.MinimumNetWpm < 0 ||
                    lessonDto.MinimumAccuracy < 0 ||
                    lessonDto.MinimumAccuracy > 100)
                {
                    throw new InvalidDataException($"Lesson '{lessonDto.LessonId}' has invalid metadata or thresholds.");
                }

                bool isFirstLesson = levelDto.LevelId == 1 && lessons.Count == 0;
                lessons.Add(new LessonModel(
                    lessonId: lessonDto.LessonId,
                    levelId: levelDto.LevelId,
                    orderIndex: lessonDto.OrderIndex,
                    title: lessonDto.Title,
                    description: lessonDto.Description ?? string.Empty,
                    targetCharacters: lessonDto.TargetCharacters ?? string.Empty,
                    practiceText: lessonDto.PracticeText,
                    difficulty: string.IsNullOrWhiteSpace(lessonDto.Difficulty) ? "Standard" : lessonDto.Difficulty,
                    minimumNetWpm: lessonDto.MinimumNetWpm,
                    minimumAccuracy: lessonDto.MinimumAccuracy,
                    isUnlocked: isFirstLesson));
            }

            levels.Add(new CourseLevel(
                levelDto.LevelId,
                levelDto.Title,
                levelDto.Description ?? string.Empty,
                levelDto.ErgonomicTip ?? string.Empty,
                lessons));
        }

        return levels;
    }

    /// <summary>
    /// Parses curriculum JSON defensively, falling back to a minimal starter level on malformed input.
    /// </summary>
    public static IReadOnlyList<CourseLevel> ParseCurriculumSafe(string json)
    {
        try
        {
            return ParseCurriculum(json);
        }
        catch (InvalidDataException)
        {
            return CreateFallbackCurriculum();
        }
        catch (ArgumentException)
        {
            return CreateFallbackCurriculum();
        }
    }

    /// <summary>
    /// Loads and validates embedded benchmark passages for timed typing tests.
    /// </summary>
    public static IReadOnlyList<TimedPassage> LoadTimedPassages()
    {
        string json = ReadEmbeddedText(PassagesResourceName);
        return ParseTimedPassages(json);
    }

    /// <summary>
    /// Loads embedded timed passages with fallback protection against corrupted data.
    /// </summary>
    public static IReadOnlyList<TimedPassage> LoadTimedPassagesSafe()
    {
        try
        {
            return LoadTimedPassages();
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException or JsonException)
        {
            return
            [
                new TimedPassage(
                    "fallback_01",
                    "Steady Focus",
                    "Consistent daily practice builds durable finger pathways far faster than occasional marathon drills. Keep your wrists neutral and focus on accuracy before speed.")
            ];
        }
    }

    /// <summary>
    /// Parses and validates timed passages JSON.
    /// </summary>
    public static IReadOnlyList<TimedPassage> ParseTimedPassages(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Timed passages JSON cannot be empty.");
        }

        PassagesDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<PassagesDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed timed passages JSON.", ex);
        }

        if (doc?.Passages is null || doc.Passages.Count == 0)
        {
            throw new InvalidDataException("Timed passages document must contain at least one passage.");
        }

        var list = new List<TimedPassage>(doc.Passages.Count);
        foreach (PassageDto dto in doc.Passages)
        {
            if (string.IsNullOrWhiteSpace(dto.Id) ||
                string.IsNullOrWhiteSpace(dto.Title) ||
                string.IsNullOrWhiteSpace(dto.Text))
            {
                throw new InvalidDataException("Timed passage entry is missing required fields.");
            }

            list.Add(new TimedPassage(dto.Id.Trim(), dto.Title.Trim(), dto.Text.Trim()));
        }

        return list;
    }

    /// <summary>
    /// Loads clean alphabetic dictionary words from embedded resources.
    /// </summary>
    public static IReadOnlyList<string> LoadDictionaryWords()
    {
        string json = ReadEmbeddedText(DictionaryResourceName);
        return ParseDictionaryWordsSafe(json);
    }

    /// <summary>
    /// Loads and validates the embedded multi-track course catalog.
    /// </summary>
    public static IReadOnlyList<CourseTrack> LoadCourseTracks()
    {
        string json = ReadEmbeddedText(CoursesResourceName);
        return ParseCourseTracks(json);
    }

    /// <summary>
    /// Loads embedded course tracks with safe fallback protection if JSON is malformed.
    /// </summary>
    public static IReadOnlyList<CourseTrack> LoadCourseTracksSafe()
    {
        try
        {
            return LoadCourseTracks();
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException or JsonException or ArgumentException)
        {
            return CreateFallbackTracks();
        }
    }

    /// <summary>
    /// Parses and validates multi-track course JSON.
    /// </summary>
    public static IReadOnlyList<CourseTrack> ParseCourseTracks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Course catalog JSON cannot be empty.");
        }

        CoursesDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<CoursesDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed course catalog JSON.", ex);
        }

        if (doc?.Tracks is null || doc.Tracks.Count == 0)
        {
            throw new InvalidDataException("Course catalog must contain at least one track.");
        }

        var tracks = new List<CourseTrack>(doc.Tracks.Count);
        var seenExerciseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (TrackDto trackDto in doc.Tracks)
        {
            if (string.IsNullOrWhiteSpace(trackDto.TrackId) ||
                string.IsNullOrWhiteSpace(trackDto.Title) ||
                trackDto.Units is null ||
                trackDto.Units.Count == 0)
            {
                throw new InvalidDataException($"Course track '{trackDto.TrackId}' is missing required fields or units.");
            }

            var units = new List<CourseUnit>(trackDto.Units.Count);
            bool firstExerciseInTrack = true;

            foreach (UnitDto unitDto in trackDto.Units.OrderBy(u => u.UnitNumber))
            {
                if (unitDto.UnitNumber <= 0 ||
                    string.IsNullOrWhiteSpace(unitDto.Title) ||
                    unitDto.Exercises is null ||
                    unitDto.Exercises.Count == 0)
                {
                    throw new InvalidDataException($"Course unit {unitDto.UnitNumber} in '{trackDto.TrackId}' is invalid.");
                }

                var exercises = new List<CourseExercise>(unitDto.Exercises.Count);
                foreach (ExerciseDto exDto in unitDto.Exercises.OrderBy(e => e.OrderIndex))
                {
                    if (string.IsNullOrWhiteSpace(exDto.ExerciseId) || !seenExerciseIds.Add(exDto.ExerciseId))
                    {
                        throw new InvalidDataException($"Invalid or duplicate exerciseId '{exDto.ExerciseId}'.");
                    }

                    CourseStepKind kind = Enum.TryParse(exDto.Kind, ignoreCase: true, out CourseStepKind parsedKind)
                        ? parsedKind
                        : CourseStepKind.KeyDrill;

                    if (kind != CourseStepKind.Tip && string.IsNullOrWhiteSpace(exDto.PracticeText))
                    {
                        throw new InvalidDataException($"Exercise '{exDto.ExerciseId}' must provide practiceText.");
                    }

                    exercises.Add(new CourseExercise(
                        exerciseId: exDto.ExerciseId.Trim(),
                        trackId: trackDto.TrackId.Trim(),
                        unitNumber: unitDto.UnitNumber,
                        orderIndex: exDto.OrderIndex,
                        title: exDto.Title?.Trim() ?? exDto.ExerciseId,
                        kind: kind,
                        estimatedMinutes: exDto.EstimatedMinutes?.Trim() ?? "3 min.",
                        tipContent: exDto.TipContent?.Trim() ?? string.Empty,
                        practiceText: exDto.PracticeText?.Trim() ?? string.Empty,
                        minimumNetWpm: Math.Max(0, exDto.MinimumNetWpm),
                        minimumAccuracy: Math.Clamp(exDto.MinimumAccuracy, 0, 100),
                        isUnlocked: firstExerciseInTrack));

                    firstExerciseInTrack = false;
                }

                units.Add(new CourseUnit(
                    unitDto.UnitNumber,
                    unitDto.Title.Trim(),
                    unitDto.FocusKeys?.Trim() ?? string.Empty,
                    unitDto.ErgonomicTip?.Trim() ?? string.Empty,
                    exercises));
            }

            tracks.Add(new CourseTrack(
                trackDto.TrackId.Trim(),
                trackDto.Title.Trim(),
                trackDto.Description?.Trim() ?? string.Empty,
                trackDto.DurationEstimate?.Trim() ?? string.Empty,
                units));
        }

        return tracks;
    }

    /// <summary>
    /// Parses dictionary JSON defensively, sanitizing to clean alphabetic lowercase words with fallback on corruption.
    /// </summary>
    public static IReadOnlyList<string> ParseDictionaryWordsSafe(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return FallbackDictionaryWords;
        }

        try
        {
            WordsDocument? doc = JsonSerializer.Deserialize<WordsDocument>(json, SerializerOptions);
            if (doc?.Words is null || doc.Words.Count == 0)
            {
                return FallbackDictionaryWords;
            }

            var cleaned = doc.Words
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim())
                .Where(w => w.All(char.IsLetter))
                .Select(w => w.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return cleaned.Length > 0 ? cleaned : FallbackDictionaryWords;
        }
        catch (JsonException)
        {
            return FallbackDictionaryWords;
        }
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        Assembly assembly = typeof(EmbeddedContentLoader).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyList<CourseLevel> CreateFallbackCurriculum() =>
    [
        new CourseLevel(
            1,
            "Level 1 — Home Row",
            "Anchor index fingers on F and J.",
            "Keep wrists neutral and hover lightly over the home row.",
            [
                new LessonModel(
                    "L1_01",
                    1,
                    1,
                    "1.1 F & J Anchors",
                    "Starter drill for F and J anchors.",
                    "fj",
                    "fff jjj fjf jfj ff jj fjfj jfjf",
                    "Beginner",
                    15,
                    90,
                    isUnlocked: true)
            ])
    ];

    private sealed class CurriculumDocument
    {
        [JsonPropertyName("levels")]
        public List<LevelDto>? Levels { get; set; }
    }

    private sealed class LevelDto
    {
        [JsonPropertyName("levelId")]
        public int LevelId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("ergonomicTip")]
        public string? ErgonomicTip { get; set; }

        [JsonPropertyName("lessons")]
        public List<LessonDto>? Lessons { get; set; }
    }

    private sealed class LessonDto
    {
        [JsonPropertyName("lessonId")]
        public string? LessonId { get; set; }

        [JsonPropertyName("orderIndex")]
        public int OrderIndex { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("targetCharacters")]
        public string? TargetCharacters { get; set; }

        [JsonPropertyName("practiceText")]
        public string? PracticeText { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }

        [JsonPropertyName("minimumNetWpm")]
        public double MinimumNetWpm { get; set; }

        [JsonPropertyName("minimumAccuracy")]
        public double MinimumAccuracy { get; set; }
    }

    private sealed class PassagesDocument
    {
        [JsonPropertyName("passages")]
        public List<PassageDto>? Passages { get; set; }
    }

    private sealed class PassageDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class WordsDocument
    {
        [JsonPropertyName("words")]
        public List<string>? Words { get; set; }
    }

    private static IReadOnlyList<CourseTrack> CreateFallbackTracks() =>
    [
        new CourseTrack(
            "fast_touch",
            "Fast Touch Typing Course",
            "Build home row posture and full touch-typing fluency.",
            "3:00 hours",
            [
                new CourseUnit(
                    1,
                    "The Home Row",
                    "asdfjkl;",
                    "Hover wrists gently above the desk with index fingers on F and J.",
                    [
                        new CourseExercise(
                            "ft_01_01",
                            "fast_touch",
                            1,
                            1,
                            "1.1 Home Row Drill",
                            CourseStepKind.KeyDrill,
                            "3 min.",
                            "Keep fingers curved cleanly over ASDF and JKL;.",
                            "fff jjj ddd kkk sss lll asdf jkl;",
                            15,
                            90,
                            isUnlocked: true)
                    ])
            ])
    ];

    private sealed class CoursesDocument
    {
        [JsonPropertyName("tracks")]
        public List<TrackDto>? Tracks { get; set; }
    }

    private sealed class TrackDto
    {
        [JsonPropertyName("trackId")]
        public string? TrackId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("durationEstimate")]
        public string? DurationEstimate { get; set; }

        [JsonPropertyName("units")]
        public List<UnitDto>? Units { get; set; }
    }

    private sealed class UnitDto
    {
        [JsonPropertyName("unitNumber")]
        public int UnitNumber { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("focusKeys")]
        public string? FocusKeys { get; set; }

        [JsonPropertyName("ergonomicTip")]
        public string? ErgonomicTip { get; set; }

        [JsonPropertyName("exercises")]
        public List<ExerciseDto>? Exercises { get; set; }
    }

    private sealed class ExerciseDto
    {
        [JsonPropertyName("exerciseId")]
        public string? ExerciseId { get; set; }

        [JsonPropertyName("orderIndex")]
        public int OrderIndex { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("estimatedMinutes")]
        public string? EstimatedMinutes { get; set; }

        [JsonPropertyName("tipContent")]
        public string? TipContent { get; set; }

        [JsonPropertyName("practiceText")]
        public string? PracticeText { get; set; }

        [JsonPropertyName("minimumNetWpm")]
        public double MinimumNetWpm { get; set; }

        [JsonPropertyName("minimumAccuracy")]
        public double MinimumAccuracy { get; set; }
    }
}
