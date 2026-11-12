# TASKS — TypeForge

## Milestone 0 — Preview Artifact
- [x] Task 0.1: Build WinUI 3 preview shell with dummy data and basic Fluent controls (ShellPage, navigation items, mock typing screen layout, virtual keyboard mockup)
  - Acceptance: User approved preview; preview stops before production scaffold; mock data not carried into production
  - Depends on: Approved PRD, TECH, and DESIGN docs
  - Gate: Stop and wait for explicit user approval

## Milestone 1 — Foundation and Typing Engine
- [x] Task 1.1: Create solution structure (`TypeForge.sln`) with `TypeForge.Core` and `TypeForge.Tests`
  - Acceptance: .NET 8 class library and xUnit test project created with Nullable and TreatWarningsAsErrors enabled
  - Depends on: Approved Milestone 0
- [x] Task 1.2: Implement Core domain models (`SessionResult`, `KeyMetric`, `LessonModel`, `CourseLevel`, `UserSettings`)
  - Acceptance: Clean POCO domain models with validation logic and zero UI dependencies
  - Depends on: Task 1.1
- [x] Task 1.3: Implement `TypingEngine` state machine
  - Acceptance: Correct/incorrect character evaluation, backspace handling without counting as correct keystrokes, stopwatch timing, session completion
  - Depends on: Task 1.2
- [x] Task 1.4: Implement metric calculation engine (Net WPM, Accuracy %, mistake tracking, per-key latency and error rate)
  - Acceptance: Mathematical correctness verified, division by zero handled, short sessions guarded
  - Depends on: Task 1.3
- [x] Task 1.5: Write comprehensive unit tests for `TypeForge.Core`
  - Acceptance: xUnit test suite covering calculations, state transitions, backspaces, and edge cases. `dotnet test` passes with zero warnings
  - Depends on: Task 1.4
  - Gate: Stop and wait for user approval

## Milestone 2 — Typing Interface
- [x] Task 2.1: Create `TypeForge.App` WinUI 3 desktop project
  - Acceptance: Windows App SDK configured, Acrylic backdrop (`DesktopAcrylicBackdrop`) enabled, MVVM Toolkit & DI wired
  - Depends on: Approved Milestone 1
- [x] Task 2.2: Implement NavigationView shell, Focus Mode, and Dashboard view
  - Acceptance: Navigation between primary routes; Focus Mode toggle; Dashboard displaying current stats and "Review Weak Keys" quick action
  - Depends on: Task 2.1
- [x] Task 2.3: Build Typing Practice screen with real-time text highlighting and HUD
  - Acceptance: Live green/red/active character stream, live WPM, Accuracy, and elapsed time
  - Depends on: Task 2.2
- [x] Task 2.4: Implement Virtual QWERTY Keyboard and Finger-Zone indicators
  - Acceptance: Visual representation of keys, active target key highlight, finger assignment bar
  - Depends on: Task 2.3
- [x] Task 2.5: Build Results view and dynamic weak keys trigger
  - Acceptance: Post-session summary with Net WPM, Accuracy, error breakdown, star display, and "Review Weak Keys" launcher
  - Depends on: Task 2.4
  - Gate: Stop and wait for user approval

## Milestone 3 — Learning Systems and Persistence
- [x] Task 3.1: SQLite persistence layer in `TypeForge.Core`
  - Acceptance: Microsoft.Data.Sqlite repository with parameterized queries, schema versioning, and auto-creation at `%LOCALAPPDATA%\TypeForge`
  - Depends on: Approved Milestone 2
- [x] Task 3.2: Structured Typing Course with ergonomic tips and progression engine
  - Acceptance: Sequential unlock rules, pass criteria evaluation, star rating calculation, repeat lesson support, ergonomic guidance cards
  - Depends on: Task 3.1
- [x] Task 3.3: Embedded resource loader with defensive validation
  - Acceptance: JSON lesson curricula, text pools, and word lists validated on startup without crashes on malformed data
  - Depends on: Task 3.2
- [x] Task 3.4: Dual-mode adaptive typing practice algorithm
  - Acceptance: Per-key latency and accuracy tracking, letter unlock stages (F/J -> D/K -> ...), phonetic pseudo-word vs natural word generators strictly omitting locked letters
  - Depends on: Task 3.3
- [x] Task 3.5: Timed typing test system & custom text support
  - Acceptance: Countdown/character tests from embedded pools plus custom pasted text option with isolated session history
  - Depends on: Task 3.4
  - Gate: Stop and wait for user approval

## Milestone 4 — Statistics, Polish, and Configurable Typing Stream
- [x] Task 4.1: WPM and Accuracy history charts using WinUI Canvas & Shapes
  - Acceptance: Native line/bar rendering across sessions without third-party chart dependencies
  - Depends on: Approved Milestone 3
- [x] Task 4.2: Keyboard Heatmap visualization
  - Acceptance: QWERTY key grid colored by error intensity and latency metrics
  - Depends on: Task 4.1
