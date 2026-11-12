# Design — TypeForge

## Priority
1. User's explicit design instructions: ultra-minimal Keybr.com-inspired dark interface (`#0D0D0D` near-black background, `#A78BFA` soft purple accent used sparingly on active target key and cursor), full-screen layout with zero left sidebar, whisper-quiet metrics line, heat-colored key pills & QWERTY keyboard, and calm daily-habit polish.
2. Cohesive dark-first palette (`#0D0D0D` + `#A78BFA`) across all routes, with clean Light theme option preserved in Settings.

## Visual Tokens
- Backdrop: Native `DesktopAcrylicBackdrop` with translucent `#C80D0D0D` Dark canvas tint (`#CCFAFAFA` Light)
- Elevated Card Surface: Dark `#BF141418` with `#242429` 1px hairline border (Light `#D9FFFFFF` with `#E4E4E7` 1px border)
- Subtle Keycap / Pill Neutral Surface: Dark `#18181E` with `#2E2E38` border (Light `#F4F4F5` with `#D4D4D8` border)
- Accent (Sparingly Applied): Soft Purple `#A78BFA` (Light `#7C3AED`), subtle purple glow fill `#24A78BFA`
- Text Primary: `#F4F4F5` (Dark) / `#18181B` (Light)
- Text Secondary: `#A1A1AA` (Dark) / `#52525B` (Light) (>= 4.5:1 contrast)
- Text Muted: `#8E8E99` (Dark) / `#63636E` (Light) (>= 4.5:1 contrast against all card/canvas/key surfaces)
- Text Disabled / Locked: `#6E6E78` (Dark) / `#7D7D87` (Light) (>= 3.0:1 contrast against all card/canvas/key surfaces)
- Mastery / Heat Green (Strong / Correct): `#34D399` foreground, `#14261E` pill/key fill, `#1F4434` border
- Mastery / Heat Yellow (Needs Work): `#FBBF24` foreground, `#282112` pill/key fill, `#4D3D19` border
- Mastery / Heat Red (Weak / Mistake): `#F87171` foreground, `#2A1517` pill/key fill, `#522327` border
- Corner Radii: 12px for cards, 8px for top-bar pills and buttons, 6px–7px for letter progress pills and QWERTY keycaps
- Typography: Segoe UI Variable for UI text, Cascadia Mono / Consolas (`26px–28px`, generous line-height) for typing stream, tabular numerals for HUD metrics.

## Shell & Top-Bar Navigation (No Left Sidebar)
- Very thin `32px` top bar aligned 1:1 with Windows `Standard` (`32px`) caption controls (`-`, maximize, close) over seamless Acrylic backdrop (`0px` left sidebar):
  - Left: `TypeForge` wordmark + 4 quiet low-contrast routes (`Course`, `Practice`, `Test`, `Stats`), with active route marked by subtle `#A78BFA` accent indicator.
  - Right: Compact streak badge (crisp Flame vector glyph + `3`) + `Settings` gear icon.
- Course opens immediately on startup as the first tab and home screen. Dashboard KPIs and "Review Weak Keys" quick launcher live cleanly inside `Stats`.

## Typing Practice Screen (`PracticePage.xaml`)
1. **Quiet Metrics Line**: Single inline line `64 WPM · 98.2% · 2 mistakes · 00:42` paired with a compact `12/30 min` daily goal progress bar on the far right.
2. **Letter Mastery Pill Strip**: Horizontal row of small rounded key pills colored from red -> yellow -> green based on mastery (muted when unmeasured); current target key highlighted with `#A78BFA` border and subtle purple glow.
3. **Centered Practice Text Stream**: Large centered monospace passage (`26px–28px`) with generous spacing directly on the `#0D0D0D` canvas; completed letters in soft green (`#34D399`), mistakes in soft red (`#F87171`), current target letter highlighted in `#A78BFA` with soft purple backdrop/underline glow.
4. **Clean Virtual QWERTY Keyboard**: Slightly rounded keys (`CornerRadius="7"`) with performance heat coloring (`green` = strong, `yellow` = needs work, `red` = weak, dark `#141416` = neutral modifiers), tactile bumps on `F` and `J`, and `#A78BFA` glow ring on the active target key. Optional finger-zone coloring toggle in Settings.
5. **Minimal Hidden Controls**: Hover-reveal top-right utility strip (Audio mute, Restart stream, Zen/Fullscreen, Results modal) and keyboard shortcuts (`Esc` Zen, `Tab` Restart). No permanent buttons or banners during typing.

## Anti-Slop Rules
- No left sidebar, no heavy gradient cards, no bright decorative borders, no marketing copy.
- No raw emojis in UI (use crisp vector / Segoe Fluent glyphs such as custom Flame path for streak).
- Responsive full-width layout (`HorizontalAlignment="Stretch"`) across all window widths.

## Brand Assets
- Mark: dark keycap with a purple tactile-bump/cursor bar and a four-point spark (top-right). Purple is the only accent.
- Illustrations: flat, dark-only, transparent PNG, 1px hairlines, one focal accent per image (purple = neutral/ready, green = all clear).
- Light theme: illustrations sit on a `#0D0D0D` rounded panel (`CornerRadius 12`, `Padding 16`).
- Raster wordmark is for About / README / store only; the top bar uses live-text "TypeForge".
- No marketing copy on empty states: a short title plus one sentence.
