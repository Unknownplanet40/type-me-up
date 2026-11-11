# Tech — TypeForge

## Stack
- UI Framework: WinUI 3
- Windows Runtime: Windows App SDK (latest stable compatible, 1.6+)
- Language: C# 12
- Runtime: .NET 8
- Architecture: MVVM (Model-View-ViewModel)
- MVVM Toolkit: CommunityToolkit.Mvvm (Community-standard, source generators, zero-boilerplate)
- Dependency Injection: Microsoft.Extensions.DependencyInjection
- Database: SQLite via Microsoft.Data.Sqlite (no ORM, pure parameterized queries, schema versioning)
- Testing: xUnit + standard assertions for TypeForge.Core
- Visualizations: Native WinUI shapes and Canvas (no third-party chart libraries)
- Certificate Generation: Native `RenderTargetBitmap` rendering to PNG export
- Storage: Local application data (`%LOCALAPPDATA%\TypeForge`)
- Packaging: MSIX (Windows App SDK unpackaged for local dev debug, packaged MSIX for release)

## Data and Auth
- [x] Entities, fields, and DB constraints:
  - Lessons: LessonId PRIMARY KEY, LevelId NOT NULL, Title NOT NULL, Description NOT NULL, Content NOT NULL, MinimumWpm NOT NULL, MinimumAccuracy NOT NULL, OrderIndex NOT NULL.
  - LessonProgress: LessonId PRIMARY KEY REFERENCES Lessons(LessonId), IsUnlocked NOT NULL, IsCompleted NOT NULL, BestWpm NOT NULL, BestAccuracy NOT NULL, Stars NOT NULL, CompletedAt NULL.
  - PracticeSessions: SessionId PRIMARY KEY, SessionType NOT NULL, StartedAt NOT NULL, DurationSeconds NOT NULL, NetWpm NOT NULL, Accuracy NOT NULL, CorrectCharacters NOT NULL, TotalKeystrokes NOT NULL, Mistakes NOT NULL.
  - KeyStatistics: Character PRIMARY KEY, TotalKeystrokes NOT NULL, CorrectKeystrokes NOT NULL, IncorrectKeystrokes NOT NULL, AverageLatencyMs NOT NULL.
  - AdaptiveProgress: Character PRIMARY KEY, IsUnlocked NOT NULL, StageIndex NOT NULL.
  - Settings: SettingKey PRIMARY KEY, SettingValue NOT NULL.
- [x] Auth method, roles, and protected routes: None. Fully local desktop app.
- [x] Indexes: Index on `PracticeSessions(StartedAt)`, `Lessons(LevelId, OrderIndex)`.
- [x] Database migrations: Versioned integer PRAGMA user_version schema runner on initialization.

## APIs and Third-Party
- None. Offline-first application with zero network calls, zero telemetry, and zero third-party cloud APIs.
- Clean-room algorithmic implementation of Markov bigram pseudo-word synthesis without AGPL-3.0 dependencies.

## Secrets and Env Vars
- No secrets or API keys required. All data resides in user's local profile.

## Security
- Parameterized SQL queries only via `SqliteCommand.Parameters.AddWithValue`. Never string-concatenate SQL.
- Strict input validation for settings, typing characters, custom pasted texts, and embedded JSON datasets.
- Schema initialization runs within transactions with rollback safety.
- Explicit rejection of system-wide Windows hooks (`SetWindowsHookEx`) to avoid keylogging behavior or security warnings.

## Deployment
- Windows desktop MSIX package generated through `msbuild /p:GenerateAppxPackageOnBuild=true`.
- Local development runs directly via `dotnet run --project src/TypeForge.App`.

## WinUI3 CI (only for native Windows projects)
- Configured in `.github/workflows/winui3-release.yml` with Windows runner (`windows-latest`).
- Dev builds use developer certificate; release pipeline supports Azure Trusted Signing for production MSIX artifacts.
