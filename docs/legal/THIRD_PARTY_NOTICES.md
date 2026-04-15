# Third-party Notices

_Last updated: 2026-04-15_

CaptureImage is built on top of several open-source components. This file
lists every runtime and build dependency together with its upstream license
and project URL. A shorter version is in the repository root [`NOTICE`](../../NOTICE) file.

All listed licenses are **compatible with distribution under the
GNU General Public License v3.0 or later**, which is the license the
CaptureImage binary is distributed under.

## Runtime dependencies

### UI framework

| Package | License | Upstream |
|---|---|---|
| Avalonia | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Desktop | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Themes.Fluent | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Fonts.Inter | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Diagnostics | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Inter typeface | SIL Open Font License 1.1 | <https://github.com/rsms/inter> |
| SkiaSharp | MIT | <https://github.com/mono/SkiaSharp> |
| SkiaSharp.NativeAssets.Win32 | MIT | <https://github.com/mono/SkiaSharp> |

### MVVM + DI + logging

| Package | License | Upstream |
|---|---|---|
| CommunityToolkit.Mvvm | MIT | <https://github.com/CommunityToolkit/dotnet> |
| Microsoft.Extensions.DependencyInjection | MIT | <https://github.com/dotnet/runtime> |
| Microsoft.Extensions.Hosting | MIT | <https://github.com/dotnet/runtime> |
| Microsoft.Extensions.Localization | MIT | <https://github.com/dotnet/runtime> |
| Microsoft.Extensions.Logging | MIT | <https://github.com/dotnet/runtime> |
| Microsoft.Extensions.Options | MIT | <https://github.com/dotnet/runtime> |
| Serilog | Apache 2.0 | <https://github.com/serilog/serilog> |
| Serilog.Extensions.Hosting | Apache 2.0 | <https://github.com/serilog/serilog-extensions-hosting> |
| Serilog.Extensions.Logging | Apache 2.0 | <https://github.com/serilog/serilog-extensions-logging> |
| Serilog.Sinks.Console | Apache 2.0 | <https://github.com/serilog/serilog-sinks-console> |
| Serilog.Sinks.File | Apache 2.0 | <https://github.com/serilog/serilog-sinks-file> |
| Serilog.Sinks.Async | Apache 2.0 | <https://github.com/serilog/serilog-sinks-async> |

### Capture pipeline (Windows)

| Package | License | Upstream |
|---|---|---|
| Vortice.Direct3D11 | MIT | <https://github.com/amerkoleci/Vortice.Windows> |
| Vortice.DXGI | MIT | <https://github.com/amerkoleci/Vortice.Windows> |
| Microsoft.Windows.CsWinRT (runtime only) | MIT | <https://github.com/microsoft/CsWinRT> |
| SixLabors.ImageSharp (>= 3.1) | Apache 2.0 | <https://github.com/SixLabors/ImageSharp> |
| System.Drawing.Common | MIT | <https://github.com/dotnet/runtime> |
| System.Management | MIT | <https://github.com/dotnet/runtime> |
| Microsoft.Win32.Registry | MIT | <https://github.com/dotnet/runtime> |
| System.IO.Abstractions | MIT | <https://github.com/TestableIO/System.IO.Abstractions> |

### Hotkeys + state machine + updater

| Package | License | Upstream |
|---|---|---|
| SharpHook | MIT | <https://github.com/TolikPylypchuk/SharpHook> |
| libuiohook (bundled with SharpHook) | GPL-3.0 / LGPL-3.0 | <https://github.com/kwhat/libuiohook> |
| Stateless | Apache 2.0 | <https://github.com/dotnet-state-machine/stateless> |
| Velopack | MIT | <https://github.com/velopack/velopack> |

### Test-only (not redistributed)

| Package | License |
|---|---|
| xUnit | Apache 2.0 |
| xunit.runner.visualstudio | Apache 2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| FluentAssertions (version 6.x) | Apache 2.0 |
| NSubstitute | BSD-3-Clause |
| System.IO.Abstractions.TestingHelpers | MIT |
| coverlet.collector | MIT |

## libuiohook + GPL compatibility note

SharpHook wraps the native `libuiohook` library, which is dual-licensed
under GPL-3.0 and LGPL-3.0. CaptureImage is itself distributed under
GPL-3.0-or-later, so this is compatible. Downstream redistributors of
CaptureImage must therefore also respect the libuiohook license terms
when redistributing the compiled binary.

## Full license texts

The full text of every license referenced above is available from:

- GNU GPL v3.0: <https://www.gnu.org/licenses/gpl-3.0.txt> — shipped as [`LICENSE`](../../LICENSE) in this repository.
- GNU LGPL v3.0: <https://www.gnu.org/licenses/lgpl-3.0.txt>
- Apache License 2.0: <https://www.apache.org/licenses/LICENSE-2.0.txt>
- MIT License: <https://opensource.org/licenses/MIT>
- BSD 3-Clause: <https://opensource.org/licenses/BSD-3-Clause>
- SIL Open Font License 1.1: <https://openfontlicense.org/open-font-license-official-text/>

Per the Apache 2.0 license terms (Section 4), this file serves as the
required notice for Apache-licensed components.

## Reporting a missing attribution

If you ship a downstream copy of CaptureImage and discover that a
dependency is missing from this list, please open a pull request or issue
with the package name, version, and upstream license. We aim to keep this
list complete at every tagged release.
