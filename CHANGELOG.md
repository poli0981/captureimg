# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2026-04-27

UX polish + accessibility cycle. No breaking changes; settings.json
schema unchanged.

### Added

- **Light / Dark / System theme** picker on the Settings tab. The
  preference was already reserved in `AppSettings.Theme` since v1.3
  but had no consumer; v1.4 wires it through to
  `FrameworkElement.RequestedTheme` and re-applies on every
  `ISettingsStore.Changed`. Mica retints automatically with the
  resolved theme.
- **Auto-switch on Alt-Tab** (opt-in, default off). When enabled,
  the dashboard's selected target follows the OS foreground window
  via a `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` watcher with a
  250 ms debounce. Apps not in the target list keep your current
  selection. Toggle lives next to the existing capture toggles.
- **Four new language packs**: Japanese (ja-JP), Simplified Chinese
  (zh-Hans), Korean (ko-KR), Spanish (es-ES). All machine-assisted —
  the About → Translations disclaimer lists every machine-assisted
  pack and invites native-speaker proofreading PRs.
- **Friendlier log lines** for end-user lifecycle events: `Picture
  captured: {Name} ({W}×{H}, {KB} KB) at {Path} [{Format}]`,
  `Picture capture cancelled by user.`, `Language switched to
  {Code} ({Name}).`, `Theme switched to {Theme}.`, `Capture sound
  {State}.`, `Auto-switch on Alt-Tab {State}.` Logs remain
  English-only by design (file shareability + parser stability
  across cultures).

### Changed

- **Window is now fixed-size** at 1200×720. Both maximize and
  resize are disabled via `OverlappedPresenter.IsMaximizable` /
  `IsResizable`; minimize remains enabled for the tray flow.
- **Tone shift on existing log call sites**: "Failed to X" →
  "Couldn't X" across `DashboardViewModel`, `SettingsViewModel`,
  `UpdateViewModel`, `AboutViewModel`, `LogViewerViewModel`.
  Structured properties are unchanged so log queries and external
  diagnostics still work.

## [1.3.1] - 2026-04-26

**Critical hotfix.** v1.3.0 shipped broken — the WinUI 3 publish pipeline
omitted `.xbf` (compiled XAML binary) and `CaptureImage.App.pri` (resource
manifest) files from the self-contained publish output, so the installed
app silently failed at MainWindow.InitializeComponent on every launch
(the process stayed alive zombie-style; no window ever appeared). v1.2.x
auto-update users hit this on the first launch after Velopack restart.

### Fixed

- Add a `CopyWinUIArtifactsToPublishDirectory` MSBuild target to
  `CaptureImage.App.csproj` that propagates every `.xbf` and the
  `CaptureImage.App.pri` from `bin/.../win-x64/` into the publish
  output, preserving the cross-assembly XAML resolution subfolders
  (`CaptureImage.UI/Controls/`, `CaptureImage.UI/Views/`). `dotnet
  publish` for unpackaged WinUI 3 self-contained apps doesn't include
  these by default — known WinAppSDK gap.

### Recovery for v1.3.0 users

If you already updated to broken v1.3.0:
- The Update tab can't help (the app's window doesn't open).
- Download `CaptureImage-win-Setup.exe` from the v1.3.1 GitHub Release
  and run it — the installer detects the existing install and replaces
  it cleanly. Your `settings.json` and capture folder are untouched.

## [1.3.0] - 2026-04-26

UI framework migration from Avalonia 11 to **WinUI 3 (Windows App SDK 1.8.x)**
with Fluent 2 design — Mica backdrop, Segoe UI Variable, integrated title bar,
Composition-API animations. Logic layers (Core / Infrastructure / ViewModels)
were intentionally left untouched: the entire ~5,000-line domain + capture +
hotkey + settings + logging stack carries forward unchanged. Velopack ships
on the existing `win` channel so v1.2.x users auto-update; multi-file
WindowsAppSDK self-contained payload is ~80–120 MB (vs the v1.2 single-file
~30 MB) — accepted trade for stack-Microsoft-native parity on Windows-only.

### Added

- **Sound on capture success** (`UI.SoundEnabled` setting now has a consumer
  for the first time since v1.0). Plays Windows `SystemAsterisk` via Win32
  `PlaySound` P/Invoke, gated on the existing checkbox.
- **Browse button** on Settings → Output folder. Opens the modern
  `IFileOpenDialog` (Vista+ folder picker) seeded at the current
  OutputDirectory. Win32 COM-interop fallback because
  `Windows.Storage.Pickers.FolderPicker` is unreliable in unpackaged
  WinUI 3.
