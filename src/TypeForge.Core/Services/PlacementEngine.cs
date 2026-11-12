using System.Text.Json;
using System.Text.Json.Serialization;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Represents the recommended course track, starting lesson/exercise, and adaptive stage produced by a placement evaluation.
/// </summary>
/// <param name="RuleId">The matched rule identifier.</param>
/// <param name="RecommendedCourseId">The recommended course track identifier.</param>
/// <param name="StartingUnitNumber">The recommended 1-based starting unit number.</param>
/// <param name="StartingExerciseId">The recommended starting exercise identifier.</param>
/// <param name="UnlockedAdaptiveStage">The initial unlocked adaptive stage (1..13).</param>
/// <param name="Summary">Human-readable placement recommendation summary.</param>
public sealed record PlacementRecommendation(
    string RuleId,
    string RecommendedCourseId,
    int StartingUnitNumber,
    string StartingExerciseId,
    int UnlockedAdaptiveStage,
    string Summary);

/// <summary>
/// Root JSON document defining placement diagnostic prompt, duration, and threshold rules.
/// </summary>
public sealed class PlacementRulesDocument
{
    /// <summary>Gets or sets the schema version of the placement rules document.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the diagnostic prompt text used for the placement test.</summary>
    [JsonPropertyName("diagnosticPrompt")]
    public string DiagnosticPrompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the placement test duration in seconds.</summary>
    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; set; } = 60;

    /// <summary>Gets or sets the list of placement evaluation rules.</summary>
    [JsonPropertyName("rules")]
    public List<PlacementRuleDto>? Rules { get; set; }
}

/// <summary>
/// Single threshold rule mapping minimum WPM and accuracy to a recommended track and adaptive stage.
/// </summary>
public sealed class PlacementRuleDto
{
    /// <summary>Gets or sets the unique rule identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum Net WPM required to match this rule.</summary>
    [JsonPropertyName("minWpm")]
    public double MinWpm { get; set; }

    /// <summary>Gets or sets the minimum accuracy percentage required to match this rule.</summary>
    [JsonPropertyName("minAccuracy")]
    public double MinAccuracy { get; set; }

    /// <summary>Gets or sets the recommended course track identifier.</summary>
    [JsonPropertyName("recommendedCourseId")]
    public string RecommendedCourseId { get; set; } = string.Empty;

    /// <summary>Gets or sets the 1-based starting unit number.</summary>
    [JsonPropertyName("startingUnitNumber")]
    public int StartingUnitNumber { get; set; } = 1;

    /// <summary>Gets or sets the starting exercise identifier.</summary>
    [JsonPropertyName("startingExerciseId")]
    public string StartingExerciseId { get; set; } = string.Empty;

    /// <summary>Gets or sets the unlocked adaptive letter stage (1..13).</summary>
    [JsonPropertyName("unlockedAdaptiveStage")]
    public int UnlockedAdaptiveStage { get; set; } = 1;

    /// <summary>Gets or sets the human-readable placement summary.</summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PlacementRulesDocument))]
internal sealed partial class PlacementRulesJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Evaluates a 60-second placement test result against validated rules from placement_rules.json.
/// Always falls back safely to Fast Touch Lesson 1 ("ft_01_02", stage 1) if rules are missing or malformed.
/// </summary>
public sealed class PlacementEngine
{
    /// <summary>Default diagnostic prompt used if the JSON prompt is missing.</summary>
    public const string DefaultDiagnosticPrompt =
        "Touch typing builds speed and accuracy when your fingers return to the home row after every keystroke. Stay relaxed, keep your eyes on the screen, and focus on steady rhythm rather than rushing.";

    /// <summary>Default fallback recommendation for beginners.</summary>
    public static readonly PlacementRecommendation DefaultBeginnerRecommendation = new(
        RuleId: "beginner_fast_touch",
        RecommendedCourseId: "fast_touch",
        StartingUnitNumber: 1,
        StartingExerciseId: "ft_01_02",
        UnlockedAdaptiveStage: 1,
        Summary: "Start at Fast Touch Typing Course Lesson 1 (Home Row F and J) to build proper finger memory from the ground up.");