- [x] Task 4.3: Multi-track course catalog and benchmark statistics
  - Acceptance: 4 course tracks with sub-steps and benchmark speed/accuracy charts
  - Depends on: Task 4.2
- [x] Task 4.4: Daily practice streak tracker
  - Acceptance: Calendar-day streak calculation stored locally
  - Depends on: Task 4.3
- [x] Task 4.5: Settings view, shared TypingSettingsControl, and preference storage
  - Acceptance: Theme switcher, font/whitespace/cursor/movement options with live preview, multi-theme sound, persisted in SQLite
  - Depends on: Task 4.4
- [x] Task 4.6: Full accessibility, keyboard navigation, and zero-warning verification
  - Acceptance: `dotnet build` with TreatWarningsAsErrors passes; `dotnet test` passes; end-to-end verification complete
  - Depends on: Task 4.5
  - Gate: Final report to user

## Milestone 5 — Learning Engine Foundation (Core + DB)
- [x] Task 5.0 (S): Doc sync before new work
  - Acceptance: PRD updated from "7 levels" to the 4-track catalog; TECH/TASKS use one backdrop name (Acrylic) consistently; MEMORY.md gets a new appended entry (never edit history) recording this plan's decisions
  - Depends on: none
- [x] Task 5.1 (M): Schema migration (upgrade to v3 with `BigramStatistics`, `KeyReviewSchedule`, `PlacementResults`, indexes, and new `UserSettings` keys `PaceCaretMode`, `PaceTargetWpm`, `KeyboardFadeStage`, `FingerGuideMode`, `ShowWpmInCourse`)
  - Acceptance: upgrade from prior versions keeps all existing data; migration failure rolls back and the app still opens; test uses a temp DB file
  - Depends on: 5.0
- [x] Task 5.2 (M): `BigramTracker`
  - Acceptance: records letter-pair latency only when both characters were typed correctly in a row; ignores pauses above cutoff (default 2000 ms); uses EMA (configurable alpha); tests cover first sample, outlier drop, error counting, backspace sequences, empty/1-char input
  - Depends on: 5.1
- [x] Task 5.3 (M): `WeaknessScorer`
  - Acceptance: pure function ranking keys and bigrams by blend of error rate and latency z-score vs user's own average; minimum-sample guard (default 20 samples); deterministic ranking tests; division-by-zero and tiny-dataset cases handled
  - Depends on: 5.2
- [x] Task 5.4 (M): Weighted drill generator
  - Acceptance: ~50% of words/pseudo-words contain a top weak key or bigram, rest drawn from unlocked letters; strictly omits locked letters; statistical test over 1,000 generated drills hits weighting target within tolerance
  - Depends on: 5.3
- [x] Task 5.5 (M): `ReviewScheduler` (spaced repetition, SM-2-lite)
  - Acceptance: keys that stay mastered get longer intervals, failed keys reset to short intervals; exposes `GetDueKeys(now)`; clock injected via `TimeProvider` / interface
  - Depends on: 5.1
- [x] Task 5.6 (M): `PlacementEngine`
  - Acceptance: takes 60-second common-words test result and outputs recommended track, starting lesson, and unlocked adaptive letters from validated embedded JSON mapping table with safe fallback to Lesson 1
  - Depends on: 5.1
- [x] Task 5.7 (M): `SessionPlanner`
  - Acceptance: builds "Today's Session" (60s warm-up -> next incomplete course drill with tip banner support -> weak-key/bigram review -> due-key review) capped at `min(dailyGoal, 15 min)` for new users; skips empty steps; unit tested across all user profiles
  - Depends on: 5.3, 5.5, 5.6
  - Gate: Stop and wait for user approval. `dotnet test` green with zero warnings.

## Milestone 6 — Guided Learning UX
- [x] Task 6.1 (M): "Start Today's Session" on Practice
  - Acceptance: one primary action on Practice screen (visible only before typing starts); plan steps run back to back with slim progress strip and inline tip banner when course drill follows a Tip step; Esc/Zen works; interrupted session resumes at current step
  - Depends on: 5.7
- [x] Task 6.2 (M): First-launch placement flow
  - Acceptance: triggers automatically only when both `PlacementResults` and `Sessions` are empty; welcome -> 60s test -> result card with recommended track -> "Start here" or "Pick myself"; skippable; retake from Settings
  - Depends on: 5.6
- [x] Task 6.3 (L): Finger guide
  - Acceptance: `FingerMap` (key -> finger) in Core; native Canvas hand-diagram control highlights finger for next key; shown in `CoursePage` right panel and alongside Virtual Keyboard during `CourseLesson` drills; every QWERTY key maps to one finger (unit tested)
  - Depends on: 6.1
- [x] Task 6.4 (M): Wrong-key feedback
  - Acceptance: in strict mode, flash "pressed X / expected Y" near keyboard; after 2 consecutive misses on same expected key, show finger hint; resets on success; pressed-key value never persisted
  - Depends on: 6.3