- **Mica backdrop** on the main window — translucent Win11 wallpaper tint.
- **Borderless title bar** integrated with the Fluent 2 chrome; caption
  buttons sit in a 48 px reserved drag region.
- **Composition slide animation** on the log drawer — 220 ms slide + 180 ms
  fade with CubicBezier(0.4, 0, 0.2, 1) decelerate. Mirrors the v1.2
  Avalonia drawer at the visual layer; uses `UIElement.Translation` (additive
  on layout) instead of `Visual.Offset` (overrides layout) so the drawer
  stays anchored to its Grid column under all window sizes.
- **+164 tests, total 320** (up from 156). New coverage:
  - 18 converter tests (FlowDirection / BoolToVisibility / InvertedBoolToVisibility / NotNullToVisibility).
  - 5 LogEntry tests (TimestampText format, FileLineText edge cases).
  - 141 Theory cases for localization completeness — 47 load-bearing keys
    × 3 cultures (en-US / vi-VN / ar-SA) — every shipping label asserted
    non-empty + non-bracketed.

### Changed

- **UI framework**: Avalonia 11.2.3 → WinUI 3 (Windows App SDK
  1.8.260416003) self-contained x64. ViewModels project still
  TFM-portable (`net9.0`); UI + UI.Tests TFM bumped to
  `net9.0-windows10.0.22621.0` to link WinAppSDK assemblies.
- **Tray icon**: `H.NotifyIcon.WindowsAppSDK` → `H.NotifyIcon.WinUI` (the
  actual nuget.org package id; the WindowsAppSDK suffix never existed on
  nuget). Tray ships with OS default glyph for v1.3.0 — proper .ico
  round-trip via `System.Drawing.Bitmap.GetHicon` + `Icon.Save` deferred
  to v1.4.
- **Folder picker**: Win32 `IFileOpenDialog` COM interop replaces the
  unreliable `Windows.Storage.Pickers.FolderPicker` in
  `CaptureImage.UI.Services.Win32FolderPicker`.
- **Tray menu commands** wired through `MenuFlyoutItem.Command` (not
  `Click` event) — the latter doesn't fire reliably from H.NotifyIcon's
  out-of-tree popup.
- **Sound playback** uses Win32 `PlaySound` P/Invoke instead of
  `System.Media.SystemSounds` (the latter ships in
  `System.Windows.Extensions` which only targets Windows TFMs, too
  restrictive for the portable ViewModels project).
- **Tray-restore from minimize** uses `AppWindow.Show` +
  `User32.ShowWindow(SW_RESTORE)` + `Window.Activate` +
  `User32.SetForegroundWindow` — `Show` alone leaves taskbar-minimized
  windows minimized + behind whatever has foreground focus.
- **Auto-refresh suppression** while a preview modal is up — Dashboard's
  WMI-driven Targets refresh used to clobber the in-flight
  `SelectedTarget` and disrupt the preview decision; now skipped when
  state is Previewing or Saving.
- **3rd-party list** in About: drop Avalonia + Inter typeface, add
  Windows App SDK / WinUI 3 + H.NotifyIcon.WinUI.

### Removed

- `Avalonia.*` packages (Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent,
  Avalonia.Fonts.Inter, Avalonia.Diagnostics) and 25 .axaml files / Avalonia
  service implementations (`AvaloniaTrayIconHost`, `AvaloniaPreviewPresenter`,
  `AvaloniaUIDispatcher`, `BytesToBitmapConverter` for Avalonia.Bitmap,
  `ToastKindToBrushConverter` for Avalonia.IBrush). All replaced with WinUI 3
  equivalents.
- Inter typeface — Fluent 2's Segoe UI Variable is the explicit body font now.
- `PublishSingleFile` from the release workflow — WinAppSDK can't be
  bundled into a single .exe, ships as multi-file folder packaged by
  Velopack.

### Fixed

- (in v1.3 from v1.2 carry-overs) Title-bar caption buttons no longer
  overlap nav rail / Page content — 48 px drag region reserved at top
  via `Window.SetTitleBar(AppTitleBar)`.

### Deferred to v1.4

- Real .ico tray icon via `System.Drawing.Bitmap.GetHicon` round-trip
  (currently OS default glyph). H.NotifyIcon.WinUI's IconSource pipeline
  ends in `System.Drawing.Icon` which only accepts .ico file streams.
