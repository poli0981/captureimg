# CLAUDE.md

Guidance for AI coding assistants (Claude Code and similar) working in this repository. Human
contributors: see [CONTRIBUTING.md](CONTRIBUTING.md).

## What this is

CaptureImage — a tray-resident, hotkey-driven Windows screenshot tool for games
(Windows.Graphics.Capture + Direct3D 11). C# on **.NET 10**, WPF + WPF UI 4.x, GPL-3.0-or-later.
Solo maintainer: poli0981, who communicates in Vietnamese.

## Build, test, run

```powershell
dotnet restore CaptureImage.sln            # lock files; CI uses --locked-mode
dotnet build CaptureImage.sln -c Release   # warnings are errors; src/ uses AnalysisMode=Recommended
dotnet test --solution CaptureImage.sln -c Release --no-build   # Microsoft.Testing.Platform (global.json)
dotnet run --project src/CaptureImage.App -c Release -- --data-dir .\.devdata --instance-id dev
```

- SDK pinned in `global.json` (10.0.401, `latestFeature`). Tests are xunit.v3 on
  Microsoft.Testing.Platform: use `--report-xunit-trx` and
  `--coverage --coverage-output-format cobertura` (there is no `--logger trx`).
- **Never run a dev build against real user data.** Always pass `--data-dir <temp>` and
  `--instance-id <name>`; the installed app keeps its data in `%LocalAppData%\poli0981\CaptureImage`
  (1.x kept it in `%LocalAppData%\CaptureImage`).
- `--smoke-test` opens every page and exits 0; `--screenshots <dir>` also writes a PNG per page
  (handy for checking UI changes without driving the mouse).
- UI tests (`tests/CaptureImage.App.UiTests`, FlaUI) move the mouse and type; they skip unless
  `CAPTUREIMAGE_UI_TESTS=1`. Don't run them on the maintainer's desktop.
- Local tools (`dotnet tool restore`): `vpk`, `jb` (InspectCode), `nuget-license`,
  `reportgenerator`, `dotnet-CycloneDX`.

## Layout and layering rules

```text
src/CaptureImage.Core            net10.0 — models, abstractions, settings migration, CaptureCoordinator; no Windows APIs
src/CaptureImage.Infrastructure  Windows implementations (WGC/D3D11, RegisterHotKey, WinEvent, WIC/Skia, settings, Velopack, Serilog)
src/CaptureImage.ViewModels      CommunityToolkit.Mvvm view models + Resources/Strings (11 languages); no UI-framework types
src/CaptureImage.UI              WPF + WPF UI: App.xaml, windows, pages, overlays, toasts, tray, UI services
src/CaptureImage.App             Main: Velopack hooks, single instance, Serilog, DI composition root
tests/*                          Core, Infrastructure, ViewModels tests + App.UiTests (FlaUI)
```

- Core and ViewModels stay free of UI-framework and `Windows.*` references; view models talk to
  Core abstractions only (no `Process.Start`, file I/O or WPF types).
- Every resx key must exist in all 11 `Strings*.resx` files with the same `{0}` placeholders, and
  every key must be used — `LocalizationCompletenessTests` enforces both.
- Central Package Management: versions live only in `Directory.Packages.props`; after changing
  references run `dotnet restore --force-evaluate` and commit the `packages.lock.json` files.
- The version lives only in `Directory.Build.props` (`<Version>`).

## Code conventions

- Every `.cs` file starts with the SPDX header (IDE0073):
  `// SPDX-License-Identifier: GPL-3.0-or-later` / `// Copyright (c) CaptureImage Contributors`.
- LF line endings (`.gitattributes` `eol=lf`), file-scoped namespaces, usings outside the namespace,
  no unused members or usings (enforced at build), `[LoggerMessage]` source-generated logging.
- Run `dotnet format` only with the CI subsets: `dotnet format whitespace` and
  `dotnet format style --diagnostics IDE0073 IDE0161 IDE0065 IDE0005`. A plain `dotnet format`
  deletes XAML event handlers.
- Pass `CancellationToken`s through (SonarCloud S8949); tests use
  `TestContext.Current.CancellationToken` (xUnit1051).

## Pitfalls already hit

- System.Text.Json source generation treats `init` properties as constructor parameters, so a
  `[JsonExtensionData]` property must have a plain `set`.
- WPF UI: setting `Style` on a `ui:CardControl` (or `BasedOn="{StaticResource {x:Type …}}"`)
  replaces its implicit style and hides header/icon — use `Margin` etc. instead. Pages need
  `ScrollViewer.CanContentScroll="False"` or `NavigationView` wraps them in a ScrollViewer (lists lose
  virtualization). `TitleBar.Header` replaces the title text. `NavigationViewItem` has no UIA Invoke
  pattern.
- Never `Hide()`/`Close()`/`Shutdown()` from inside `Window.Closing`; defer with the dispatcher.
  Exits go through `App.RequestExit()`.
- WPF projects exclude `System.IO` from implicit usings (`Path` clashes with `System.Windows.Shapes`).

## Pull requests and CI

- `main` is protected by a ruleset: PR required, signed commits, linear history, squash merge only
  (PR title = commit subject, Conventional Commits), branch up to date, no bypass. Release tags `v*`
  can't be moved or deleted.
- Required checks: `build`, `format`, `deadcode` (InspectCode, file-local rules), `audit`,
  `license` (shipped dependencies vs `.github/license/allowed-licenses.json`), `publish-smoke`
  (publish + `--smoke-test` + page screenshots artifact), `pr-title`, `dependency-review`,
  `workflow-lint` (actionlint + zizmor), `docs` (markdownlint + offline lychee), plus CodeQL via the
  code-scanning rule. `ui-tests` runs too but is not required yet.
- Never add `paths:` filters to a workflow that provides a required check. Actions are pinned to
  full commit SHAs with a `# vX.Y.Z` comment.
- SonarCloud (automatic analysis, project `poli0981_captureimg`) comments on PRs — fix what it
  reports.
- Dependabot NuGet updates rely on `EnableWindowsTargeting=true` in `Directory.Build.props` (the
  updater runs on Linux).
- NuGet prunes the WPF projects' Windows Desktop packages (System.Drawing.Common, EventLog, …) only
  when restoring on Windows, so a Linux restore never matches the lock files. Jobs that restore run
  on `windows-latest`; if a Dependabot PR fails `--locked-mode`, re-run
  `dotnet restore CaptureImage.sln --force-evaluate` on Windows and push the lock files.
- README screenshots come from `--screenshots` with seeded demo data, rendered on a separate,
  hidden desktop — never from a personal desktop, whose window titles show up on the Dashboard.

## Releases

1. Bump `<Version>` in `Directory.Build.props`; rename `## [Unreleased]` in `CHANGELOG.md` to
   `## [x.y.z] - date` (it becomes the release and in-app update notes).
2. After the PR merges: `git tag -s vX.Y.Z -m "…"` and push the tag.
3. `release.yml` builds, tests, publishes framework-dependent, smoke-tests, packs with Velopack
   (`packId CaptureImage`, `mainExe CaptureImage.App.exe`, channel `win`, `--framework
   net10-x64-desktop` — never change the first three), attaches SBOM, SHA256SUMS and provenance
   attestations, and creates a **draft** release.
4. Verify the draft (see `docs/RELEASING.md`), then `gh release edit vX.Y.Z --draft=false`.

## Contacts used in docs and the About page

`contact@poli0981.dev` (general) and <https://poli0981.dev/links>; role aliases at the same domain:
security@, privacy@, legal@, copyright@, dmca@, takedown@, code@, sponsor@, games@, collab@, press@.
