# Changelog

All notable changes to this project are documented here.

## [Unreleased]

## [0.4.0] - 2026-11-12

### Added
- Reusable `TypingStreamView` with configurable font family/weight/style, whitespace glyphs (`None` / `Bar` / `Bullet`), cursor shape (`Block` / `Box` / `Line` / `Underline`), and smooth vs jumping cursor motion (dcc2a61)
- Shared `TypingSettingsControl` with animated `"Eat more apples and oranges."` live preview used across both `PracticePage` and `TestPage` (dcc2a61)
- `BookModel`, `TestTextGenerator`, bundled offline *Alice's Adventures in Wonderland* (`books.json`), and 1,000-word `english_words.json` (dcc2a61)
- `ForgiveErrors` and `SpaceSkipsWords` support in `TypingEngine` and 4-theme volume-scaled `KeystrokeAudioService` (`Default`, `Mechanical`, `Typewriter`, `Soft`) (dcc2a61)
- Unit tests in `TestFeatureCoreTests` covering book loading, word stats, pseudo-words, `ForgiveErrors`, `SpaceSkipsWords`, and SQLite settings round-trip (dcc2a61)

### Changed
- Switched default startup route to `Course` and maximized/centered main window on work area (dcc2a61)
- Widened `PracticePage` Session Complete card so `"Review Weak Keys"` displays without clipping (dcc2a61)
- point version.json at release v0.3.0 [skip ci] (8edb8b6)

### Fixed
- Fixed Practice and Test keystroke registration after clicking top-bar tabs via `AllowFocusOnInteraction="False"`, deferred surface focus, and `RootGrid` shell fallback routing (dcc2a61)

### Removed
- Removed Course Certificate / Progress Report UI and PNG export from Statistics (dcc2a61)

## [0.3.0] - 2026-11-12

### Added
- Inno Setup script at `installer/TypeForge.iss` compiling `TypeForge-Setup-win-x64.exe` with `PrivilegesRequired=lowest`, `CloseApplications=yes`, and LZMA2 compression. (3bef703)
- Trim-safe `UpdateJsonContext` and `UpdateState` / `ReleaseInfo` models in `UpdateService` with 12s startup plus 24h `DispatcherTimer` checks, download progress streaming, and `/VERYSILENT` installer handoff. (3bef703)

### Changed
- Wired `IUpdateService` background monitoring, state events, and download progress into `App.xaml.cs`, `MainWindow.xaml.cs`, `SettingsViewModel.cs`, and `SettingsPage.xaml.cs`. (3bef703)
- Updated `.github/workflows/release.yml` and `.github/workflows/winui3-release.yml` to publish self-contained `win-x64`, package `TypeForge-win-x64.zip`, compile `TypeForge-Setup-win-x64.exe` via Inno Setup 6, and upload both assets. (3bef703)
- Ignored generated `installer/*.exe` and `*-win-x64.zip` build outputs in `.gitignore`. (3bef703)
- point version.json at release v0.2.0 [skip ci] (cae65f3)

### Removed
- Removed the `Velopack` NuGet reference, `DISABLE_XAML_GENERATED_MAIN` constant, and custom `src/TypeForge.App/Program.cs` bootstrap. (3bef703)

## [0.2.0] - 2026-11-12

### Added
- Velopack bootstrap in `Program.cs` (`DISABLE_XAML_GENERATED_MAIN`) and dynamic `VERSION` injection in `TypeForge.App.csproj`. (812320d)
- `UpdateService` (`IUpdateService`) checking GitHub Releases via Velopack (`GithubSource`) for installed builds and trim-safe `JsonDocument` checks for portable/dev builds, plus fetching `release_notes.md`. (812320d)
- Silent launch-time update check for installed apps with clickable top-bar `Update v...` pill in `MainWindow` and `Updates & What's New` card in `SettingsPage` / `SettingsViewModel`. (812320d)

### Changed
- Updated `release.yml` to run automatically on pushes to `main` (skipping `[skip ci]`) and chain a `windows-latest` `publish-installer` job producing `TypeForgeApp-win-Setup.exe`, `TypeForgeApp-win-Portable.zip`, `.nupkg` delta/full packages, and `releases.win.json`. (812320d)
- Updated `winui3-release.yml` with explicit `--runtime win-x64` Velopack packaging for manual dispatches. (812320d)
- update version.json for WinUI3 release v0.1.0 [skip ci] (baab131)
- point version.json at release v0.1.0 [skip ci] (4649225)

### Fixed
- gate publish-installer on new_version output and trigger auto-release (4294a71)

## [0.1.0] - 2026-11-11

### Added
- Synthesized in-memory PCM mechanical click and error alert sounds via Win32 `PlaySoundW` in `KeystrokeAudioService`, honoring HUD mute and `SoundEnabled` settings. (00fabf1)
- Sequential course progression and rotating timed passage advancement on `QueueNextDrill`, plus targeted weak-key drill synthesis in `TypeForgeStateService`. (00fabf1)
- `TypeForge.Core`: typing state machine (`TypingEngine`), adaptive unlock & generator engine (`AdaptiveEngine`), SQLite v2 persistence (`SqliteTypeForgeRepository`), embedded curriculum/course/timed/dictionary JSON resources, multi-track `CourseCatalogService`, and `StatisticsAggregator`. (22dfe5f)
- `TypeForge.App`: Acrylic Keybr-inspired shell (`MainWindow`), real-time Practice HUD with hover controls & audio click feedback, virtual QWERTY heatmap keyboard with finger-zone option, Multi-Track Course UI (`Fast Touch` 12-lesson core, `Speed Building`, `Numbers`, `Special Marks`) with inline tips & Optimal Duration, Timed/Custom Tests, native Canvas benchmark Statistics (`Gross/Net WPM`, `Accuracy %`, `Difficult Keys`) with `RenderTargetBitmap` PNG Certificate export, and Settings. (22dfe5f)
- `TypeForge.Tests`: 48 xUnit tests covering typing calculations, backspaces, embedded JSON loading/validation, SQLite schema v1/v2 persistence, adaptive word filtering, course progression, and statistics ranking. (22dfe5f)
- `docs/`: `PRD.md`, `DESIGN.md`, `TECH.md`, `TASKS.md`, `WINUI3_STARTER_GUIDE.md`, `WINUI3_UPDATE_WORKFLOW.md`, `TypeForge.sln`. (22dfe5f)
- GitHub actions workflows for CI, CodeQL, and releases (4708f2f)
- Issue and pull request templates (4708f2f)
- Project documentation templates (PRD, TECH, DESIGN, TASKS, MEMORY) (4708f2f)
- Helper scripts for initialization and scaffolding (4708f2f)
- Linter, editor, and prettier configurations (4708f2f)

### Fixed
- Migrated `EmbeddedContentLoader` JSON deserialization to System.Text.Json source generation (`EmbeddedContentJsonContext`) so Release builds run with reflection disabled. (00fabf1)
- Replaced placeholder metadata in `Package.appxmanifest` and updated `winui3-release.yml` to build and attach self-contained `win-x64` zip archives. (00fabf1)

### Removed
- Deleted unused WinUI template files (`MainPage.xaml` and `MainPage.xaml.cs`). (00fabf1)

### Security
- Parameterized SQLite queries exclusively, strict domain model validation, offline-zero-network architecture, and `.gitignore` exclusion of build/database/secret files. (22dfe5f)
