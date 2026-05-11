# Developer machine specification

This document describes the reference hardware CaptureImage is
developed and validated against. It is a snapshot of the maintainer's
primary development box — not a runtime requirement. End-user
requirements are listed in the project [README](../README.md#requirements).

A Vietnamese mirror of this document lives at
[docs/i18n/vi/pc_spec.md](i18n/vi/pc_spec.md).

## Primary development machine

| Component   | Details                                                           |
|-------------|-------------------------------------------------------------------|
| **OS**      | Windows 11 Pro 25H2 Insider Preview (Dev Channel), build 26300.8376 |
| **CPU**     | Intel Core i7-14700KF                                             |
| **GPU**     | NVIDIA GeForce RTX 5080 (16 GB VRAM)                              |
| **RAM**     | 32 GB DDR5                                                        |
| **Storage** | 1 TB SSD                                                          |
| **IDE**     | JetBrains 2026.x paid lineup (Rider for this repo; PyCharm / WebStorm / RustRover for sibling projects) + Visual Studio Code |

The Windows Insider Dev Channel build is intentional — CaptureImage
exercises the latest `Windows.Graphics.Capture` surface area and
benefits from running on builds ahead of stable so regressions on
upcoming public releases are caught early.

## Mobile test devices

Some adjacent / future projects ship a small web companion. When that
work lands, mobile browser testing is performed on:

- **iPhone 14 Pro** (iOS 26.x)
- **iPhone 13 Pro Max** (iOS 26.x)

Browsers under test on iOS: **Chrome**, **Brave**.

> **Note.** CaptureImage itself is a Windows 11 desktop application
> (WinUI 3 / Windows App SDK) and has no iOS / web build. The mobile
> devices above are listed for transparency about the maintainer's
> overall test fleet; they are not used to validate CaptureImage.

## Related documents

- [`docs/dev_env.md`](dev_env.md) — toolchain versions (Python, Node,
  Rust, Git) and IDE configuration.
- [`README.md` § Requirements](../README.md#requirements) — end-user
  runtime requirements (Windows 11 22H2+, no specific GPU needed).
- [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) — software architecture.
- [`docs/RELEASING.md`](RELEASING.md) — release process.
