# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### M4 — Auto-update, About tab, legal docs

Velopack-powered self-update, a full About tab, and the first cut of the
shipped legal documents.

- **Velopack integration**: `IUpdateService` in Core plus
  `VelopackUpdateService` in Infrastructure that wraps `UpdateManager` and
  `GithubSource`. `VelopackApp.Build().Run()` is now the first line of
  `Program.Main` so installer hooks (`--squirrel-install`,
  `--squirrel-firstrun`, `--squirrel-uninstall`) run before Avalonia boots.
  When the binary is not a Velopack-installed app (e.g. `dotnet run` from
  source), the service gracefully reports `UpdateStatus.Unavailable`
  instead of crashing.
- **Update tab**: `UpdateViewModel` owns the check / download / install
  workflow with a bounded rolling log (200 lines), progress bar, and
  localized state labels. `UpdateView.axaml` lays out Check / Download /
  Install buttons with correct enable/disable wiring based on `UpdateStatus`.
- **About tab**: full rewrite of `AboutViewModel` + `AboutView` with:
  app metadata (name, version, license, developer, repository link), the
  AI-assistance disclosure, translation / capture-support / liability
  disclaimers, a third-party component list, and buttons that open every
  shipped legal document via `Process.Start` + default shell handler.
- **Legal documents** (new, shipped next to the binary via
  `CopyToOutputDirectory`):
  - `docs/legal/DISCLAIMER.md` — no warranty, capture limitations,
    AI-assisted development, machine-assisted translations, trademark
    notice, "not legal advice" caveat.
  - `docs/legal/PRIVACY.md` — local-only storage, no telemetry, update
    check is the only network call, optional cloud upload is out-of-scope
    for this release, plaintext log contents.
  - `docs/legal/TERMS.md` — plain-language restatement of GPL-3.0 free
    software obligations, user agreement, trademark notice, no support
    obligation, contribution acceptance terms.
  - `docs/legal/THIRD_PARTY_NOTICES.md` — expanded version of the root
    `NOTICE` file with upstream URLs, license identifiers, and a note on
    libuiohook's GPL-3.0 compatibility.
  - `docs/CONTRIBUTORS.md` — maintainer info, AI assistance disclosure,
    translation help wanted, open-source acknowledgements.
- **Translation disclaimer**: explicit note in About tab + DISCLAIMER that
  Vietnamese and Arabic strings are machine-assisted, and an invitation
  for native speakers to contribute corrections.
- **Capture limitation disclaimer**: explicit list of known-bad cases
  (DRM, D3D9 exclusive fullscreen, anti-cheat protected titles, HDCP,
  minimized windows) both in-app and in DISCLAIMER.md §2.
- **Liability disclaimer**: plain-language "AS IS" notice in About tab
  pointing to the full GPL-3.0 text.
- **Localization**: 20+ new resx keys (`About_*`, `Update_*`) shipped in
  English, Vietnamese, and Arabic satellite assemblies. Legal link button
  labels are left in English intentionally so they match the file names
  when opened.
- **CompositionRoot**: `IUpdateService` registered as singleton.

### M3 — UX polish: state machine, localization, preview, toast, tray, settings, log viewer

Major shell rewire landing everything the spec calls "polish".

- **Settings persistence**: `AppSettings` record hierarchy
  (`CaptureSettings`, `UiSettings`) in Core plus `ISettingsStore` abstraction.
  `JsonSettingsStore` writes to `%LocalAppData%\CaptureImage\settings.json` via
  `System.Text.Json` source generators (`SettingsJsonContext`), with atomic
  temp+rename writes, a 300ms debounce window, and import/export helpers.
  12 unit tests cover round-trip, version guard, corrupt input, and import/export.
- **Localization**: `ILocalizationService` in Core with indexer + INPC.
  `ResxLocalizationService` in UI backed by `Strings.resx`, `Strings.vi.resx`,
  `Strings.ar.resx` (≈40 keys each). `FlowDirectionConverter` maps the portable
  `TextFlowDirection` enum to Avalonia's own `FlowDirection`; MainWindow binds
  via `FlowDirection="{Binding Localization.CurrentFlowDirection, Converter=...}"`
  so `ar-SA` auto-mirrors the whole layout.
- **Culture-aware nav rail**: `NavItemViewModel` subscribes to the localization
  service's `PropertyChanged("Item[]")` and re-raises `Label`, so every nav rail
  entry refreshes in place when the user switches language.
- **Capture state machine**: `CaptureStateMachine` in Core (Stateless-backed)
  implementing the plan §5 states — Idle → TargetsSelected → Armed → Capturing →
  (Previewing?) → Saving → Complete → Armed (with Failed as the parallel error
  path). 14 unit tests cover happy path, preview gate, error paths, and illegal
  transitions.
- **Toast system**: `IToastService` in Core with `ToastItem` record.
  `ToastService` + `ToastHost` custom Avalonia UserControl render an
  `ObservableCollection<ToastItem>` as a bottom-right overlay (bottom-left under
  RTL). `ToastKindToBrushConverter` colors each toast by severity.
- **Preview flow**: `IPreviewPresenter` in Core + `AvaloniaPreviewPresenter` in
  UI that opens a modal `PreviewWindow` showing the captured frame (encoded once
  as PNG via SkiaSharp) and awaits a Save/Discard decision. Wired through the
  state machine so `PreviewRejected` routes back to Armed with the frame
  discarded.
- **Tray icon**: `ITrayIconHost` in Core + `AvaloniaTrayIconHost` in UI using
  Avalonia's built-in `TrayIcon` and `NativeMenu`. Runtime-generated 32×32 icon
  via SkiaSharp so we don't ship a binary `.ico` yet. Menu items: Show / Open
  captures folder / Exit. MainWindow `Closing` event is intercepted when
  `UiSettings.MinimizeToTray` is true so the close button hides instead of quits.
- **Real-time log viewer**: `LogEntry` model + `LogLevel` enum in Core,
  `ILogBufferSource` abstraction. `InMemorySink` is a Serilog sink that keeps a
  2000-entry ring buffer and implements `ILogBufferSource`. `LogViewerViewModel`
  hydrates from a snapshot on first show and appends live entries; bounded at
  500 visible rows. `LogViewerView` is a right-side drawer overlay toggled from
  the nav rail footer.
- **Dashboard rewire**: `DashboardViewModel` now drives the capture flow through
  the state machine, reads hotkey/format/output settings from `ISettingsStore`,
  gates saves behind the preview presenter when `PreviewBeforeSave` is on,
  surfaces toasts on success/failure, and auto-resets from Complete/Failed to
  Armed after 800ms so the next shot is one hotkey press away.
- **Settings UI**: `SettingsView` with a scrollable form — language dropdown
  (auto-persists + live-switches culture), hotkey display, default format
  picker, JPEG/WebP quality sliders, output folder, file name template, toggle
  switches for preview/minimize-to-tray/sound, and Import / Export / Open
  buttons wired to Avalonia's `IStorageProvider`.
- **CompositionRoot**: all M3 services registered. `Program.Main` loads settings
  before building the Avalonia lifetime, applies the persisted culture so the
  first frame renders correctly, and flushes settings on shutdown.
- **Integration test**: M2's Notepad integration test switched to `winver.exe`
  because `notepad.exe` on Win11 is a Store launcher shim whose `Process.Start`
  return doesn't expose a real main window.

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
