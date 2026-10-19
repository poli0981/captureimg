# Development environment

The toolchain used to build, test and release CaptureImage. Hardware reference:
[`pc_spec.md`](pc_spec.md). Vietnamese version: [`i18n/vi/dev_env.md`](i18n/vi/dev_env.md).

## Toolchain

| Tool | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0 (exact feature band pinned in [`global.json`](../global.json)) | Build, test, run |
| **Windows** | 11 22H2 or later | Required by the WPF app and `Windows.Graphics.Capture` |
| **Git** | 2.40 or later | Source control |
| **GPG** | with Ed25519 support | Signed commits and tags (required on `main` and for release tags) |
| **GitHub CLI** (`gh`) | recent | Pull requests, releases, attestation checks |
| **Node.js** | 22 LTS or later | `markdownlint-cli2` for the docs check (optional locally) |

The Windows SDK projections come from NuGet with the target framework
(`net10.0-windows10.0.22621.0`); no separate Windows SDK install is needed. CI uses
`actions/setup-dotnet` with `global-json-file: global.json`, so local and CI SDKs match.

.NET tools used by CI are pinned in [`.config/dotnet-tools.json`](../.config/dotnet-tools.json) and
installed with `dotnet tool restore`: `vpk` (Velopack), JetBrains InspectCode, `nuget-license`,
ReportGenerator and CycloneDX.

## Editors

- **JetBrains Rider** (primary) — the ReSharper inspections match the `deadcode` CI check.
- **Visual Studio 2026** or **Visual Studio Code** with the C# Dev Kit also work.

## Build, test, run

```powershell
git clone https://github.com/poli0981/captureimg.git
cd captureimg
dotnet build CaptureImage.sln -c Release
dotnet test --solution CaptureImage.sln -c Release
dotnet run --project src/CaptureImage.App -c Release -- --data-dir .\.devdata --instance-id dev
```

Warnings are errors and `src/` uses the recommended .NET analyzers, so a clean local build means the
CI build will compile too. See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for the formatting commands
and the full list of required checks.

## Release tooling

Releases are produced by GitHub Actions from signed tags — see [`RELEASING.md`](RELEASING.md).

## Related documents

- [`pc_spec.md`](pc_spec.md) — hardware reference.
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — how the app is built.
- [`CONTRIBUTORS.md`](CONTRIBUTORS.md) — contributors and AI-assistance policy.
- [`../README.md`](../README.md) — project overview.
