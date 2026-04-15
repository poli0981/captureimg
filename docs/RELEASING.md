# Releasing CaptureImage

_Last updated: 2026-04-15_

This document describes how to cut a new release of CaptureImage end to
end: tagging, CI/CD, verifying the installer, and what to tell users
about the unsigned-binary warning.

## 1. Prerequisites

You need:

- Push access to <https://github.com/poli0981/captureimg>.
- The working tree on `main` is clean and all tests pass locally:
  ```powershell
  dotnet build CaptureImage.sln -c Release /p:TreatWarningsAsErrors=true
  dotnet test CaptureImage.sln -c Release
  ```
- `CHANGELOG.md` has an entry for the release under `[Unreleased]`.
  Move the entries into a new `## [1.0.0] - 2026-MM-DD` section before
  tagging — the CI release workflow does not rewrite the file.

## 2. Cutting a release

Releases are cut by pushing a tag. The tag triggers
[`.github/workflows/release.yml`](../.github/workflows/release.yml) which
builds, tests, publishes a single-file self-contained Win64 build, wraps
it with Velopack, computes SHA256 checksums, and uploads everything as a
GitHub Release.

```powershell
# Update the version number in the source metadata.
# (Directory.Build.props already sets <Version> to the target release.)
# Commit the CHANGELOG bump.
git add CHANGELOG.md
git commit -m "chore(release): CaptureImage v1.0.0"

# Create and push the tag. Use an annotated tag with a release message.
git tag -a v1.0.0 -m "CaptureImage v1.0.0"
git push origin main
git push origin v1.0.0
```

Within a minute or two, GitHub Actions should show a `Release` workflow
running. Total build time is about 8–12 minutes on a `windows-latest`
runner.

When the workflow finishes:

- A new GitHub Release appears under
  <https://github.com/poli0981/captureimg/releases>.
- It contains `Setup.exe`, the Velopack `.nupkg`, the `RELEASES`
  manifest, and `SHA256SUMS.txt`.
- A workflow artifact bundle is also uploaded as a backup.

## 3. If the workflow fails mid-run

Do NOT manually create a release with the broken artifacts. Instead:

1. Investigate the failure in the Actions tab.
2. Delete the broken tag both locally and on GitHub:
   ```powershell
   git tag -d v1.0.0
   git push --delete origin v1.0.0
   ```
3. Fix the underlying problem on `main`.
4. Re-tag and re-push.

This keeps the GitHub Releases page as a trustworthy source of truth —
every release there was produced by a clean, green workflow run.

## 4. Post-release checklist

After the release publishes, verify by hand:

- [ ] `Setup.exe` downloads from the Release page.
- [ ] `Setup.exe` installs to `%LocalAppData%\CaptureImage\app-<version>\`
  (Velopack default).
- [ ] First launch opens the main window without crashing.
- [ ] `About` tab shows the new version number.
- [ ] The `Update` tab can hit GitHub and reports the installed version
  matches the latest release.
- [ ] `%LocalAppData%\CaptureImage\logs\captureimg-<today>.log` contains
  `"Velopack update service initialized: installed=True"`.

Also update `docs/CONTRIBUTORS.md` if new contributors landed during the
cycle, and post a short release note in GitHub Discussions if the user
community wants one.

## 5. Unsigned installer — Windows SmartScreen

**CaptureImage is currently NOT code-signed.** The project applied to
<https://signpath.io>'s free OSS program and was declined in the first
round. Until a signing certificate is available, every download will
trigger Windows SmartScreen:

> Windows protected your PC
> Microsoft Defender SmartScreen prevented an unrecognized app from
> starting. Running this app might put your PC at risk.

Users can proceed via **More info** → **Run anyway**, but many will
reasonably refuse. To mitigate:

1. The GitHub Release description links to the `SHA256SUMS.txt` file
   and explains how to verify the download:
   ```powershell
   Get-FileHash -Algorithm SHA256 Setup.exe
   # Compare the output with the hash listed in SHA256SUMS.txt
   ```
2. [`docs/legal/DISCLAIMER.md §7`](legal/DISCLAIMER.md) explicitly
   documents the unsigned state and the SmartScreen warning so users
   know it is expected, not a compromise.
3. When SmartScreen "Unknown publisher" warnings decrease over time
   (SmartScreen learns reputation from download volume + age), we
   re-evaluate whether manual signing is worth the cost.

### When signing becomes available

- [ ] Re-apply to SignPath.io with a more polished project description,
      public download count, and signed commits history.
- [ ] Alternatively, purchase an Azure Trusted Signing subscription
      (~$10/month) or a cheap EV cert from a CA that issues to individuals.
- [ ] Add a `SIGN_EXE` step to `.github/workflows/release.yml` after
      `vpk pack` that calls `signtool sign` on `Setup.exe` and every
      intermediate `.nupkg` contents.
- [ ] Re-run `vpk pack` with the signed binaries so the installer embeds
      the signed files.
- [ ] Update `docs/legal/DISCLAIMER.md §7` to remove the unsigned note.
- [ ] Update this file's §5 to describe the signing step.

## 6. Manual release (emergency only)

If CI is down and you must cut a release by hand, run locally:

```powershell
dotnet publish src/CaptureImage.App/CaptureImage.App.csproj `
  -c Release -r win-x64 --self-contained `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:Version=1.0.0 `
  -o .\publish

dotnet tool install --global vpk
vpk pack `
  --packId CaptureImage `
  --packVersion 1.0.0 `
  --packDir .\publish `
  --mainExe CaptureImage.App.exe `
  --outputDir .\releases

Get-ChildItem .\releases -File | ForEach-Object {
    (Get-FileHash $_ -Algorithm SHA256).Hash.ToLower() + "  " + $_.Name
} | Out-File .\releases\SHA256SUMS.txt -Encoding utf8

# Create the Release manually via the GitHub web UI and attach every
# file from .\releases\ including SHA256SUMS.txt.
```

Prefer CI over manual releases — every manual release is one more
build environment to audit.

## 7. Version policy

CaptureImage uses [Semantic Versioning](https://semver.org) with one
practical twist: as long as the app is pre-1.0 internally (i.e. the
user community is tiny), breaking changes between 1.x.y releases are
still avoided, and API changes in the plugin surface (once that exists)
are flagged in `CHANGELOG.md` as **BREAKING**.

- `MAJOR` bumps when the capture engine gains or loses a backend.
- `MINOR` bumps for new user-visible features.
- `PATCH` bumps for bug fixes and security updates.

Velopack deltas are generated automatically by `vpk pack` — users on an
older version will get a diff download instead of the full installer.