- `release.yml` upload of `CaptureImage-win-Portable.zip` (carry-over
  from v1.2 — added in v1.3 release cut).
- DPI-aware initial window sizing (`AppWindow.Resize` takes physical
  pixels; v1.3 hardcodes 1200×720).

## [1.2.0] - 2026-04-21

Logging overhaul. Turns a working-but-thin diagnostic surface into one you
can actually rely on when things go wrong — developer and end-user alike.
No capture-engine changes; Velopack release on the existing `win` channel
so the delta chain from v1.1.2 stays intact.

### Added

- **Runtime log-level control.** New Settings → Logging group with a
  minimum-level picker (Debug / Information / Warning / Error).
  Persisted in `AppSettings.LogLevel` (new field, schema bump v1 → v2,
  additive) and pushed into Serilog's `LoggingLevelSwitch` live — no
  restart required.
- **Log viewer min-level filter.** Dropdown in the log drawer header
  narrows what's shown without touching the sink buffer, so Export
  still carries the full history regardless of the filter.
- **"Open folder" button in the log drawer.** Launches Explorer at
  `%LocalAppData%\CaptureImage\logs` for support-session copy-paste.
  Guarded with a Toast on failure.
- **Caller context on every log entry** — each line carries `File`,
  `Line`, and `Member` structured properties, captured via new
  `LogInformationAt` / `LogWarningAt` / `LogErrorAt` / `LogDebugAt`
  extension methods on `ILogger`. Rolling-file template now prints
  `(File.cs:42)`; log viewer shows a subdued `File:Line` column; Export
  includes the same. Absolute build-machine paths are stripped via
  `Path.GetFileName` so shipped logs stay clean.
- **Global exception handlers.** `AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`, and
  `Dispatcher.UIThread.UnhandledException` all now reach Serilog before
  the process dies or the UI resumes. `SetObserved()` on unobserved
  task exceptions keeps the default-crash policy from ending the app on
  background-thread throws.
- **Startup + shutdown banner** — Program.Main logs
  `Version` / `OS` / `Culture` / `Args` on start and a
  `Reason=Normal|StartupCrash` line on exit, so support sessions can
  tell which build produced the log at a glance.
- **Expanded coverage** — `Windows.Graphics.Capture` engine emits Info
  on session start, Debug on first frame + session dispose, Warning on
  frame timeout, Error with explicit `HRESULT=0x…` on `COMException`;
  `HotkeyConflictSniffer` decodes Win32 error codes by name
  (`ERROR_HOTKEY_ALREADY_REGISTERED` / `ERROR_INVALID_PARAMETER` / …);
  `SharpHookHotkeyService` logs when `TaskPoolGlobalHook` boots;
  `VelopackUpdateService` logs before + after
  `ApplyUpdatesAndRestart`; `AvaloniaTrayIconHost` logs tray
  init/dispose and guards the runtime SkiaSharp icon render so a
  native-asset blow-up falls back to the platform default instead of
  crashing startup; `ResxLocalizationService` warns once per
  `(culture, key)` pair when a resource key is missing (dedup
  prevents flooding on every refresh).

### Changed

- **Window widened to 1200×720** (was 960×600) and **log drawer
  widened to 480 px** (was 400 px). The drawer now slides in with a
  200 ms fade + 48 px translate-X easing (`CircularEaseOut`) instead
  of snapping in and out. Content area visible while the drawer is
  open is now ~500 px (was ~340 px).
- **Scroll bars hidden on About + Settings tabs.** Mouse-wheel
  scrolling still works; the track is just no longer drawn, so the
  view reads cleanly on the fixed-size window. Horizontal scrolling
  on those tabs is disabled entirely. Diagnostic surfaces (Update
  log, Log viewer drawer) keep their visible bars.
- **Default minimum log level is now `Information`** (was `Debug`).
  v1 users are migrated in place: `settings.json` is stamped
  Version=2 on first v1.2 launch and the LogLevel field defaults to
  `Information`. Flip back to Debug via Settings → Logging if you
  were relying on verbose output.
- Four previously-silent `catch { /* ignore */ }` blocks now log
  their exceptions: `SharpHookHotkeyService.Stop/Dispose` (Warning),
  `WmiProcessWatcher.DisposeWatchers_NoLock` (Debug),
  `JsonSettingsStore.Deserialize` (Warning with JSON line / byte
  position), `JsonSettingsStore.TryDelete` (Debug).

### Migration

