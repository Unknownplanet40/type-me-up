# Changelog

All notable changes to this project are documented here.

## [Unreleased]

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