    private readonly PlacementRulesDocument _document;
    private readonly IReadOnlyList<PlacementRuleDto> _sortedRules;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlacementEngine"/> class using embedded placement rules.
    /// </summary>
    public PlacementEngine()
        : this(LoadEmbeddedRulesSafe())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlacementEngine"/> class with the specified rules document.
    /// </summary>
    public PlacementEngine(PlacementRulesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = ValidateOrFallback(document);
        _sortedRules = (_document.Rules ?? [])
            .OrderByDescending(r => r.MinWpm)
            .ThenByDescending(r => r.MinAccuracy)
            .ToList();
    }

    /// <summary>Gets the validated diagnostic prompt text.</summary>
    public string DiagnosticPrompt => string.IsNullOrWhiteSpace(_document.DiagnosticPrompt)
        ? DefaultDiagnosticPrompt
        : _document.DiagnosticPrompt.Trim();

    /// <summary>Gets the validated placement duration in seconds.</summary>
    public int DurationSeconds => _document.DurationSeconds is >= 15 and <= 300
        ? _document.DurationSeconds
        : 60;

    /// <summary>
    /// Creates a <see cref="PlacementEngine"/> from raw JSON, falling back to default rules if malformed.
    /// </summary>
    public static PlacementEngine FromJsonSafe(string? json)
    {
        var doc = ParseRulesSafe(json);
        return new PlacementEngine(doc);
    }

    /// <summary>
    /// Loads embedded placement rules from <c>placement_rules.json</c> with safe fallback.
    /// </summary>
    public static PlacementRulesDocument LoadEmbeddedRulesSafe()
    {
        try
        {
            var assembly = typeof(PlacementEngine).Assembly;
            using var stream = assembly.GetManifestResourceStream("TypeForge.Core.Resources.placement_rules.json");
            if (stream is null)
            {
                return CreateFallbackDocument();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return ParseRulesSafe(json);
        }
        catch
        {
            return CreateFallbackDocument();
        }
    }

    /// <summary>
    /// Parses placement rules JSON safely, returning fallback rules on error.
    /// </summary>
    public static PlacementRulesDocument ParseRulesSafe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateFallbackDocument();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize(json, PlacementRulesJsonContext.Default.PlacementRulesDocument);
            return parsed is null ? CreateFallbackDocument() : ValidateOrFallback(parsed);
        }
        catch
        {
            return CreateFallbackDocument();
        }
    }

    /// <summary>
    /// Evaluates net WPM and accuracy against placement rules and returns the highest matching recommendation.
    /// </summary>
    public PlacementRecommendation Evaluate(double netWpm, double accuracyPercentage)
    {
        if (double.IsNaN(netWpm) || double.IsInfinity(netWpm) || netWpm < 0)
        {
            netWpm = 0;
        }

        if (double.IsNaN(accuracyPercentage) || double.IsInfinity(accuracyPercentage))
        {
            accuracyPercentage = 0;
        }

        accuracyPercentage = Math.Clamp(accuracyPercentage, 0.0, 100.0);

        foreach (var rule in _sortedRules)
        {
            if (netWpm >= rule.MinWpm && accuracyPercentage >= rule.MinAccuracy)
            {
                return new PlacementRecommendation(
                    RuleId: rule.Id,
                    RecommendedCourseId: rule.RecommendedCourseId,
                    StartingUnitNumber: rule.StartingUnitNumber,
                    StartingExerciseId: rule.StartingExerciseId,
                    UnlockedAdaptiveStage: Math.Clamp(rule.UnlockedAdaptiveStage, 1, 13),
                    Summary: rule.Summary);
            }
        }

        return DefaultBeginnerRecommendation;
    }

