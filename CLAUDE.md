# CLAUDE.md

Guidance for AI coding assistants (Claude Code and similar) working in this
repository. Human contributors: see [CONTRIBUTING](docs/CONTRIBUTORS.md).

## What this is

CaptureImage — a tray-resident, hotkey-driven Windows screenshot tool for games
(Windows.Graphics.Capture + Direct3D 11). C# on **.NET 10**, GPL-3.0-or-later.
Solo maintainer: poli0981. The maintainer communicates in Vietnamese.

## Build, test, run

```powershell
dotnet restore CaptureImage.sln            # lock files; CI uses --locked-mode
dotnet build CaptureImage.sln -c Release   # warnings are errors
dotnet test --solution CaptureImage.sln -c Release --no-build   # Microsoft.Testing.Platform (see global.json)
dotnet run --project src/CaptureImage.App -c Release
```

- SDK pinned in `global.json` (10.0.401, `latestFeature`); tests are xunit.v3 on
  Microsoft.Testing.Platform — `--logger trx` does not exist, use `--report-xunit-trx`
  and `--coverage --coverage-output-format cobertura`.
- Local tools (`dotnet tool restore`): `vpk`, `jb` (InspectCode), `nuget-license`,
  `reportgenerator`, `dotnet-CycloneDX`.
- This machine may have a released CaptureImage installed with real user settings
  under `%LocalAppData%\CaptureImage\`. Do not run dev builds against real user data.

## Layout and layering rules

```text
src/CaptureImage.Core            portable net10.0 — models, abstractions, pipeline; no Windows APIs
src/CaptureImage.Infrastructure  Windows implementations (WGC/D3D11, hotkeys, settings, logging, update)
src/CaptureImage.ViewModels      portable MVVM layer (CommunityToolkit.Mvvm); no UI-framework types
src/CaptureImage.UI              views, controls, converters, resx localization (11 languages)
src/CaptureImage.App             entry point, DI composition root, logging setup, single instance
tests/*                          one test project per src project
```

- Core and ViewModels must stay free of UI-framework and `Windows.*` references.
- Every resx key must exist in all 11 `Strings.*.resx` files.
- Central Package Management: versions live only in `Directory.Packages.props`.
- Version lives only in `Directory.Build.props` (`<Version>`); the release workflow
  refuses a tag that does not match it.

## Code conventions

- Every `.cs` file starts with the SPDX header (IDE0073):
  `// SPDX-License-Identifier: GPL-3.0-or-later` / `// Copyright (c) CaptureImage Contributors`.
- LF line endings everywhere (`.gitattributes` `eol=lf`), file-scoped namespaces,
  usings outside the namespace, no unused members/usings (enforced at build).
- `dotnet format` must only be run with the safe subsets used by CI:
  `dotnet format whitespace` and `dotnet format style --diagnostics IDE0073 IDE0161 IDE0065 IDE0005`.
  A plain `dotnet format` deletes XAML event handlers (it cannot see XAML-generated code).
- Pass `CancellationToken`s through (SonarCloud S8949); tests use
  `TestContext.Current.CancellationToken` (xUnit1051).

## Pull requests and CI

- `main` is protected by a ruleset: PR required, signed commits, linear history,
  squash merge only (PR title = commit subject, Conventional Commits), branch must
  be up to date, no bypass.
- Required checks: `build`, `format`, `deadcode` (InspectCode, file-local rules),
  `audit` (vulnerable packages), `license` (shipped dependency licenses vs
  `.github/license/allowed-licenses.json`), `publish-smoke`, `pr-title`,
  `dependency-review`, `workflow-lint` (actionlint + zizmor), `docs` (markdownlint +
  offline lychee), plus CodeQL via the code-scanning rule.
- Never add `paths:` filters to a workflow that provides a required check.
- Actions are pinned to full commit SHAs with a `# vX.Y.Z` comment.
- SonarCloud (automatic analysis, project `poli0981_captureimg`) comments on PRs —
  fix what it reports; it is not a required check.
- Dependabot NuGet updates only work because `EnableWindowsTargeting=true` is set
  in `Directory.Build.props` (the updater runs on Linux).

## Releases

1. Bump `<Version>` in `Directory.Build.props` and add a `## [x.y.z] - date` section
   to `CHANGELOG.md` (it becomes the release notes and the in-app update notes).
2. After the PR merges: `git tag -s vX.Y.Z -m "…"` and push the tag.
3. `release.yml` builds, packs with Velopack (`packId CaptureImage`, `mainExe
   CaptureImage.App.exe`, channel `win` — never change these), attaches SBOM,
   SHA256SUMS and provenance attestations, and creates a **draft** release.
4. Verify the draft, then `gh release edit vX.Y.Z --draft=false` (this triggers the
   Discord announcement).

## Contacts used in docs and the About page

`contact@poli0981.dev` (general) and <https://poli0981.dev/links>; role aliases at the
same domain: security@, privacy@, legal@, copyright@, dmca@, takedown@, code@,
sponsor@, games@, collab@, press@.
