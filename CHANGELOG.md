# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### M2 — Capture engine, encoders, hotkey, orchestrator

- Core models: `ImageFormat` (+ extension helpers), `CaptureRequest`, `CaptureResult`
  discriminated union (`Success` / `Failure`), `CapturedFrame` (raw BGRA with row stride),
  `HotkeyBinding` (modifiers + VK code).
- Core errors: stable `CaptureError` enum + `CaptureException` domain exception.
- Core abstractions: `ICaptureEngine`, `IImageEncoder`, `IHotkeyService`.
- `FileNameStrategy` — token expansion, invalid-char sanitization, collision counter.
  17 unit tests in `CaptureImage.Core.Tests`.
- `CaptureOrchestrator` — end-to-end coordinator: engine → encoder → atomic file write,
  converts exceptions to `CaptureResult.Failure` records, never throws.
- `CaptureItemInterop` — `IGraphicsCaptureItemInterop::CreateForWindow` bridge to turn a
  Win32 HWND into a WinRT `GraphicsCaptureItem`. Uses manual HSTRING management for
  `RoGetActivationFactory` and `MarshalInspectable` for the ABI → projection conversion.
- `Direct3D11Interop` — bidirectional bridge between Vortice D3D11 and the WinRT
  `IDirect3DDevice` / `IDirect3DSurface` types via `CreateDirect3D11DeviceFromDXGIDevice`
  and `IDirect3DDxgiInterfaceAccess`.
- `D3D11DeviceManager` — single shared D3D11 device with BGRA support and
  invalidate-on-device-lost.
- `WindowsGraphicsCaptureEngine` — on-demand single-frame WGC pipeline: create item,
  start session (no border, no cursor), wait for FrameArrived, copy to staging, map and
  read BGRA rows into a heap buffer, dispose everything. 2s frame timeout.
- `PrintWindowFallback` — GDI-based fallback via `PrintWindow(PW_RENDERFULLCONTENT)`,
  with an all-black-frame guard that surfaces `CaptureError.FallbackProducedBlackFrame`.
- `SkiaImageEncoder` — PNG, JPEG, WebP via SkiaSharp. Wraps the captured BGRA buffer as
  an `SKBitmap` without copying when row-packed.
- `ImageSharpTiffEncoder` — TIFF-only (LZW compression, 32-bit) via ImageSharp.
- `SharpHookHotkeyService` — global hotkey listener via `TaskPoolGlobalHook`, matches on
  native VK code + modifier mask, fires `Triggered` event off the UI thread.
- `DashboardViewModel` gains `ArmCommand`, `DisarmCommand`, `IsArmed`, `IsCapturing`,
  `StatusMessage`, `LastCapturePath`. Hotkey trigger marshals through the UI dispatcher
  and runs the orchestrator with PNG output into `%USERPROFILE%\Pictures\CaptureImage\`.
- `DashboardView.axaml` gets Arm/Disarm buttons, a state dot (gray/green/orange), and a
  live status line.
- `CompositionRoot` wires `D3D11DeviceManager`, `ICaptureEngine`,
  `PrintWindowFallback`, two `IImageEncoder` impls, `FileNameStrategy`,
  `CaptureOrchestrator`, `IHotkeyService`.
- 2 integration tests in `CaptureImage.Infrastructure.Tests/Capture/`:
  `Orchestrator_CapturesNotepadToPng_SuccessAndNonEmptyFile` (full WGC → Skia → disk round
  trip against a live Notepad), and `Engine_InvalidHwnd_ThrowsCaptureException` (HWND 0
  produces `TargetGone`). Tests are tagged `Category=Windows`.
- New packages: `SkiaSharp` 3.119.2, `SkiaSharp.NativeAssets.Win32`,
  `Microsoft.Win32.Registry` 5.0.0, `System.Drawing.Common` 9.0.0. Infrastructure project
  enables `AllowUnsafeBlocks` for the `LibraryImport` source generator.

### M1 — Process detection & Steam attribution

- `GameTarget`, `SteamAppInfo`, `SteamLibrary`, `ProcessChange` domain models in `CaptureImage.Core`.
- Core abstractions: `IProcessDetector`, `IProcessWatcher`, `ISteamDetector`,
  `ISteamRootLocator`, `IUIThreadDispatcher`.
- `VdfParser` — recursive-descent parser for Valve KeyValues files (.vdf / .acf)
  with escape sequences, line comments, and conditional blocks tolerated.
- `SteamLibraryScanner` — reads `libraryfolders.vdf` + every `appmanifest_*.acf`
  under the discovered libraries, maps executable paths to `SteamAppInfo`.
  Tested against real libraryfolders.vdf + appmanifest snippets.
- `RegistrySteamRootLocator` — finds Steam install dir via `HKLM\Software\Valve\Steam`
  with Program Files fallback.
- `WindowEnumerator` — `EnumWindows` P/Invoke with tool-window, owned-window and
  self-process filtering; uses `LibraryImport` source-gen.
- `IconExtractor` — extracts the associated icon for an executable and encodes as
  PNG bytes, cached by path.
- `GameDetector` (`IProcessDetector`) — composes WindowEnumerator + IconExtractor
  + ISteamDetector into a single `EnumerateTargetsAsync()` call.
- `WmiProcessWatcher` (`IProcessWatcher`) — WMI `__InstanceCreationEvent` /
  `__InstanceDeletionEvent` subscription over `Win32_Process`, 2s poll window.
- `AvaloniaUIDispatcher` — `IUIThreadDispatcher` implementation over
  `Dispatcher.UIThread`.
- `BytesToBitmapConverter` — converts PNG `byte[]` → Avalonia `Bitmap` for the
  Dashboard icon column.
- `GameTargetViewModel` + live `DashboardViewModel` — `ObservableCollection<GameTargetViewModel>`,
  refresh command, 500ms debounce on WMI events, IDisposable for clean teardown.
- `DashboardView.axaml` — real process list with icon, title, process name, and
  orange **STEAM** warning badge with tooltip.
- 23 unit tests in `CaptureImage.Infrastructure.Tests` covering VdfParser edge
  cases and SteamLibraryScanner path matching with `MockFileSystem`.

### M0 — Bootstrap

- Solution + 8-project skeleton (Core, Infrastructure, ViewModels, UI, App, and 3 test projects).
- Central Package Management via `Directory.Packages.props`.
- Shared MSBuild props via `Directory.Build.props` (Nullable, LangVersion, versioning).
- `.NET 9` SDK pin via `global.json`.
- Avalonia 11.2 shell with fixed-size main window (960x600, non-resizable).
- Nav rail with four tabs: Dashboard, Update, Settings, About.
- `INavigationService` + `NavigationService` wiring view-model navigation through DI.
- `MainWindowViewModel` + four placeholder tab view models.
- Serilog configured with console + rolling file sink at
  `%LocalAppData%\CaptureImage\logs\captureimg-{Date}.log`.
- GPL-3.0 license text and NOTICE file with third-party attributions.
