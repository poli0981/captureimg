# Changelog

All notable changes to CaptureImage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
