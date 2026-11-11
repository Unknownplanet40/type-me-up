# PRD — TypeForge

## Problem
Learning touch-typing is often hindered by rigid curricula, paywalled features, intrusive advertisements, cloud tracking, or lack of targeted drills on an individual user's weakest keys. Users need a distraction-free, privacy-preserving, native Windows desktop application to master touch typing from beginner to advanced speeds.

## Users and Roles
- User: Solo local user practicing touch typing. No authentication or accounts required.
- Admin: None. Application is offline-first and runs strictly locally.

## Core Features (MVP)
1. Structured Typing Course: 7 progressive levels (Home Row to Combined Practice) with sequential unlock, star ratings (1-3), minimum WPM/accuracy gates, ergonomic guidance cards, and repeatable lessons.
2. Adaptive Typing Practice: Keybr-inspired letter progression with per-key latency, accuracy, and keystroke tracking; dual-mode generator offering both pronounceable phonetic pseudo-words and dictionary-based natural words using only unlocked letters.
3. Dynamic Weakness Review: One-click refresher session that aggregates the user's top 3-5 weakest keys across all sessions and builds a personalized 2-minute targeted drill.
4. Core Typing Engine: Real-time character evaluation, stopwatch-based net WPM and accuracy calculations, backspace and mistake tracking, per-key event logging, and Focus Mode (distraction-free typing). Zero WinUI dependencies.
5. Typing Practice Screen: Live character feedback (correct green, incorrect red, active highlight), complete virtual QWERTY keyboard with dynamic finger-zone highlights, live HUD metrics, and completion results screen.
6. Timed Typing Test & Custom Text: Standalone 1, 3, and 5-minute typing tests selected from embedded text pools or custom user-pasted text, with countdown timer and history logging without affecting course progression.
7. Statistics and Progress: Dashboard displaying overall WPM, accuracy, practice time, calendar day streak calculation, session history chart, and key heatmap using native WinUI shapes and Canvas.
8. Milestone Certificate Export: Clean, exportable completion certificate (PNG/PDF) upon passing the 7-level course or reaching major WPM milestones.
9. Settings: Local preference management for theme (Light/Dark/System), typing font size, virtual keyboard visibility, phonetic vs dictionary adaptive mode, and key sound toggle.

## Out of Scope
- Global background keystroke monitoring / system-wide TypingMeter (triggers antivirus heuristics and violates user privacy).
- Online leaderboards, multiplayer, and cloud sync (app is strictly offline-first).
- Copying proprietary texts, branding, or AGPL-licensed code from Keybr or TypingMaster.
- Non-QWERTY physical layouts for initial MVP (future milestone).
- Paid tiers, in-app purchases, or advertisements.

## Data Entities (checklist)
- [x] Entities listed with fields and required/optional
  - Lessons (LessonId [PK, text], LevelId [int], Title [text], Description [text], Content [text], MinimumWpm [int], MinimumAccuracy [real], OrderIndex [int])
  - LessonProgress (LessonId [PK, FK, text], IsUnlocked [int/bool], IsCompleted [int/bool], BestWpm [real], BestAccuracy [real], Stars [int], CompletedAt [text, nullable])
  - PracticeSessions (SessionId [PK, text], SessionType [text], StartedAt [text UTC], DurationSeconds [real], NetWpm [real], Accuracy [real], CorrectCharacters [int], TotalKeystrokes [int], Mistakes [int])
  - KeyStatistics (Character [PK, text], TotalKeystrokes [int], CorrectKeystrokes [int], IncorrectKeystrokes [int], AverageLatencyMs [real])
  - AdaptiveProgress (Character [PK, text], IsUnlocked [int/bool], StageIndex [int])
  - Settings (SettingKey [PK, text], SettingValue [text])
- [x] Who can create/edit/delete each entity: Local application user only.
- [x] Validation rules per field: Non-negative WPM, accuracy bounded 0.0-100.0%, non-empty lesson IDs and content, valid ISO 8601 UTC timestamps.

## User Flows
- [x] Flow 1: Launch application -> View Dashboard -> Continue Course -> Complete Lesson -> Results Screen -> Next Lesson Unlocked -> Return to Dashboard.
- [x] Flow 2: Open Adaptive Practice -> Toggle between Natural Words and Phonetic Pseudo-words -> Type generated drills -> Real-time key stats update -> Meet 95% accuracy and >=20 sample threshold -> Unlock next letter.
- [x] Flow 3: Select "Review Weak Keys" from Dashboard -> Launch targeted 2-minute drill on weakest keys -> Review post-drill accuracy delta.
- [x] Flow 4: Select Timed Test -> Choose 1/3/5 min or custom text -> Complete test -> View performance metrics -> Stored in history and charted on Statistics page.
- [x] Flow 5: Complete Course Level 7 -> Click "Export Completion Certificate" -> Generate high-resolution certificate image.
- [x] Flow 6: Navigate to Settings -> Toggle theme / font size / virtual keyboard / sound -> Persisted instantly to SQLite and applied.

## Assumptions
- Target OS is Windows 10 (version 1809 / build 17763 or higher) and Windows 11.
- All application state is stored locally in SQLite at `%LOCALAPPDATA%\TypeForge\typeforge.db`.
- Standard US QWERTY physical keyboard layout is assumed for finger zones.
- Embedded curricula, word lists, and test passages are embedded directly in application resources as validated JSON/text.
- All algorithms are written clean-room in pure C# with zero code copied from AGPL or commercial projects.
