# Tech — TypeForge

## Stack
- UI Framework: WinUI 3 (`DesktopAcrylicBackdrop` translucent Acrylic shell)
- Windows Runtime: Windows App SDK (1.6+)
- Language: C# 12 (`Nullable=enable`, `TreatWarningsAsErrors=true`)
- Runtime: .NET 8
- Architecture: MVVM (Model-View-ViewModel) with all business logic in `TypeForge.Core` (zero WinUI dependencies)
- MVVM Toolkit: CommunityToolkit.Mvvm (source generators)
- Dependency Injection: Microsoft.Extensions.DependencyInjection
- Database: SQLite via Microsoft.Data.Sqlite (no ORM, parameterized queries, `PRAGMA user_version` migrations inside transactions)
- Testing: xUnit for `TypeForge.Core`
- Visualizations: Native WinUI shapes and Canvas (charts, heatmaps, finger-guide hand diagram, cursor & pace caret overlays)
- Storage: Local application data (`%LOCALAPPDATA%\TypeForge`)
- Packaging & Updates: Self-contained `win-x64` publish packaged with Inno Setup 6 (`TypeForge-Setup-win-x64.exe`) and in-app GitHub Releases self-updater (`UpdateService`)

## Data and Auth
- [x] Entities, fields, and DB constraints:
  - Lessons: LessonId PRIMARY KEY, LevelId NOT NULL, Title NOT NULL, Description NOT NULL, Content NOT NULL, MinimumWpm NOT NULL, MinimumAccuracy NOT NULL, OrderIndex NOT NULL.
  - LessonProgress: LessonId PRIMARY KEY REFERENCES Lessons(LessonId), IsUnlocked NOT NULL, IsCompleted NOT NULL, BestWpm NOT NULL, BestAccuracy NOT NULL, Stars NOT NULL, CompletedAt NULL.
  - CourseExerciseProgress: ExerciseId PRIMARY KEY, IsUnlocked NOT NULL, IsCompleted NOT NULL, BestWpm NOT NULL, BestAccuracy NOT NULL, Stars NOT NULL, CompletedAt NULL.
  - PracticeSessions: SessionId PRIMARY KEY, SessionType NOT NULL, StartedAt NOT NULL, DurationSeconds NOT NULL, NetWpm NOT NULL, Accuracy NOT NULL, CorrectCharacters NOT NULL, TotalKeystrokes NOT NULL, Mistakes NOT NULL.
  - KeyStatistics: Character PRIMARY KEY, TotalKeystrokes NOT NULL, CorrectKeystrokes NOT NULL, IncorrectKeystrokes NOT NULL, AverageLatencyMs NOT NULL.
  - BigramStatistics: Bigram TEXT PRIMARY KEY (2 chars), TotalCount INT NOT NULL, ErrorCount INT NOT NULL, AverageLatencyMs REAL NOT NULL.
  - KeyReviewSchedule: Character TEXT PRIMARY KEY, LastReviewedAt TEXT NULL, IntervalDays REAL NOT NULL, EaseFactor REAL NOT NULL, DueAt TEXT NOT NULL.
  - PlacementResults: PlacementId TEXT PRIMARY KEY, TakenAt TEXT NOT NULL, NetWpm REAL NOT NULL, Accuracy REAL NOT NULL, RecommendedTrack TEXT NOT NULL.
  - AdaptiveProgress: Character PRIMARY KEY, IsUnlocked NOT NULL, StageIndex NOT NULL.
  - Settings: SettingKey PRIMARY KEY, SettingValue NOT NULL.
- [x] Auth method, roles, and protected routes: None. Fully local desktop app.
- [x] Indexes: `PracticeSessions(StartedAt)`, `Lessons(LevelId, OrderIndex)`, `KeyReviewSchedule(DueAt)`, `BigramStatistics(AverageLatencyMs)`.
- [x] Database migrations: Versioned integer `PRAGMA user_version` schema runner on initialization inside explicit transactions with rollback on failure.

## APIs and Third-Party
- Offline-first learning & typing engine with zero telemetry and zero cloud data sync.
- Optional background check against GitHub Releases API for self-updating installer assets.
- Clean-room C# implementation of bigram EMA tracking, z-score weakness ranking, SM-2-lite spaced repetition, and phonetic/dictionary drill generation.

## Secrets and Env Vars
- No application secrets or API keys required. All user data resides in `%LOCALAPPDATA%\TypeForge`.

## Security
- Parameterized SQL queries only via `SqliteCommand.Parameters.AddWithValue`. Never string-concatenate SQL.
- Strict input validation for settings, typing characters, custom pasted texts, and embedded JSON datasets.
- Schema migrations run within transactions with rollback safety so failed upgrades leave prior data intact.
- Explicit rejection of system-wide Windows hooks (`SetWindowsHookEx`); pressed wrong-key identities are never persisted to disk.

## Deployment
- Self-contained `win-x64` executable compiled via `dotnet publish -c Release -p:PublishProfile=win-x64` and wrapped into `TypeForge-Setup-win-x64.exe` via Inno Setup 6 (`installer/TypeForge.iss`).
- Local development runs directly from `src/TypeForge.App/bin/Debug/net8.0-windows10.0.26100.0/win-x64/TypeForge.App.exe`.

## WinUI3 CI
- Configured in `.github/workflows/release.yml` on `windows-latest` to run tests, publish `win-x64`, build Inno Setup installer + portable zip, and publish GitHub Releases.
