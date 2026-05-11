# Development environment

This document lists the toolchain versions used to build, test, and
release CaptureImage. Hardware reference: [`docs/pc_spec.md`](pc_spec.md).
Vietnamese mirror: [`docs/i18n/vi/dev_env.md`](i18n/vi/dev_env.md).

## Toolchains

| Toolchain   | Version                                | Purpose                                       |
|-------------|----------------------------------------|-----------------------------------------------|
| **.NET SDK**| 9.0 (exact version pinned in [`global.json`](../global.json)) | Primary build / test runtime |
| **Python**  | 3.12                                   | Release scripting, Discord notification helper (`.github/scripts/discord_notify.py`) |
| **Node.js** | ≥ 22 LTS                               | Future docs site / web companion tooling      |
| **Rust**    | stable, via `rustup`                   | Reserved for future native helpers (none in v1.5.x) |
| **Git**     | recent (≥ 2.40)                        | Source control                                 |
| **GPG**     | any version supporting Ed25519         | Commit + tag signing — `commit.gpgsign=true` is set repo-wide; tag signing required per [`docs/RELEASING.md`](RELEASING.md) |

The CI runner uses `actions/setup-dotnet@v5` with `global-json-file:
global.json`, so the local and CI .NET versions are guaranteed to
match — see [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

## IDE setup

Primary: **JetBrains Rider 2026.x** (paid lineup). Recommended Rider
plugins:

- *.NET Core User Secrets* (built in)
- *Markdown* (built in)
- *Resource Bundle Editor* — handy when editing the 11 `.resx`
  locale files at once
  ([`src/CaptureImage.UI/Resources/Strings/`](../src/CaptureImage.UI/Resources/Strings/))

Sibling JetBrains IDEs used on adjacent projects: **PyCharm**,
**WebStorm**, **RustRover**.

Secondary: **Visual Studio Code** with the C# Dev Kit extension for
quick edits.

## Windows SDK

WinUI 3 + `Windows.Graphics.Capture` projections require the
**Windows 11 22H2 SDK** (10.0.22621). Install via the Visual Studio
Installer ("Windows 11 SDK (10.0.22621.x)") or directly from
<https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/>.

## Build / test / run

```powershell
git clone https://github.com/poli0981/captureimg.git
cd captureimg
dotnet restore
dotnet build CaptureImage.sln -c Release
dotnet test  CaptureImage.sln -c Release
dotnet run --project src/CaptureImage.App -c Release
```

The CI commands are a literal mirror of the above plus
`/p:TreatWarningsAsErrors=true /p:WarningsAsErrors=nullable` — run
the same locally before opening a PR.

## Release tooling

- **Velopack** (`vpk` CLI) — packaging + delta updates.
- **`gh`** (GitHub CLI) — creating Releases, uploading artifacts,
  fetching SHAs (the reusable-workflow pin lookup in
  [`docs/RELEASING.md`](RELEASING.md)).
- See [`docs/RELEASING.md`](RELEASING.md) for the end-to-end cycle.

## Related documents

- [`docs/pc_spec.md`](pc_spec.md) — hardware reference.
- [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) — software architecture.
- [`docs/CONTRIBUTORS.md`](CONTRIBUTORS.md) — contribution policy.
- [`README.md`](../README.md) — project overview.
