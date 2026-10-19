# Disclaimer

_Last updated: 2026-10-19 (CaptureImage 2.0)_

This document describes the known limitations of **CaptureImage** and the caveats that apply to its
development, translations and licensing. Please read it before relying on the application.

## 1. No warranty

CaptureImage is licensed under the **GNU General Public License v3.0 or later** (see
[`LICENSE`](../../LICENSE)). Under sections 15 and 16 of that license the software is provided
**"AS IS"**, without warranty of any kind, express or implied, including the warranties of
merchantability, fitness for a particular purpose and non-infringement.

The authors and contributors are not liable for any claim, damages or other liability arising from
the software or its use — including, without limitation:

- lost or corrupted screenshots,
- game crashes, anti-cheat actions or account suspensions involving third-party software that
  interacts with a game,
- data loss from full disks or abrupt shutdowns, or
- any consequence of running an unsigned, self-built or unofficially distributed binary.

**Use at your own risk.**

## 2. Capture support is not universal

CaptureImage captures windows and screens with the Windows `Windows.Graphics.Capture` API. **It
does not, and by design cannot, capture every application or game.** Known limitations include:

1. **Protected content.** Windows that mark their content as protected (DRM video, some
   conferencing and banking apps) produce a black image or a "protected content" error. This is a
   Windows restriction.
2. **Exclusive full-screen games.** Some older games that bypass the desktop compositor cannot be
   captured, and the region overlays and notifications cannot appear above them. Borderless or
   windowed mode usually works.
3. **Anti-cheat.** Games protected by kernel-level anti-cheat (VAC, EAC, BattlEye, Vanguard and
   others) may block external capture. CaptureImage does not read game memory or inject code, but
   anti-cheat decisions are outside our control. If a game's anti-cheat objects to CaptureImage,
   **stop using it with that game** and tell us (§8).
4. **Hotkeys.** While armed, the capture hotkeys are registered with Windows, so the game and other
   apps do not receive those key combinations; disarm to give them back. A few games read the
   keyboard in a way that bypasses registered hotkeys — use the tray menu's region or screen capture
   there.
5. **HDR.** HDR captures are tone-mapped to a standard image using the display's SDR white level.
   Colors and highlights are an approximation of what an HDR display shows; keep the JPEG XR original
   if exact HDR data matters.
6. **Minimized windows** have nothing to capture; restore the window first.
7. **Mixed-DPI setups.** A window spanning monitors with different scaling may show scaling seams in
   its capture.

If a game does not work, please open an issue with the game, its graphics API, and whether it runs
windowed, borderless or full screen.

## 3. Development is AI-assisted

CaptureImage is developed with substantial help from an AI coding assistant — Claude (Anthropic),
used through Claude Code — for code, tests, Windows interop, user-interface layout, translations,
commit messages and documentation (including this disclaimer).

The maintainer reviews, tests and accepts every change before it is released. AI-assisted code can
still contain subtle mistakes, so treat the source as you would any community contribution: read
it, audit it and run the tests before you depend on it. Commits with meaningful AI authorship carry
a `Co-Authored-By` trailer; the git history is the authoritative record.

## 4. Translations are machine-assisted

The user interface is available in English, Vietnamese, Arabic, Japanese, Simplified Chinese,
Korean, Spanish, French, German, Brazilian Portuguese and Russian. Except where a native speaker has
reviewed them, the translations were produced with AI assistance. Terminology may not match native
conventions everywhere, and some technical words (for example "hotkey") are kept as users commonly
know them. Native speakers who can proofread a language are very welcome — see
[`CONTRIBUTING.md`](../../CONTRIBUTING.md).

## 5. Trademarks

Microsoft, Windows, DirectX, .NET, Steam, Valve, GitHub, WPF UI, Velopack, SkiaSharp, Serilog and
other names that appear in the source code, documentation or user interface are trademarks of their
respective owners, used only to identify them. No affiliation or endorsement is implied.

## 6. No legal advice

This disclaimer summarizes the project's intent in plain language and does not replace the license.
Where it conflicts with [`LICENSE`](../../LICENSE), the license controls. If you need legal certainty
— for example to redistribute or bundle CaptureImage — consult a lawyer in your jurisdiction.

## 7. Unsigned installer — SmartScreen

Until a code-signing certificate is available, the installer `CaptureImage-win-Setup.exe` is **not
Authenticode-signed**, and Windows SmartScreen shows an "unknown publisher" warning on first run.
That alone does not mean the file was altered. To verify a download:

1. Download `SHA256SUMS.txt` from the same release page.
2. Compute the hash of your download and compare:

   ```powershell
   Get-FileHash -Algorithm SHA256 .\CaptureImage-win-Setup.exe
   ```

3. Or verify the GitHub build-provenance attestation:

   ```powershell
   gh attestation verify .\CaptureImage-win-Setup.exe --repo poli0981/captureimg
   ```

Only then choose **More info → Run anyway**. If the hashes don't match, **do not run the file** and
report it to `security@poli0981.dev` or through a GitHub security advisory.

## 8. Contact

- Game publishers and anti-cheat vendors with concerns about CaptureImage: `games@poli0981.dev`.
- Copyright, DMCA or takedown requests: `dmca@poli0981.dev` (or `takedown@poli0981.dev`).
- Other legal questions: `legal@poli0981.dev`.
- Everything else: `contact@poli0981.dev` · <https://poli0981.dev/links>.