- [x] Task 6.5 (M): Accuracy-first course mode
  - Acceptance: lesson JSON supports `ShowWpm` flag; early lessons show accuracy only until lesson accuracy target met once
  - Depends on: none
- [x] Task 6.6 (M): Keyboard fade ramp
  - Acceptance: `KeyboardFadeStage` (`0` full labels -> `1` no labels with home-row bumps -> `2` hidden); lesson JSON default stage with Settings override
  - Depends on: 6.3
- [x] Task 6.7 (S): Beginner-friendly defaults
  - Acceptance: fresh DB defaults daily goal to 15 min; upgraded DB keeps saved value; configurable 10–60 min
  - Depends on: 5.1
  - Gate: Stop and wait for user approval.

## Milestone 7 — Speed Tools and Insights
- [x] Task 7.1 (M): Pace caret
  - Acceptance: pure `GetGhostIndex(elapsed, targetWpm)` (`ms per char = 12000 / WPM`); modes `Off` / `Fixed WPM` / `Personal Best` / `Last session` configured in `TypingSettingsControl` and `SettingsPage`; rendered in `TypingStreamView` without allocations in render tick
  - Depends on: none
- [x] Task 7.2 (M): Bigram drills
  - Acceptance: "Drill my slowest pairs" launcher in Stats and Results screen; builds 2-minute drill from top bigrams; stored as `SessionType.BigramDrill`
  - Depends on: 5.4
- [x] Task 7.3 (M): Fatigue detector
  - Acceptance: rolling 30-keystroke windows; if accuracy drops 10+ points under session baseline for two consecutive windows (or session passes 20 min) after >=3 min, show quiet non-modal break suggestion once per session
  - Depends on: none
- [x] Task 7.4 (M): `InsightEngine` + results screen
  - Acceptance: surfaces one primary insight and one action on Results screen with neutral summary fallback when data is sparse
  - Depends on: 5.3
- [x] Task 7.5 (S): Spaced review hook
  - Acceptance: updates `KeyReviewSchedule` after each session; due keys feed Today's Session
  - Depends on: 5.5, 6.1
- [x] Task 7.6 (M): Real-world text pools for Test
  - Acceptance: embedded `Quotes`, `Code snippets`, `Numbers`, and `Punctuation` pools selectable under `Real-world pools` in Test settings dialog with preview list; validated on startup
  - Depends on: none
  - Gate: Stop and wait for user approval.

## Milestone 8 — Polish and Release
- [x] Task 8.1 (M): Contrast and legibility pass
  - Acceptance: disabled/locked text >= 3:1 and muted text >= 4.5:1 against card/canvas in Dark and Light (verified by test over token table)
- [x] Task 8.2 (S): Rename "Optimal Duration" -> "Skip ahead when mastered"
  - Acceptance: label updated on Course card, Settings, and docs; stored setting key unchanged
- [x] Task 8.3 (M): Course page layout
  - Acceptance: right panel shows Finger Guide and lesson focus keys; all step rows show one-line description even when locked; responsive across narrow and wide widths
- [x] Task 8.4 (M): Stats additions
  - Acceptance: weekly trend (WPM & accuracy), weak-bigram list, "due for review" keys using native Canvas/Shapes
- [x] Task 8.5 (M): Accessibility and keyboard navigation for all new UI
  - Acceptance: `AutomationProperties` on new controls; full keyboard path; screen reader announcements for target key and hint
- [x] Task 8.6 (S): Final verification
  - Acceptance: `dotnet build` with TreatWarningsAsErrors passes; `dotnet test` passes with >=80% Core coverage; DB upgrade verified; docs match shipped behavior
  - Gate: Final report to user.

## Milestone 9 — Branding Assets
- [x] Task 9.1 (S): Import asset pack (`assets-raw/`, `tools/assets/build_assets.py`, `src/TypeForge.App/Assets/`)
- [x] Task 9.2 (S): App icon + manifest wiring (`TypeForge.App.csproj`, `Package.appxmanifest`, `MainWindow.xaml.cs`, `installer/TypeForge.iss`)
- [x] Task 9.3 (S): Core `EmptyStateCatalog` + `EmptyStateCatalogTests`
- [x] Task 9.4 (M): `EmptyStateView` + integration in Stats (`NoStats`, `NoWeakKeys`, `NoTestHistory`)
- [x] Task 9.5 (S): Break reminder card with `break-reminder.png` in `PracticePage`
- [x] Task 9.6 (S): Welcome hero (`hero-welcome.png`) in placement flow (`PracticePage` & `CoursePage`, 900px responsive breakpoint)
- [x] Task 9.7 (S): About card with `typeforge-logo-lockup.png` in `SettingsPage`
- [x] Task 9.9 (M): Light-theme `#0D0D0D` rounded panel backing for all dark-only illustrations and lockup
