<div align="center">
  <img src="src/TypeForge.App/Assets/Branding/typeforge-logo-lockup.scale-200.png" alt="TypeForge" width="320" />

  <h3>Master Touch Typing. Offline, Focused, and Built for Windows.</h3>

  <p>
    A native Windows 10/11 typing tutor combining structured courses, adaptive weak-key and bigram training, spaced repetition, and real-time ergonomic feedback in a distraction-free Fluent Acrylic interface.
  </p>

  <p>
    <strong>Windows 10 / 11 (x64)</strong> &middot; <strong>WinUI 3 &amp; .NET 8</strong> &middot; <strong>100% Offline Local Storage</strong>
  </p>
</div>

---

## Why TypeForge?

Most typing tools are browser tabs cluttered with ads, accounts, and generic word lists. TypeForge is a fast, native Windows desktop application built for deliberate skill building:

- **Accuracy before speed** — Early lessons hide WPM and focus on clean muscle memory before ramping up pace.
- **Targeted weakness training** — Tracks per-key and 2-letter pair (*bigram*) latency and error rates to generate drills around what actually slows you down.
- **Daily guided flow** — **Today's Session** builds a compact daily routine (warm-up, next course step, weak-key review, and spaced-repetition due keys) tailored to your daily goal.
- **Zero telemetry, zero accounts** — All sessions, statistics, and profiles stay in local SQLite databases on your machine.

---

## Key Features

### Structured 4-Track Curriculum
- **Fast Touch Typing**, **Speed Building**, **Numbers**, and **Special Marks** tracks.
- Sequential 5-step lesson workflow: **Tip** -> **New Keys** -> **Key Drill** -> **Word Drill** -> **Paragraph Drill**.
- **60-Second Placement Test** on first launch recommends your starting track and unlocked keys.
- **Skip ahead when mastered** option and configurable accuracy/mistake pass gates (`90%` accuracy / `<= 5` mistakes by default).

### Adaptive Learning & Speed Tools
- **Bigram & Weakness Engine** (`BigramTracker` + `WeaknessScorer`) targeting slow letter transitions and error-prone keys.
- **Spaced Repetition (`ReviewScheduler`)** scheduling due keys for periodic reinforcement.
- **Live Pace Caret** (`Off`, `Fixed WPM`, `Personal Best`, `Last session`) to pace your rhythm without breaking focus.
- **Typing Rhythm & Replay**: View **Net WPM**, **Raw WPM**, **Accuracy %**, and **Consistency %** after every session, with one-click **Practice Missed Words** replay.

### Ergonomic & Focus-First Interface
- **Finger Guide**: Live native hand diagram highlighting the exact finger for each target key.
- **Keyboard Fade Ramp**: Progresses from full keycap labels to home-row bumps only, then hidden.
- **Smart Guards**: Strict-mode wrong-key feedback with finger hints after consecutive misses, Caps Lock warning on lowercase-only drills, and quiet fatigue break nudges.
- **Zen / Focus Mode**: Press `Esc` or toggle Focus Mode for a pure typing canvas with Dark (`#0D0D0D` Acrylic) and Light themes.

### Timed Tests & Real-World Pools
- Countdown and word-count tests with **Real-World Pools** (*Quotes*, *Code snippets*, *Numbers*, *Punctuation*) and custom pasted text.
- Isolated from adaptive unlock progression so experimentation never skews your learning path.

### Multi-Profile, Fresh Start & Leaderboard
- **Isolated User Profiles**: Each profile has its own SQLite database (`%LOCALAPPDATA%\TypeForge\profiles\<id>.db`).
- **"Who's typing?" Launch Picker**: Appears automatically when two or more profiles exist, alongside a quick-switch pill in the top bar.
- **Fresh Start (Reset Progress)**: Clear sessions, course progress, stats, and streaks for a profile while keeping theme, sound, and typing preferences.
- **Profile Leaderboard**: Compare **Average Net WPM**, **Best WPM**, **Accuracy %**, **Course %**, and **Streaks** across local profiles on the Stats page.

### Analytics & Visuals
- Native Canvas **WPM & Accuracy History**, **7-Day Weekly Trend**, **QWERTY Performance Heatmap**, **Weak Bigrams**, and **Due Review Keys**.

---

## Installation & Updates

1. Download `TypeForge-Setup-win-x64.exe` from the [GitHub Releases](../../releases) page and run the installer.
2. TypeForge checks GitHub Releases in the background (the only network activity in the app) and notifies you in **Settings** when a new version is ready to download and install.

---

## Building from Source

### Prerequisites
- Windows 10 (1809+) or Windows 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) & Windows App SDK build tools

### Run Unit Tests

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet test tests/TypeForge.Tests/TypeForge.Tests.csproj -c Release --nologo
```

### Build the Desktop App

```powershell
dotnet build src/TypeForge.App/TypeForge.App.csproj -c Debug -p:Platform=x64 --nologo
```

---

## Project Structure

- `src/TypeForge.Core` — Typing engine, adaptive/course/placement/review algorithms, and parameterized SQLite persistence.
- `src/TypeForge.App` — WinUI 3 desktop UI (`CommunityToolkit.Mvvm`, DI, native Canvas visualizations, audio feedback, and updater).
- `tests/TypeForge.Tests` — xUnit test suite covering engine math, migrations, learning algorithms, and profile isolation.
- `installer/TypeForge.iss` — Inno Setup 6 script for `TypeForge-Setup-win-x64.exe`.
