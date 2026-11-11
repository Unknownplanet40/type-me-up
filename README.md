# Vibe Coding Template

Copy this folder to start a new project, then open it in Google Antigravity.

Prerequisite: Node.js installed on PATH (needed for npx / chrome-devtools-mcp).

## How to use

1. Copy `temps-plates/` to your new project folder (or use GitHub "Use this template").
2. Run `powershell -ExecutionPolicy Bypass -File scripts/init-project.ps1` to initialize Git and exclude agent/template files from project commits.
3. Open the new folder in Antigravity.
4. Run `/discovery` — first the New Project checklist (app, users/roles, stack, design, data/sensitive), then 7 MVP questions.
5. Approve the generated docs in `docs/`.
6. Run `/build-feature` to build tasks one by one.

## Default stack

Next.js + TypeScript + Tailwind + shadcn + Supabase + Vercel. Override in discovery if you want another stack.

## What is included

- `AGENTS.md` — project rules, honesty, coding, security, update flow
- `.agents/rules/` — commit workflow and coding rules
- `.agents/skills/` — discovery, build-feature, handoff skills
- `.github/` — issue and PR templates plus release workflow
- `docs/templates/` — PRD, DESIGN, TECH, TASKS templates
- `handoff.md` — session memory, updated after each push

## Update flow

Push to `main` triggers GitHub Actions. CI bumps `VERSION`, tags the release, and publishes Release notes. The app reads `version.json` or the latest Release for "What is new". There is no CHANGELOG.md.

## Native Windows preset
Only if discovery Stack question is answered 2 (Native Windows WinUI3): copy `docs/presets/WINUI3_STARTER_GUIDE.md` and `docs/presets/WINUI3_UPDATE_WORKFLOW.md` into project `docs/` via `scripts/copy-winui3-presets.ps1` and invoke `/winui3` skill.

## Security
- Copy `.env.example` to `.env.local` for local dev. Never commit `.env.local`.
- Production keys go in the hosting dashboard. CI keys go in GitHub Secrets.
