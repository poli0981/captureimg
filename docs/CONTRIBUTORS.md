# Contributors

_Last updated: 2026-04-15_

## Maintainer

- **poli0981** — project owner, design decisions, code review, release
  management. All commits are signed off by the maintainer before landing
  in the default branch.

## AI assistance

Development of CaptureImage was carried out with significant assistance
from the **Claude Opus 4.6** large language model (Anthropic), used
through Claude Code, during the 2026 development cycle. This assistance
was pair-programming-style: the maintainer drove requirements, chose
trade-offs, and reviewed every change, while the model generated code,
unit tests, XAML layouts, P/Invoke signatures, commit messages, and
documentation (including the file you are reading now).

Commits where AI authored significant portions of the diff carry the
trailer:

```
Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

This follows the git convention documented at
<https://docs.github.com/en/pull-requests/committing-changes-to-your-project/creating-and-editing-commits/creating-a-commit-with-multiple-authors>.
The co-author trailer is an attribution mechanism; it does not give the
model any copyright over the output — per Anthropic's usage terms and
current US Copyright Office guidance, AI-generated material is not
copyrightable by the model itself. The resulting project is licensed
under **GPL-3.0-or-later** by the human maintainer and contributors.

If this distinction matters for your use case (for example, your
organization prohibits shipping AI-assisted code in production), please
audit the git history. Every commit's full diff is available via
`git show` and the co-author trailer is visible in the commit message.

## How to contribute

Contributions are welcome. The general flow is:

1. Open an issue describing the bug or feature before starting work on
   anything non-trivial. A short discussion avoids duplicated effort.
2. Fork the repository and create a feature branch from `main`.
3. Keep commits small and focused. Use conventional-commits style:
   `feat(m5): …`, `fix(capture): …`, `docs(legal): …`, etc.
4. Run `dotnet build CaptureImage.sln` and `dotnet test CaptureImage.sln`
   locally. Every PR must ship with the full suite passing.
5. For UI changes, include before/after screenshots or a short video.
6. If your change was AI-assisted, say so in the PR description — it
   helps reviewers focus their attention.
7. Open a pull request.

By submitting a contribution, you agree that it can be distributed under
GPL-3.0-or-later as part of CaptureImage (see [TERMS.md](legal/TERMS.md)
§5).

## Translation help wanted

The Vietnamese (`vi-VN`) and Arabic (`ar-SA`) language packs were
produced with machine assistance. Native speakers who can review the
shipped strings are warmly invited to open a PR against
`src/CaptureImage.UI/Resources/Strings/Strings.*.resx`. Small
improvements are welcome — please submit the smallest possible diff and
explain the reasoning for each string change in the PR body.

## Acknowledgements

CaptureImage stands on the shoulders of several open-source projects.
See [`THIRD_PARTY_NOTICES.md`](legal/THIRD_PARTY_NOTICES.md) for the full
list with licenses. Special thanks to:

- The **Windows App SDK / WinUI 3** team at Microsoft for the modern
  Win11-native XAML framework underneath the v1.3+ UI.
- The **Avalonia UI** team — v1.0 through v1.2 of CaptureImage shipped on
  Avalonia, and the project owes a substantial debt to the cross-platform
  XAML framework that got us off the ground.
- The **H.NotifyIcon** team for the WinUI 3 tray-icon library that fills
  the gap WinAppSDK doesn't (yet) cover natively.
- The **OBS Studio** team for blazing the trail on Windows Graphics
  Capture integration — their public source code was a useful reference
  while wiring up `IGraphicsCaptureItemInterop`.
- The **Velopack** team for making installer + updater flow for desktop
  .NET apps genuinely easy.
- The **Vortice.Windows** team for high-quality, actively-maintained
  Direct3D/DXGI bindings.
- The **SharpHook** team for cross-platform global keyboard hooks that
  "just work" on .NET.
