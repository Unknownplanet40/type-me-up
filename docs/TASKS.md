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
  - Acceptance: Windows App SDK configured, Mica backdrop enabled, MVVM Toolkit & DI wired
  - Depends on: Approved Milestone 1
- [x] Task 2.2: Implement NavigationView shell, Focus Mode, and Dashboard view
  - Acceptance: Navigation between 6 primary routes; Focus Mode toggle; Dashboard displaying current stats and "Review Weak Keys" quick action
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
  - Acceptance: 1, 3, and 5-minute countdown tests from embedded pools plus custom pasted text option with isolated session history
  - Depends on: Task 3.4
  - Gate: Stop and wait for user approval

## Milestone 4 — Statistics, Certificate Export, and Final Polish
- [x] Task 4.1: WPM and Accuracy history charts using WinUI Canvas & Shapes
  - Acceptance: Native line/bar rendering across sessions without third-party chart dependencies
  - Depends on: Approved Milestone 3
- [x] Task 4.2: Keyboard Heatmap visualization
  - Acceptance: QWERTY key grid colored by error intensity and latency metrics
  - Depends on: Task 4.1
- [x] Task 4.3: Milestone Certificate Export
  - Acceptance: RenderTargetBitmap generation of course completion certificate with date, WPM, and stars exportable to PNG
  - Depends on: Task 4.2
- [x] Task 4.4: Daily practice streak tracker
  - Acceptance: Calendar-day streak calculation stored locally
  - Depends on: Task 4.3
- [x] Task 4.5: Settings view and preference storage
  - Acceptance: Light/Dark/System theme switcher, typing font size slider, keyboard toggle, adaptive mode switch (phonetic vs dictionary), key sound toggle
  - Depends on: Task 4.4
- [x] Task 4.6: Full accessibility, keyboard navigation, and zero-warning verification
  - Acceptance: `dotnet build` with TreatWarningsAsErrors passes; `dotnet test` passes with >=80% coverage; end-to-end verification complete
  - Depends on: Task 4.5
  - Gate: Final report to user
