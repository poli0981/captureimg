# Security Policy

_Last updated: 2026-10-19_

## Supported versions

Only the latest release receives security fixes.

| Version | Supported |
|---|---|
| 2.x (latest release) | ✅ |
| 1.x and older | ❌ — update from inside the app or install the latest release |
| Unreleased `main` | ❌ |

## Reporting a vulnerability

**Please don't open a public issue for security problems.** Report privately, either:

- through GitHub's private vulnerability reporting:
  <https://github.com/poli0981/captureimg/security/advisories/new>, or
- by email to `security@poli0981.dev`.

Please include what you can of:

- the affected version (release tag or commit),
- what the issue is and its impact (code execution, privilege escalation, data leaving the
  computer, …),
- steps or a proof of concept to reproduce it,
- a suggested fix, if you have one,
- whether and how you would like to be credited.

You can expect an acknowledgement within **7 days** and a triage decision (fix plan and rough
timeline) within **14 days**. CaptureImage is maintained by a volunteer, so these are good-faith
targets rather than guarantees.

## In scope

- Code execution triggered by a crafted settings file, image, library index or update package.
- CaptureImage gaining more privileges than the user who started it.
- Capturing a window or screen without the user's action, or captures, settings or logs leaving
  the computer without consent.
- The update path: tampering with releases or the update feed, or installing a package that wasn't
  published by this repository.
- The single-instance pipe or other local interfaces being usable by another user or process to
  control the app.
- Memory-safety problems in the Win32 / WinRT / Direct3D interop code.

## Out of scope (please file a normal bug report)

- A particular game can't be captured — see
  [`docs/legal/DISCLAIMER.md`](docs/legal/DISCLAIMER.md), section 2.
- SmartScreen shows "unknown publisher" — the installer isn't code-signed yet; verify downloads as
  described in the [README](README.md#unsigned-installer--smartscreen).
- An anti-cheat system flags the app — CaptureImage doesn't inject into or read from games, but we
  can't control how anti-cheat software classifies capture tools.
- Translation, documentation or accessibility issues, and feature requests.

## After you report

1. The report is acknowledged.
2. The issue is reproduced, the cause found and a fix prepared (privately for non-trivial issues).
3. A release with the fix is published.
4. The advisory is published, crediting you if you wish; a CVE may be requested for serious issues.

## Supply chain

- Dependencies are pinned centrally with lock files; Dependabot proposes updates after a cooldown,
  and every pull request is checked for known-vulnerable packages and build tools and for
  incompatibly licensed packages.
- GitHub Actions are pinned to full commit SHAs and linted with actionlint and zizmor; CodeQL,
  OpenSSF Scorecard and SonarCloud review the code.
- Releases are built by GitHub Actions from signed tags and ship with SHA-256 checksums, a CycloneDX
  SBOM and a build-provenance attestation (`gh attestation verify`).

Vulnerabilities in a dependency should be reported upstream first; we will pick up the fixed
version once it is released.
