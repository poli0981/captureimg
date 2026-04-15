# Disclaimer

_Last updated: 2026-04-15_

This document describes the known limitations of **CaptureImage** and the
caveats that apply to its development, translations, and licensing. Please
read it before using the application in any production or archival setting.

## 1. No warranty

CaptureImage is licensed under the **GNU General Public License v3.0 or
later** (see [`LICENSE`](../../LICENSE)). Under Sections 15 and 16 of that
license, the software is provided **"AS IS"**, without warranty of any kind,
express or implied, including but not limited to the warranties of
merchantability, fitness for a particular purpose, and non-infringement.

The author(s) and contributors shall not be held liable for any claim,
damages, or other liability, whether in an action of contract, tort, or
otherwise, arising from, out of, or in connection with the software or the
use or other dealings in the software — including, without limitation:

- lost or corrupted screenshots,
- game crashes, anti-cheat bans, or account suspensions caused by third-party
  software interacting with a title's process,
- data loss resulting from disk full errors, abrupt shutdowns, or failed
  atomic file writes, or
- any consequence of running an unsigned binary, a self-built binary, or a
  binary downloaded from an unofficial source.

**Use at your own risk.**

## 2. Capture support is not universal

CaptureImage is designed to capture the visible surface of a target window
using the Windows `Windows.Graphics.Capture` API, with an optional GDI
`PrintWindow` fallback for legacy windowed applications. **It does not, and
by design cannot, capture every application or game.** Known limitations
include, without limitation:

1. **DRM / protected content.** Applications that mark their swap chain as
   protected (DRM movie players, secure conferencing, some banking apps) will
   produce either an all-black frame or a `ProtectedContent` error. This is a
   Windows-level restriction, not a bug in CaptureImage.
2. **Exclusive-fullscreen Direct3D 9 titles.** Some older games that refuse
   to register with the Desktop Window Manager cannot be captured by
   Windows.Graphics.Capture. The GDI fallback may return a black frame for
   these as well.
3. **Anti-cheat protected titles.** Games protected by kernel-mode anti-cheat
   systems (VAC, EAC, BattlEye, Vanguard, etc.) may block external capture
   tools, or — in rare cases — treat such tools as untrusted and apply
   account sanctions. CaptureImage is **not** a cheat and does not read game
   memory or inject code, but anti-cheat heuristics are outside our control.
   If a given title's anti-cheat flags this tool, **stop using it with that
   title immediately** and report the incident via the project's issue
   tracker.
4. **High-DPI / multi-monitor mismatches.** When a target window spans two
   monitors with different scale factors, the captured frame may include
   scaling artifacts at the monitor boundary.
5. **Layered / transparent windows.** Capture fidelity for windows that rely
   heavily on layered composition (e.g. some overlays, screen reader
   highlights) is best-effort and may omit overlay effects.
6. **Minimized or occluded windows.** A minimized window has no live surface
   to capture. Windows.Graphics.Capture may deliver a stale cached frame,
   the last-rendered bitmap, or time out with `NoFrameArrived`.

If a specific title does not work, please file an issue describing the game,
its graphics API, whether it runs windowed / borderless / fullscreen, and
whether the `PrintWindow` fallback was also unable to capture it.

## 3. Development was AI-assisted

The implementation of CaptureImage was carried out with significant
assistance from the **Claude Opus 4.6** large language model (Anthropic),
used through Claude Code, during the 2026 development cycle. AI assistance
included — but was not limited to — code generation, unit test drafting,
architectural design proposals, Win32 / WinRT interop troubleshooting,
P/Invoke signature authoring, XAML layout, commit message drafting, and
documentation writing (including this disclaimer).

Every change was reviewed, accepted, and tested by a human maintainer
before landing in version control. However, AI-assisted code may still
contain subtle errors, non-idiomatic patterns, or references to APIs that
behave differently than the model expected. Users and downstream
distributors should treat the source as they would any other community
contribution: **read it, audit it, and run the tests before shipping.**

Commits authored with AI assistance carry a `Co-Authored-By` trailer that
names the model. Git history is the authoritative record.

## 4. Translations are machine-assisted

CaptureImage ships with localized strings for English (`en-US`),
Vietnamese (`vi-VN`), and Arabic (`ar-SA`). The Arabic translation in
particular was produced with assistance from the same AI model; the
Vietnamese strings were reviewed by a native speaker but still pass
through an automated generation step.

Consequences:

- Terminology may not match native conventions in every locale.
- Some technical jargon (e.g. "hotkey", "toast", "frame") is deliberately
  kept in English because it is more recognizable to developers than any
  localized equivalent.
- Right-to-left layouts (Arabic) are verified for structural mirroring but
  copy may still sound machine-translated.

Native speakers willing to proofread any shipped language are warmly
invited to open a pull request — see [`CONTRIBUTORS.md`](../CONTRIBUTORS.md).

## 5. Trademarks

"Steam", "Dota", "Counter-Strike", "Windows", "DirectX",
"Microsoft .NET", "Avalonia", "Velopack", "SkiaSharp", "ImageSharp",
"Serilog", "SharpHook", and any other third-party names that appear in the
source, documentation, or UI strings are trademarks or registered
trademarks of their respective owners. Their use in this project is for
identification purposes only and does not imply any affiliation with, or
endorsement by, the respective trademark holders.

## 6. No legal advice

This disclaimer summarizes the project's intent in plain language and is
**not** a substitute for the actual license text. Where this document
conflicts with [`LICENSE`](../../LICENSE), the license text controls. If
you need legal certainty about your use of CaptureImage — for example
because you intend to redistribute it, bundle it with another product, or
integrate it into a commercial offering — consult a qualified attorney in
your jurisdiction.
