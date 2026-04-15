# CaptureImage

A lightweight Windows screen-capture tool for games that don't ship a built-in
screenshot feature. Tray-resident, hotkey driven, fast, and gentle on resources.

> **Status:** early development. Currently at milestone **M0 — Bootstrap**:
> solution scaffold, Avalonia shell, DI composition root, and file logging are
> wired up. Capture engine, process list, hotkeys, settings, and auto-update
> are landing in M1–M5.

## Requirements

- **Windows 11 22H2 or later** (build 10.0.22621+). Required by the
  Windows.Graphics.Capture API the capture engine uses.
- .NET 9 SDK for building from source (`global.json` pins the version).

## Repository layout

```
src/
  CaptureImage.Core            Pure domain, portable, testable.
  CaptureImage.Infrastructure  Windows implementations (capture, processes, steam, hotkeys).
  CaptureImage.ViewModels      Platform-agnostic MVVM.
  CaptureImage.UI              Avalonia views, styles, controls, resx.
  CaptureImage.App             Entry point, DI composition root, logging setup.
tests/
  CaptureImage.Core.Tests
  CaptureImage.Infrastructure.Tests   (Windows-only)
  CaptureImage.ViewModels.Tests
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) (coming soon) for the full
design rationale and the screen-capture pipeline writeup.

## Build & run (M0)

```bash
dotnet restore
dotnet build
dotnet run --project src/CaptureImage.App
```

The app opens a 960x600 window showing four empty tabs. Logs are written to
`%LocalAppData%\CaptureImage\logs\captureimg-{Date}.log`.

## License

CaptureImage is distributed under the **GNU General Public License v3.0 or
later** (SPDX: `GPL-3.0-or-later`). See [`LICENSE`](LICENSE) for the full text
and [`NOTICE`](NOTICE) for third-party attributions.
