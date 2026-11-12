# Project Memory
<!-- Append-only. Never edit history. -->

## YYYY-MM-DD Title
- [Decision] ...
- [Rejected] ...

## 2026-11-11 TypeForge Architecture and Design Decisions
- [Decision] Typing Input Flow: Free-flow with error highlighting by default (cursor advances, backspace allowed, errors recorded), with optional 'Stop-on-error' strict mode toggle in Settings.
- [Decision] Audio Feedback: Embedded mechanical switch click on valid keystroke and soft alert tap on mistake, enabled by default with instant HUD mute button and Settings toggle.
- [Decision] Adaptive Unlock Profiles: Three selectable presets: Casual (650ms / 90%), Standard (500ms / 95%, default), and Pro (350ms / 98%).
- [Decision] Milestone 0 Preview: Deliver runnable WinUI 3 desktop shell prototype with dummy data for visual and layout review before production logic.
- [Decision] Certificate Personalization: Dedicated certificate preview modal allowing custom user display name input and local PNG export via RenderTargetBitmap.
- [Rejected] Background TypingMeter: Rejected system-wide key hooks (SetWindowsHookEx) due to privacy concerns and antivirus false positives.
- [Rejected] Online multiplayer & cloud sync: Rejected to preserve 100% offline-first reliability.
- [Rejected] AGPL-3.0 code inclusion: Rejected in favor of clean-room C# algorithmic implementation.

## 2026-11-12 Apple-Inspired Design & Keybr-Style HUD Decisions
- [Decision] Visual Aesthetic: macOS-inspired Dark & Light system (#18181A / #1E1E20 dark canvas, frosted translucent sidebar, 12-16px rounded cards, subtle 1px hairline borders, Apple electric blue #0A84FF accent).
- [Decision] Typing Practice HUD: Open Keybr-style layout with compact top metrics strip (Speed, Accuracy, Score), All-Keys letter status tiles with locked diagonal slash, Daily goal progress bar, floating top-right icon actions, borderless centered monospace text stream with middle-dot space markers and underline cursor.
- [Decision] Virtual Keyboard Style: Color-coded finger zones matching ergonomic assignments (muted green pinky, olive ring, gold middle, teal/rose index, warm terracotta spacebar), tactile bumps on F/J, and circular highlight ring on target key.
- [Decision] Navigation & Responsiveness: Custom macOS-style frosted sidebar with rounded pill items, collapse toggle, single Settings item, and 100% full-width fluid content area (no fixed MaxWidth).
- [Decision] Daily Practice Goal: Default 30 minutes/day goal (configurable 10-60 mins in Settings) tracked in HUD and Dashboard.


## 2026-11-12 Ultra-Minimal Keybr-Inspired Shell & Heat Palette
- [Decision] Shell & Navigation: Removed left sidebar; launch directly into full-screen Practice view with a very thin top bar containing 'TypeForge' and 4 quiet routes (Practice, Course, Test, Stats) on the left, streak badge + Settings gear on the right, folding Dashboard overview KPIs into Stats.
- [Decision] Visual System: Pure-black default Dark theme (#0D0D0D canvas, #141416 cards, #242429 hairlines) with soft purple accent (#A78BFA) used sparingly on active target key, active cursor, and active navigation, plus Light mode option retained in Settings.
- [Decision] Keyboard Coloring: Performance heat coloring (green strong, yellow needs work, red weak, #A78BFA glow on target key) is default on both key progress pills and bottom virtual QWERTY keyboard, with finger-zone coloring retained as an optional Settings toggle.
- [Decision] Practice Chrome: Zero visible buttons on Practice canvas; quiet controls (Mute, Restart, Zen/Fullscreen, Results preview) appear only on hover at top-right or via keyboard shortcuts, while ergonomic tips live inside Course cards and Settings.
- [Rejected] Permanent left sidebar & always-visible toolbar buttons during active typing.

## 2026-11-12 Acrylic Backdrop & Titlebar Alignment
- [Decision] Window Backdrop: Switched to DesktopAcrylicBackdrop paired with translucent #C80D0D0D Dark tint (#CCFAFAFA Light) and translucent #BF141418 cards for subtle frosted depth.
- [Decision] Top Bar Alignment: Set top bar height to 32px matching Windows TitleBarHeightOption.Standard caption controls (using RightInset DPI scaling) with theme-matched caption hover states.

## 2026-11-12 Multi-Track Course & Benchmark Statistics Alignment
- [Decision] Multi-Track Course Catalog: Upgraded from single flat 7-level list to 4 selectable tracks (`Fast Touch Typing Course` 12 lessons, `Speed Building Course`, `Numbers Course`, `Special Marks Course`) accessible via a Course Switcher view/drawer in TypeForge dark/purple styling.
- [Decision] Lesson Sub-Steps & Optimal Duration: Each course lesson uses numbered tabs (`1..N`) and sequential checklist steps (`Tip`, `NewKeys`, `KeyDrill`, `WordDrill`, `ParagraphDrill`) with inline Tip cards, checkmarks, bottom course progress bar, and an `Optimal Duration` toggle (~50% shortened drill length when prior accuracy >= 96% and WPM target met).
- [Decision] Benchmark Statistics Tabs & Certificate Export: Statistics view features `Typing Speed` (Gross & Net WPM bar charts with `OK`/`Good`/`Very good` benchmark bands) and `Accuracy` (Accuracy bar chart vs `94%` Goal line + sorted `Difficult Keys` chart with `OK`/`Difficult`/`Problematic` bands), paired with interactive QWERTY Heatmap and `RenderTargetBitmap` PNG Progress Report / Certificate export.


## 2026-11-12 Learning-Speed Improvement Plan
- [Decision] Add a learning engine in Core: bigram latency tracking, weighted weak-key/bigram drills, spaced key review, placement test, and a "Today's Session" planner.
- [Decision] Course UX: finger guide default-on in Course (shown in Course right panel and alongside Virtual Keyboard during CourseLesson drills), accuracy-first early lessons, keyboard fade ramp, wrong-key hints in strict mode.
- [Decision] Today's Session handles Course Tip steps by showing the tip text in a compact banner above the next drill and auto-completing the Tip step when the drill finishes.
- [Decision] First-launch placement triggers automatically only when both PlacementResults and Sessions are empty (brand-new users), with retake available in Settings.
- [Decision] Speed tools: pace caret (Off / Fixed / Personal Best / Last session) configured in shared TypingSettingsControl and SettingsPage, fatigue-based break suggestion, one-insight results screen, and Real-world pools radio option in Test -> Text tab.
- [Decision] New installs default to a 15-minute daily goal; existing users keep their value.
- [Decision] "Optimal Duration" renamed "Skip ahead when mastered" (setting key unchanged).
- [Rejected] Any system-wide key capture for these features; pressed-wrong-key values are never persisted.

## 2026-11-12 Brand Assets
- [Decision] 8 Gemini-generated assets adopted (icon, lockup, splash, welcome hero, 3 empty states, break reminder); certificate art dropped with the certificate feature.
- [Decision] Assets are processed by tools/assets/build_assets.py from assets-raw/ into src/TypeForge.App/Assets/: per-size icon crops, brightness-keyed transparent illustrations, MSIX tile/splash sizes, 7-frame ICO.
- [Decision] Empty-state copy and asset stems live in Core (EmptyStateCatalog) with tests; the app renders them through one EmptyStateView control.
- [Decision] Illustrations are dark-only; Light theme shows them on a dark panel until light variants exist.
- [Rejected] Blur-based background cleanup (erased hairlines and thinned the wordmark).
