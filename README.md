# TypeForge

Offline-first native Windows 10/11 typing tutor built with **WinUI 3**, **Windows App SDK**, and **.NET 8**.

## Features

- **4-Track Course Catalog**: Structured lessons across *Fast Touch Typing*, *Speed Building*, *Numbers*, and *Special Marks* with sequential 5-step progression (`Tip`, `New Keys`, `Key Drill`, `Word Drill`, `Paragraph Drill`).
- **Guided Daily Practice**: First-launch 60-second placement test, **Today's Session** daily planner (`Warm-up`, `Course Step`, `Weakness Review`, `Spaced Repetition Due Keys`), and **Skip ahead when mastered**.
- **Adaptive Learning Engine**: Per-key and 2-letter bigram (`BigramTracker`) latency/accuracy tracking, `WeaknessScorer` weighted drills, SM-2-lite `ReviewScheduler`, and **Slowest Pairs** drills.
- **Ergonomic & Accuracy Coaching**: Native Canvas **Finger Guide** hand diagram, strict-mode wrong-key feedback, Caps Lock guard on lowercase-only drills, keyboard fade ramp (`Full Labels` -> `Home-Row Bumps` -> `Hidden`), and accuracy-first early lessons.
- **Speed & Rhythm Tools**: Live **Pace Caret** (`Off`, `Fixed WPM`, `Personal Best`, `Last session`), **Raw WPM** & **Consistency %**, quiet fatigue break nudges, and one-click **Practice Missed Words** replay on Results.
- **Timed Tests & Real-World Pools**: Configurable timed/word tests plus **Real-World Pools** (`Quotes`, `Code snippets`, `Numbers`, `Punctuation`) and custom pasted text.
- **Multi-Profile, Fresh Start & Leaderboard**: Isolated per-profile SQLite databases, `"Who's typing?"` launch picker when `2+` profiles exist, top-bar profile switcher, **Reset Progress (Fresh Start)** preserving settings, and a **Profile Leaderboard** on the Stats page.
- **Analytics & Visuals**: WPM/Accuracy history charts, 7-day Weekly Trend, QWERTY Heatmap, Weak Bigrams, Due Review Keys, and Dark/Light Acrylic themes.
- **Automatic Updates**: Checks GitHub Releases in the background and installs updates via Inno Setup (`TypeForge-Setup-win-x64.exe`).

## Architecture & Storage

- `src/TypeForge.Core`: Pure .NET 8 typing engine, adaptive/course/placement/review algorithms, and parameterized SQLite persistence (`Microsoft.Data.Sqlite`).
- `src/TypeForge.App`: WinUI 3 desktop UI (`CommunityToolkit.Mvvm`, Dependency Injection, native Canvas charts & keyboard/finger controls).
- `tests/TypeForge.Tests`: xUnit unit test suite.
- **Local Data**: Stored offline at `%LOCALAPPDATA%\TypeForge\` (`profiles.json` and `profiles/<id>.db`), with automatic migration from legacy `typeforge.db`.

## Build & Test

Run unit tests:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet test tests/TypeForge.Tests/TypeForge.Tests.csproj -c Release --nologo
```

Build the WinUI 3 app:

```powershell
dotnet build src/TypeForge.App/TypeForge.App.csproj -c Debug -p:Platform=x64 --nologo
```

## Release Flow

Pushing to `main` triggers `.github/workflows/release.yml`, which automatically bumps `VERSION`, builds the self-contained Windows installer (`TypeForge-Setup-win-x64.exe`), and publishes a GitHub Release used by the in-app updater. Do not edit `VERSION` or `version.json` manually.