    /// <summary>
    /// Creates a validated <see cref="PlacementResultRecord"/> for persistence.
    /// </summary>
    public PlacementResultRecord CreateRecord(double netWpm, double accuracyPercentage, DateTimeOffset? takenAt = null)
    {
        var rec = Evaluate(netWpm, accuracyPercentage);
        var record = new PlacementResultRecord(
            PlacementId: Guid.NewGuid().ToString("N"),
            TakenAt: takenAt ?? DateTimeOffset.UtcNow,
            NetWpm: Math.Max(0, netWpm),
            AccuracyPercentage: Math.Clamp(accuracyPercentage, 0, 100),
            RecommendedTrack: rec.RecommendedCourseId);
        record.Validate();
        return record;
    }

    private static PlacementRulesDocument ValidateOrFallback(PlacementRulesDocument doc)
    {
        if (doc.Rules is null || doc.Rules.Count == 0)
        {
            return CreateFallbackDocument();
        }

        var validRules = new List<PlacementRuleDto>();
        foreach (var rule in doc.Rules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                string.IsNullOrWhiteSpace(rule.RecommendedCourseId) ||
                string.IsNullOrWhiteSpace(rule.StartingExerciseId) ||
                rule.StartingUnitNumber <= 0 ||
                rule.UnlockedAdaptiveStage < 1 ||
                rule.UnlockedAdaptiveStage > 13 ||
                double.IsNaN(rule.MinWpm) ||
                double.IsNaN(rule.MinAccuracy) ||
                rule.MinWpm < 0 ||
                rule.MinAccuracy < 0 ||
                rule.MinAccuracy > 100)
            {
                continue;
            }

            validRules.Add(rule);
        }

        if (validRules.Count == 0)
        {
            return CreateFallbackDocument();
        }

        return new PlacementRulesDocument
        {
            SchemaVersion = doc.SchemaVersion > 0 ? doc.SchemaVersion : 1,
            DiagnosticPrompt = string.IsNullOrWhiteSpace(doc.DiagnosticPrompt) ? DefaultDiagnosticPrompt : doc.DiagnosticPrompt,
            DurationSeconds = doc.DurationSeconds is >= 15 and <= 300 ? doc.DurationSeconds : 60,
            Rules = validRules
        };
    }

    private static PlacementRulesDocument CreateFallbackDocument()
    {
        return new PlacementRulesDocument
        {
            SchemaVersion = 1,
            DiagnosticPrompt = DefaultDiagnosticPrompt,
            DurationSeconds = 60,
            Rules =
            [
                new PlacementRuleDto
                {
                    Id = "advanced_speed_building",
                    MinWpm = 30.0,
                    MinAccuracy = 95.0,
                    RecommendedCourseId = "speed_building",
                    StartingUnitNumber = 1,
                    StartingExerciseId = "sb_01_01",
                    UnlockedAdaptiveStage = 13,
                    Summary = "Strong baseline speed and accuracy. Start at Speed Building Course (Lesson 1) with all 26 letters unlocked in Practice."
                },
                new PlacementRuleDto
                {
                    Id = "intermediate_fast_touch",
                    MinWpm = 15.0,
                    MinAccuracy = 90.0,
                    RecommendedCourseId = "fast_touch",
                    StartingUnitNumber = 6,
                    StartingExerciseId = "ft_06_01",
                    UnlockedAdaptiveStage = 6,
                    Summary = "Solid home-row foundation. Start at Fast Touch Typing Course Lesson 6 with the first 6 adaptive letter stages unlocked."
                },
                new PlacementRuleDto
                {
                    Id = "beginner_fast_touch",
                    MinWpm = 0.0,
                    MinAccuracy = 0.0,
                    RecommendedCourseId = "fast_touch",
                    StartingUnitNumber = 1,
                    StartingExerciseId = "ft_01_02",
                    UnlockedAdaptiveStage = 1,
                    Summary = DefaultBeginnerRecommendation.Summary
                }
            ]
        };
    }
}
