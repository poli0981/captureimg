# Contributors

_Last updated: 2026-10-19_

## Maintainer

- **poli0981** — project owner: design decisions, code review, releases. Every change is reviewed and
  accepted by the maintainer before it lands on `main`.

## AI assistance

CaptureImage is developed with substantial help from an AI coding assistant — **Claude
(Anthropic), used through Claude Code**. The work is pair-programming style: the maintainer sets the
requirements, chooses the trade-offs and reviews every change, while the assistant drafts code,
tests, Windows interop, user-interface layouts, translations, commit messages and documentation
(including this file).

Commits where the assistant wrote a significant part of the change carry a trailer such as:

```text
Co-Authored-By: Claude <model name> <noreply@anthropic.com>
```

following GitHub's
[multiple-authors convention](https://docs.github.com/en/pull-requests/committing-changes-to-your-project/creating-and-editing-commits/creating-a-commit-with-multiple-authors).
The trailer is attribution only; the project is licensed under **GPL-3.0-or-later** by its human
maintainer and contributors. If AI-assisted code matters for your use (for example, a policy against
shipping it), the full history is available with `git log` and `git show`.

## How to contribute

See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for the development setup, the required checks and the
conventions. By contributing you agree that your work is distributed under GPL-3.0-or-later as part
of CaptureImage (see [`legal/TERMS.md`](legal/TERMS.md), section 5).

## Translation help wanted

All 11 languages were produced or completed with AI assistance. Native speakers who can review the
strings in `src/CaptureImage.ViewModels/Resources/Strings/Strings.*.resx` are warmly invited to
open a pull request — small, focused changes with a short reason per string are easiest to review.

## Acknowledgements

CaptureImage is built on open-source work; the full list with licenses is in
[`legal/THIRD_PARTY_NOTICES.md`](legal/THIRD_PARTY_NOTICES.md). Special thanks to:

- **WPF UI** (lepo.co) for the Fluent controls the 2.x interface is built with.
- **H.NotifyIcon** for the tray icon.
- **OBS Studio**, whose public Windows Graphics Capture code was a useful reference.
- **Velopack** for the installer and updater.
- **Vortice.Windows** for Direct3D, DXGI and WIC bindings, and **SkiaSharp** for image encoding.
- **Serilog** and **CommunityToolkit.Mvvm**.
- **Avalonia UI** (versions 1.0–1.2) and **WinUI 3 / Windows App SDK** (1.3–1.5), the frameworks
  earlier versions of CaptureImage were built on.