- **`settings.json` schema v1 → v2.** Automatic on first v1.2 run —
  the file is re-written in canonical v2 form with `LogLevel =
  "Information"` and existing values preserved. Hand-edited files
  with `logLevel` already present keep their value.

### Tests

- 33 new unit tests (total 156, up from 123). Covers the
  `InMemorySink` ring buffer / pause / clear / snapshot-copy
  semantics, `SerilogLogLevelSwitcher` string mapping + fallback,
  `CallerAwareLoggerExtensions` scope + path-stripping behaviour,
  `LogViewerViewModel` hydration / filter / live-tail paths, and the
  v1 → v2 settings migration with write-back.

## [1.1.2] - 2026-04-20

Localization hotfix on top of v1.1.1 — closes 13 bugs and gaps found in
a post-v1.1.1 audit plus three bugs reported during smoke testing of
the hotfix itself. No new features; no capture-engine changes. Velopack
release on the same `win` channel as v1.1.1 to keep the delta chain
intact.

### Fixed

- **Dashboard status line stale on culture switch.** `StatusMessage`
  is an `[ObservableProperty]` stored backing field, so the v1.1.1
  `OnPropertyChanged(nameof(Localization))` signal re-read the same
  stored string. Redrive via `UpdateStatusForState()` on culture
  change so the new culture's template wins.
- **Preview window hardcoded English.** `PreviewWindow.axaml` had
  `Title="Preview"` and no `FlowDirection` binding, so the modal stayed
  in English and rendered LTR under Arabic. Title now binds to
  `Localization[Preview_Title]`; `FlowDirection` binds to
  `Localization.CurrentFlowDirection`.
- **`PreviewViewModel` did not listen for culture changes.** If the
  user switched language while the preview modal was open, the four
  indexer bindings (`Preview_Title`, `Preview_Prompt`, `Preview_Save`,
  `Preview_Discard`) stayed in the old culture. VM now implements
  `IDisposable`, subscribes with a named `OnLocalizationChanged`, and
  `AvaloniaPreviewPresenter` `using`-disposes it after the modal so
  the subscription doesn't leak into the singleton service.
- **File picker titles hardcoded English.** `Import settings` /
  `Export settings` now come from new resx keys
  `Settings_ImportTitle` / `Settings_ExportTitle` (en + vi + ar).
- **Settings toast error title hardcoded English.**
  `_toasts.ShowError("Error", …)` in three places now reads through
  `Localization["Toast_Error"]` (new resx key).
- **About tab disclaimer body paragraphs hardcoded English.** The
  three disclaimer headings were already localized; the body
  paragraphs were not, leaving the tab half-translated for vi / ar
  users. Added `About_TranslationDisclaimerBody`,
  `About_CaptureLimitationDisclaimerBody`, and
  `About_LiabilityDisclaimerBody` in all three resx files (machine-
  assisted translation, matching the existing Translations
  disclaimer). `AiAssistanceNotice` intentionally stays English — it
  describes the development process, not the user-facing product.
- **Tray menu rebuilt 3× per culture switch.**
  `AvaloniaTrayIconHost`'s `PropertyChanged` handler fired for every
  notification the service raises (`Item[]` + `CurrentCulture` +
  `CurrentFlowDirection`). Named handler now filters `"Item[]"` only,
  and the subscription is torn down in `Dispose`.
- **`ResxLocalizationService.SetCulture` set only
  `DefaultThreadCurrentUICulture`.** Date / number / currency
  formatting on the UI thread kept the OS locale. The service now
  also sets `CultureInfo.CurrentUICulture`, `CurrentCulture`, and
  `DefaultThreadCurrentCulture` so implicit `.ToString()` follows
  the chosen language.
- **Startup accepted any resolvable culture.** A hand-edited
  `settings.json` with `Culture=fr-FR` would flip the app to fr and
  fall through to neutral English resx while Settings still showed
  "English" selected. `Program.cs` now validates the persisted
  culture against `SupportedCultures` before calling `SetCulture`.
- **Log drawer toggle button did not retranslate live.** Same
  compiled-indexer-binding limitation as the next item — the v1.1.1
  `OnPropertyChanged(nameof(Localization))` signal does not wake
  Avalonia compiled indexer bindings through an intermediate
  property. Fix: expose `LogToggleLabel` and `LogToggleTooltip` as
  plain computed properties on `MainWindowViewModel` and bind
  MainWindow.axaml to those instead of the indexer path.
