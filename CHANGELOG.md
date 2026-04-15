# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
