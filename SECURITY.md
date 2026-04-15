# Security Policy

_Last updated: 2026-04-15_

## Supported versions

CaptureImage has a single release line. Only the latest tagged release
on the `main` branch receives security updates.

| Version | Supported |
|---|---|
| Latest tagged release | ✅ |
| Older releases | ❌ |
| `main` branch (untagged) | ❌ |

## Reporting a vulnerability

**Please do not open a public issue for security problems.** Instead,
report privately via GitHub Security Advisories:

<https://github.com/poli0981/captureimg/security/advisories/new>

Include as much of the following as you can:

- Affected version(s) (tag name or commit hash).
- A description of the issue and the security impact (arbitrary code
  execution, local privilege escalation, data exfiltration, etc.).
- Reproduction steps or a proof-of-concept.
- Your suggested fix, if any.
- Whether you want public credit in the advisory.

You should expect an acknowledgement within **7 days** and a triage
decision (fix plan + rough timeline) within **14 days**. CaptureImage
is a volunteer-run project — these are good-faith targets, not
guaranteed SLAs.

## Scope — what counts as a security vulnerability

- Arbitrary code execution triggered by opening a crafted settings file,
  image, or update payload.
- Privilege escalation (anything that causes CaptureImage to acquire
  more privileges than the user that launched it).
- Bypass of the capture-target selection (e.g. a way to make the app
  capture a window the user did not select).
- Data exfiltration (anything that causes captured images, process
  names, or settings to leave the user's machine without consent).
- Remote code execution via the Velopack update path (e.g. an attacker
  who can MITM the GitHub Releases fetch and inject a malicious package).
- Memory safety issues in the Win32 / WinRT interop code that could be
  exploited.

## Out of scope

These are bugs, not security issues — please file them via a regular
bug report instead:

- A specific game cannot be captured (this is an expected limitation —
  see [`docs/legal/DISCLAIMER.md §2`](docs/legal/DISCLAIMER.md)).
- The app crashes during startup on an unsupported Windows version.
- SmartScreen warns "Unknown publisher" on the installer (the binary is
  intentionally not signed yet — see
  [`docs/RELEASING.md`](docs/RELEASING.md) §5).
- Third-party anti-cheat software flags the app as untrusted. CaptureImage
  does not inject into or read from game memory, but we cannot control
  how any particular anti-cheat engine classifies capture tools.
- Missing localization, machine-translated strings, or other documentation
  issues — these are tracked as normal bugs.
- Feature requests, even if they would improve user privacy.

## What happens after you report

1. The maintainer acknowledges receipt of the advisory.
2. We reproduce the issue, identify the root cause, and draft a fix.
3. For non-trivial issues the fix lands on a private branch and is
   reviewed before merging to `main`.
4. A new release is cut with the fix.
5. The advisory is made public, crediting the reporter (if they want
   credit). CVE assignment may be requested for high-severity issues.

## Supply chain

CaptureImage depends on a large tree of open-source libraries (see
[`docs/legal/THIRD_PARTY_NOTICES.md`](docs/legal/THIRD_PARTY_NOTICES.md)).
Dependabot runs weekly to pick up upstream security advisories, and
pinned package versions in `Directory.Packages.props` are bumped as
needed.

If you find a vulnerability in one of CaptureImage's dependencies,
please report it upstream first. We will pick it up once the upstream
fix is released.