- **SettingsView did not retranslate live when the user switched
  language from the Settings tab itself.** The view only refreshed
  on re-navigation (DataTemplate re-instantiation). Added 18 plain
  computed properties on `SettingsViewModel` and 6 on
  `HotkeyBindingViewModel`, rewrote the bindings in
  `SettingsView.axaml` and `HotkeyRecorder.axaml` to target them.
- **`settings.json` was never written on first run.**
  `JsonSettingsStore.LoadAsync` created a fresh `AppSettings()` in
  memory when the file was missing but only persisted on the first
  user-triggered `Update()`. Partial / malformed / future-version
  files left the on-disk form out of sync with the in-memory form.
  `LoadAsync` now writes back when the file is missing, unparseable,
  version-too-new, or when the canonical re-serialized form differs
  from the raw JSON. Write failure is logged but non-fatal so a
  read-only disk does not break Load.

### Changed

- **Handler hygiene across localization subscribers.** Anonymous
  lambda subscriptions on `Localization.PropertyChanged` extracted
  to named methods so they can be unsubscribed
  (`DashboardViewModel`, `AboutViewModel`, `LogViewerViewModel`,
  `SettingsViewModel`, `AvaloniaTrayIconHost`). `LogViewerViewModel`
  and `SettingsViewModel` now implement `IDisposable` and
  unsubscribe both of their event sources; runtime behavior is
  unchanged because the DI container disposes singletons on
  shutdown.

### Removed

- Four unused resx keys (`Settings_Theme`, `Tray_Arm`, `Tray_Disarm`,
  `Log_Export`) in lockstep across en / vi / ar. None were wired to
  UI; a future v1.2+ can re-add them when a theme picker, tray
  arm/disarm, or log export button actually ships.

### Known limitations

- Toast title / message snapshot the localized string at the time
  `ToastService.Show` is called; switching culture while a toast is
  still fading out does not retranslate it. Fixing this requires
  threading resx keys through `ToastService` instead of strings —
  deferred to v1.2+ to keep this hotfix focused.
- `Dashboard` / `About` / `Update` / `LogViewer` tabs still use
  `{Binding Localization[Key]}` indexer bindings in their
  respective views. They refresh because navigation re-instantiates
  them via `DataTemplate`; if a future change pins the active tab
  through a culture switch, the same computed-property refactor may
  be needed.

### Tests

- New `tests/CaptureImage.UI.Tests/` project covering
  `ResxLocalizationService` (SetCulture thread-culture propagation,
  FlowDirection mapping for ar-SA, event counts, indexer fallback
  for missing keys, localized lookup for known keys, SupportedCultures
  surface).
- New `tests/CaptureImage.ViewModels.Tests/Preview/PreviewViewModelTests.cs`
  covering the v1.1.2 M1 fix: Localization PropertyChanged forwarding
  + Dispose detachment.

## [1.1.1] - 2026-04-20

Hotfix on top of v1.1.0 — closes three bugs reported after the v1.1.0
smoke install, and closes the CI gap that prevented in-app updates from
working.

### Fixed

- **In-app update from v1.0.0 → v1.1.0 failed** because `release.yml`
  packaged only a full `.nupkg` with the legacy `RELEASES` manifest —
  Velopack clients ≥ 0.0.900 poll for `releases.win.json` and prefer an
  incremental `*-delta.nupkg`. The workflow now runs
  `vpk download github --channel win` ahead of `vpk pack` so Velopack
  sees the previous release, emits a delta + the JSON manifest, and the
  GitHub Release upload pattern picks up `releases.*.json`.
- **Settings tab did not re-localize live when the language dropdown
  was used**. The `{Binding Localization[Settings_*]}` indexer bindings
  relied on the service's own `PropertyChanged("Item[]")` notification,
  which Avalonia's compiled indexer bindings through an intermediate
  property don't always pick up. Every ViewModel that exposes a
  `Localization` property now raises
  `OnPropertyChanged(nameof(Localization))` on culture change
  (`SettingsViewModel`, `MainWindowViewModel`, `UpdateViewModel`,
  `AboutViewModel`, and the existing listeners on
  `DashboardViewModel`, `LogViewerViewModel`, `HotkeyBindingViewModel`).
- **Log drawer toggle label on the nav rail required an app restart to
  switch language** — same root cause as the Settings bug, same fix.

### Added

- **Output folder picker** in Settings. Click **Browse…** next to the
  Output folder field to open the native folder picker instead of
  typing a path by hand. The picker seeds from the current value when
  set. New resx keys `Settings_Browse`, `Settings_BrowseTitle`,
  `Settings_BrowseTooltip` in EN / VI / AR.

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
