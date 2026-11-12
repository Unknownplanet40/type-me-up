# PRD — TypeForge

## Problem
Learning touch-typing is often hindered by rigid curricula, paywalled features, intrusive advertisements, cloud tracking, or lack of targeted drills on an individual user's weakest keys and letter pairs. Users need a distraction-free, privacy-preserving, native Windows desktop application to master touch typing from beginner to advanced speeds.

## Users and Roles
- User: Solo local user practicing touch typing. No authentication or accounts required.
- Admin: None. Application is offline-first and runs strictly locally.

## Core Features
1. Multi-Track Structured Typing Course: 4 selectable tracks (`Fast Touch Typing Course` with 12 progressive lessons, `Speed Building Course`, `Numbers Course`, and `Special Marks Course`) with sequential checklist sub-steps (`Tip`, `NewKeys`, `KeyDrill`, `WordDrill`, `ParagraphDrill`), star ratings (1-3), minimum WPM/accuracy gates, ergonomic guidance cards, and "Skip ahead when mastered" shortened drill duration.
2. First-Launch Placement & Guided Daily Session: Optional 60-second placement test for brand-new users that recommends the right starting track/lesson, plus a one-click "Start Today's Session" flow combining a 60s warm-up, next incomplete course drill, weak-key/bigram review, and spaced-repetition due-key review.
3. Adaptive Typing Practice & Bigram Engine: Keybr-inspired letter progression with per-key and per-bigram EMA latency and error tracking, `WeaknessScorer` z-score ranking, SM-2-lite `ReviewScheduler`, and a weighted dual-mode drill generator (~50% words/pseudo-words targeting weak keys or bigrams while strictly omitting locked letters).
4. Core Typing Engine: Real-time character evaluation, stopwatch-based net WPM and accuracy calculations, backspace/forgive-errors/space-skips-words mechanics, pace caret (`Off` / `Fixed WPM` / `Personal Best` / `Last session`), rolling-window fatigue detector, and single-action `InsightEngine`. Zero WinUI dependencies.
5. Typing Practice & Course UX: Static start-aligned text stream with moving cursor indicator (`Block` / `Box` / `Line` / `Underline`, `Jumping` / `Smooth`), native Canvas finger-guide hand diagram, strict-mode wrong-key hints, keyboard fade ramp (`Full` -> `Unlabeled` -> `Hidden`), and accuracy-first early course HUD.
6. Configurable Typing Test: Duration and length modes (`15s`, `30s`, `1m`, `100`, `500`, `1000` chars) across Common words, Pseudo words, bundled Book paragraphs (*Alice's Adventures in Wonderland*), Real-world pools (`Quotes`, `Code snippets`, `Numbers`, `Punctuation`), and Custom text.
7. Statistics and Progress: Overview KPIs, benchmark WPM and Accuracy charts, weekly trend, weak-bigram list, due-for-review keys, and interactive QWERTY heatmap using native WinUI shapes and Canvas.
8. Settings: Local preference management for theme (Light/Dark/System), shared typing/appearance/sound settings (`Default`, `Mechanical`, `Typewriter`, `Soft`), pace caret, finger guide, keyboard fade stage, and automatic GitHub Releases self-updates.

## Out of Scope
- Global background keystroke monitoring / system-wide TypingMeter (triggers antivirus heuristics and violates user privacy).
- Persisting which wrong key was pressed beyond the live session (only expected-character errors are stored).
- Online leaderboards, multiplayer, and cloud sync (app is strictly offline-first).
- Copying proprietary texts, branding, or AGPL-licensed code from Keybr or TypingMaster.
- Non-QWERTY physical layouts for initial releases.
- Paid tiers, in-app purchases, or advertisements.

## Data Entities (checklist)
- [x] Entities listed with fields and required/optional
  - Lessons (LessonId [PK, text], LevelId [int], Title [text], Description [text], Content [text], MinimumWpm [int], MinimumAccuracy [real], OrderIndex [int])
  - LessonProgress (LessonId [PK, FK, text], IsUnlocked [int/bool], IsCompleted [int/bool], BestWpm [real], BestAccuracy [real], Stars [int], CompletedAt [text, nullable])
  - CourseExerciseProgress (ExerciseId [PK, text], IsUnlocked [int/bool], IsCompleted [int/bool], BestWpm [real], BestAccuracy [real], Stars [int], CompletedAt [text, nullable])
  - PracticeSessions (SessionId [PK, text], SessionType [text], StartedAt [text UTC], DurationSeconds [real], NetWpm [real], Accuracy [real], CorrectCharacters [int], TotalKeystrokes [int], Mistakes [int])
  - KeyStatistics (Character [PK, text], TotalKeystrokes [int], CorrectKeystrokes [int], IncorrectKeystrokes [int], AverageLatencyMs [real])
  - BigramStatistics (Bigram [PK, text, 2 chars], TotalCount [int], ErrorCount [int], AverageLatencyMs [real])
  - KeyReviewSchedule (Character [PK, text], LastReviewedAt [text, nullable], IntervalDays [real], EaseFactor [real], DueAt [text UTC])
  - PlacementResults (PlacementId [PK, text], TakenAt [text UTC], NetWpm [real], Accuracy [real], RecommendedTrack [text])
  - AdaptiveProgress (Character [PK, text], IsUnlocked [int/bool], StageIndex [int])
  - Settings (SettingKey [PK, text], SettingValue [text])
- [x] Who can create/edit/delete each entity: Local application user only.
- [x] Validation rules per field: Non-negative WPM, accuracy bounded 0.0-100.0%, 2-character bigrams, non-empty IDs and content, valid ISO 8601 UTC timestamps.

## User Flows
- [x] Flow 1: First launch (new user) -> Optional 60s Placement Test -> Recommended course track & starting lesson unlocked -> Start Course or Today's Session.
- [x] Flow 2: Click "Start Today's Session" on Practice -> Run 60s warm-up -> Next incomplete course drill (with inline tip banner if preceded by a Tip step) -> Weak-key/bigram drill -> Spaced-repetition due-key review.
- [x] Flow 3: Open Adaptive Practice -> Type weighted drills targeting weak keys and bigrams -> Real-time key/bigram stats and spaced review schedule update -> Unlock next letter when threshold met.
- [x] Flow 4: Select Test -> Configure duration/length and text source (Common words, Pseudo words, Book paragraphs, Real-world pools, Custom text) -> Complete test -> View insight and action on Results screen.
- [x] Flow 5: Navigate to Stats -> Review WPM/Accuracy trends, weak bigrams, due review keys, and QWERTY heatmap -> Launch "Drill my slowest pairs" or "Review Weak Keys".

## Assumptions
- Target OS is Windows 10 (version 1809 / build 17763 or higher) and Windows 11.
- All application state is stored locally in SQLite at `%LOCALAPPDATA%\TypeForge\typeforge.db`.
- Standard US QWERTY physical keyboard layout is assumed for finger zones.
- Embedded curricula, word lists, books, placement rules, and test passages are embedded directly in application resources as validated JSON.
- All algorithms are written clean-room in pure C# with zero code copied from AGPL or commercial projects.
