# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-04-20

Second release. Polish + configurability pass on top of v1.0.0 — ships a
real hotkey rebinder, live localization fixes across every tab, an
architecture overview for contributors, and a visual refresh aligned
with 2025/26 Windows 11 conventions. No capture-engine changes; no new
backends.

### M4 — UI/UX polish: live-localization fixes + 2025 modernization

Polish pass closing seven localization / live-refresh bugs and modernizing
the visual system to match 2025/26 Windows 11 conventions. Commit
[`5f5e878`](https://github.com/poli0981/captureimg/commit/5f5e878).

- **Dashboard localization**: title, subtitle, Refresh / Arm / Disarm
  buttons, "N target(s) visible" line, and the loading hint were all
  hardcoded English; now bound through `{Binding Localization[Dashboard_*]}`
  and refresh live on culture switch. `DashboardViewModel` grows
  `TargetsCountText`, `LoadingText`, `EmptyStateText`, `HasNoTargets` and
  expands its culture listener to refresh all of them.
- **Steam badge tooltip**: `GameTargetViewModel.SteamBadgeTooltip` was a
  hardcoded mixed EN/VI string; now formats via
  `Dashboard_SteamBadgeTooltip` with a culture listener. The VM is now
  `IDisposable` and the dashboard disposes rows on reconcile so the
  listener doesn't pin dead targets.
- **LogViewer Pause/Resume**: the button was pinned to `Log_Pause` and
  didn't flip on state change. New `TogglePauseLabel` switches between
  `Log_Pause` / `Log_Resume` on `IsPaused` + culture change. A new
  culture listener on `LogViewerViewModel` also refreshes
  `EventsCountText` + `EmptyStateText`.
- **Hotkey error message**: `HotkeyBindingViewModel.ErrorMessage` is an
  indexer-backed computed property; added a culture listener that raises
  `PropertyChanged` so the Settings error text re-localizes live.
- **MainWindow header**: hardcoded `"CaptureImage"` + stale
  `"v0.1.0 — M3 UX polish"` replaced with `AppTitle` + `AppVersion`
  pulled from `AssemblyInformationalVersion` / assembly metadata.
  `App.csproj` now carries a `<Version>` element; release.yml keeps
  overriding via `/p:Version=<git-tag>`.
- **Inter font stack**: `Avalonia.Fonts.Inter` was in the package graph
  but never applied. `App.axaml` now declares a global Inter style for
  TextBlock / TextBox / Button / ComboBox / CheckBox (monospace
  overrides remain for LogViewer entries + HotkeyRecorder display).
- **Focus ring**: keyboard-focus `:focus-visible` style on
  `ListBoxItem` + `Border#BindingField:focus` style on
  `HotkeyRecorder` — both paint the border in the accent brush so Tab
  navigation and "waiting for your key" are visually obvious.
- **Corner radius**: unified to 8 px on cards / borders across Dashboard,
  About, Update, Preview, LogViewer, Toast. 4 px kept only on the inline
  Steam badge pill per the design contract.
- **Text contrast**: every `Opacity="0.4"`–`"0.7"` on labels and hints
  swapped for `Foreground=SystemControlForegroundBaseMediumBrush` so
  secondary text meets WCAG AA on both light and dark theme variants.
- **Accessibility**: `AutomationProperties.Name` + HelpText on the log
  drawer toggle, HotkeyRecorder BindingField, and Dashboard status
  card — screen readers now announce meaningful names.
- **Empty states**: Dashboard and LogViewer now render a localized hint
  when no targets / no log events. New keys `Dashboard_EmptyState`,
  `Dashboard_LogToggle`, `Log_EmptyState`, `Log_EventsCount` added in
  EN / VI / AR.
- **FontSize scale**: snapped outliers to 11/12/13/14/16/22 — page
  titles 24→22, preview dialog 20→22, nav icon 18→16, Steam badge
  10→11.

### M3 — Architecture tour + dogfood screenshots

Contributor-facing documentation + real README screenshots. Commit
[`8e374a0`](https://github.com/poli0981/captureimg/commit/8e374a0).

- **`docs/ARCHITECTURE.md`** (new) — one-page-per-layer reference with
  five Mermaid diagrams that GitHub renders inline: project dependency
  graph, capture state machine, WGC capture pipeline sequence, DI
  composition sketch, i18n + RTL flow, and Velopack update flow.
- **`docs/screenshots/`** (new) — six real app screenshots
  (Dashboard / Settings / About / Update / LogViewer / RTL Dashboard),
  captured by dogfooding CaptureImage on itself for the passive views.
- **README refresh** — replaces the "coming with first tagged release"
  placeholder with a 2×3 screenshot table, and adds an Architecture
  section linking to the new doc.

### M2 — Dependabot cleanup + Node 24 runners

Close the dependabot backlog ahead of the Node 20 deprecation deadline
(2026-09-16). Commit [`d5706f2`](https://github.com/poli0981/captureimg/commit/d5706f2).

- **GitHub Actions bumps** (all Node 20 → Node 24 runtime updates; no
  API changes, `windows-latest` already supports the newer runner):
  `actions/checkout` 4→6, `actions/setup-dotnet` 4→5,
  `actions/upload-artifact` 4→7, `softprops/action-gh-release` 2→3.
- **Test tooling bumps**: `xunit` 2.9.2→2.9.3 (patch),
  `coverlet.collector` 6.0.2→8.0.1 (major — requires .NET 8 LTS floor,
  which we already exceed on .NET 9).
- **Release.yml audit**: `upload-artifact` v5/v6/v7 kept `name`,
  `path`, `retention-days` intact; the v1.1 plan's speculated
  `overwrite` default flip never happened. No YAML option changes
  required.

### M1 — Hotkey rebinder + conflict sniff

First user-facing feature addition since 1.0.0. Commit
[`0482f71`](https://github.com/poli0981/captureimg/commit/0482f71).

- **`HotkeyRecorder` control** — keyboard-driven recorder in Settings.
  Click Record, press a combination, Esc cancels. The view maps
  Avalonia `Key` to Win32 virtual-key codes so the persisted
  `HotkeyBinding` stays round-trippable against SharpHook's RawCode.
- **`HotkeyBindingViewModel`** — bridges recorder → persistence and
  calls `IHotkeyService.SetBinding` live so an armed capture picks up
  the new combo without disarm/re-arm.
- **`ReservedHotkeys`** (new, `CaptureImage.Core.Validation`) —
  catalogue of combinations owned by the Windows shell (Win+L,
  Ctrl+Shift+Esc, Alt+Tab, Alt+F4, Win snap keys, etc.).
  `HotkeyBinding.Validate()` rejects those plus modifier-only /
  naked-letter bindings before persistence with a localized error.
- **`HotkeyConflictSniffer`** — `RegisterHotKey`/`UnregisterHotKey`
  probe on `HWND=NULL`. If another process already owns the combo the
  UI surfaces a non-blocking warning, but the user keeps control and
  still saves.
- **i18n strings** for every recorder surface added in EN / VI / AR.
- **Tests**: +24 Core validation (reserved combos + `HotkeyBinding.Validate`),
  +12 ViewModel flow with NSubstitute fakes. Total 107 pass.

## [1.0.0] - 2026-04-16

Initial public release. Screen-capture tool targeting Windows 11 22H2+,
built with C# .NET 9 + Avalonia 11, distributed as a Velopack installer
via GitHub Releases. Unsigned (SignPath application was declined).

### M5 — Release infrastructure (1.0.0 cut)

Everything you need to actually ship the binary. No new product features;
this milestone is the build system, CI/CD, and the docs you read before
clicking Run.

- **GitHub Actions CI** (`.github/workflows/ci.yml`): builds + tests the
  solution on `windows-latest` on every PR and push to `main`. Treats
  nullable warnings as errors; uploads `.trx` test results as artifacts.
- **Release workflow** (`.github/workflows/release.yml`): tag-triggered
  (`v*` pattern). Runs the full test suite, `dotnet publish` single-file
  self-contained for `win-x64`, `vpk pack` via the Velopack CLI,
  computes `SHA256SUMS.txt`, uploads everything to a GitHub Release
  with auto-generated release notes. Also supports `workflow_dispatch`
  with a manual version input for emergency reruns.
- **Issue + PR templates**: `bug_report.yml` (with repro, environment,
  capture-engine dropdown, preflight checks), `feature_request.yml`
  (use case first, scope picker), `config.yml` routing security reports
  to Security Advisories. PR template covers testing, AI assistance
  disclosure, and contribution checklist.
- **Dependabot**: weekly NuGet + GitHub Actions bumps, with Avalonia,
  Microsoft.Extensions, Serilog, and test-tooling grouped to avoid PR
  spam.
- **SECURITY.md**: private disclosure process via GitHub Security
  Advisories, supported versions, scope (RCE, privilege escalation,
  target-selection bypass, Velopack supply chain), out-of-scope (game
  incompatibility, unsigned-installer SmartScreen, etc.), supply-chain
  dependency note.
- **`docs/RELEASING.md`**: step-by-step tag flow, failure recovery,
  post-release verification checklist, detailed unsigned-installer
  section (SignPath was declined — documented how to verify via SHA256
  and what to do when signing becomes available later), manual
  fallback steps, version policy.
- **README.md**: full rewrite with CI / release / license / .NET 9
  badges, feature list, screenshots placeholder, system requirements,
  install instructions with SmartScreen warning, usage walkthrough,
  build-from-source steps, repository layout, contributing pointer,
  security pointer, AI assistance disclosure.
- **`docs/legal/DISCLAIMER.md §7`**: new section documenting the
  unsigned installer state, how to verify via SHA256, and the SignPath
  application rejection context.
- **`.gitattributes`**: normalize line endings, declare text vs binary
  for every file type we ship, silence the "CRLF will be replaced"
  warning on commit.
- **`Directory.Build.props`**: version bumped to `1.0.0` (from `0.1.0`).
  Added `Authors`, `RepositoryUrl`, `RepositoryType`, `PackageProjectUrl`,
  `PackageLicenseExpression` so the assembly manifest and the About tab
  can report accurate metadata. Copyright updated to reference
  GPL-3.0-or-later explicitly.

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
