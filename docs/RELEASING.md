# Releasing CaptureImage

_Last updated: 2026-10-19 (CaptureImage 2.0)_

Releases are built by [`.github/workflows/release.yml`](../.github/workflows/release.yml) from a
signed tag and published as a **draft** first. Nothing reaches users until a maintainer publishes
the draft.

## 1. Prepare the release pull request

1. Make sure `main` is green.
2. On a branch (e.g. `chore/release-2.1.0`):
   - set `<Version>` in [`Directory.Build.props`](../Directory.Build.props) — the single source of
     the version; the release workflow refuses a tag that doesn't match it;
   - rename `## [Unreleased]` in [`CHANGELOG.md`](../CHANGELOG.md) to `## [2.1.0] - YYYY-MM-DD` and
     add a new empty `## [Unreleased]` above it. The workflow uses this section as the release notes
     shown in the app and on GitHub.
3. Open the pull request, let the required checks pass, squash-merge.

## 2. Tag

Tags must be signed (the `main` branch and `v*` tags are protected by rulesets; tags can't be moved
or deleted once pushed).

```bash
git switch main
git pull --ff-only
git tag -s v2.1.0 -m "CaptureImage v2.1.0"
git push origin v2.1.0
```

The workflow then:

1. checks the tag is SemVer and equals `<Version>`;
2. restores with lock files, builds, runs all tests;
3. publishes the app (framework-dependent, `win-x64`) and runs it with `--smoke-test`;
4. packs it with Velopack (`vpk`, same version as the library): package id `CaptureImage`, main
   executable `CaptureImage.App.exe`, channel `win`, shortcuts on the desktop and Start menu, the
   .NET 10 Desktop Runtime (minimum patch from `RUNTIME`) as a prerequisite, full packages only;
5. adds a CycloneDX SBOM and `SHA256SUMS.txt`, attests build provenance, and creates or updates a
   **draft** release with all assets.

> Never change the package id, executable name, channel or shortcut set — installed copies would
> stop updating or end up with duplicate shortcuts.

## 3. Verify the draft

- [ ] Assets: `CaptureImage-win-Setup.exe`, `CaptureImage-win-Portable.zip`,
      `CaptureImage-<version>-full.nupkg`, `releases.win.json`, `assets.win.json`, `RELEASES`,
      `SHA256SUMS.txt`, `CaptureImage-<version>-sbom.cdx.json`.
- [ ] `gh attestation verify CaptureImage-win-Setup.exe --repo poli0981/captureimg` succeeds.
- [ ] On a clean machine or Windows Sandbox: Setup installs (and installs the .NET runtime if
      missing), the app starts, About shows the new version.
- [ ] Update path: install the previous release, then apply the new package
      (`Update.exe apply --package <full.nupkg>` in the install folder, or the in-app updater once
      published) — settings are kept and migrated.
- [ ] The portable zip starts with `--smoke-test --data-dir <temp>` and exits 0.

## 4. Publish

```bash
gh release edit v2.1.0 --draft=false
```

Publishing with your own token triggers `release: published`, which runs the announcement workflow.
Installed copies see the update on their next check.

## 5. When something goes wrong

- **The workflow failed** — fix the problem on `main`, then re-run it for the same tag with
  **Actions → Release → Run workflow** (`tag: v2.1.0`, `dry_run: false`). It checks out the tag with
  the workflow from `main`, so a broken tag doesn't need to be moved. Use `dry_run: true` to build
  and pack without touching the release.
- **A bad release was published** — publish a fixed patch release; don't delete the old one (users
  may be mid-update). Mark the bad one as pre-release or edit its notes if needed.

## 6. Code signing

The installer isn't Authenticode-signed yet (the SignPath application was declined), so SmartScreen
warns about an unknown publisher. Users can verify downloads with `SHA256SUMS.txt` or the
build-provenance attestation — see [`legal/DISCLAIMER.md`](legal/DISCLAIMER.md), section 7. When a
certificate becomes available (SignPath, Azure Artifact Signing or a CA), sign with `vpk pack
--signTemplate` / `--azureTrustedSignFile` and update the disclaimer.

## 7. Versioning

[Semantic Versioning](https://semver.org): **major** for breaking changes to behavior, settings or
supported systems; **minor** for new features; **patch** for fixes. Pre-releases use a suffix
(`v2.1.0-beta.1`) and are marked as pre-releases automatically; the in-app updater ignores them.
