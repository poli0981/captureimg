# Privacy Policy

_Last updated: 2026-10-26 (CaptureImage 2.0.2)_

This policy describes what **CaptureImage** reads on your computer, what it stores, and what it
sends over the network. It applies to the official builds published on the project's
[GitHub Releases](https://github.com/poli0981/captureimg/releases) page.

## Summary

- CaptureImage is a **local, offline** desktop application.
- It collects **no telemetry, analytics, usage statistics or crash reports**, and has no account
  or sign-in.
- The only network access is the **update check and download** against GitHub Releases (§3).
- Captures stay on your computer unless you move them yourself.

## 1. What the app reads

| Source | What is read | Why |
|---|---|---|
| Open windows | Window handles, titles, process ids, process names and executable paths, from Windows window events (`SetWinEventHook`) and process queries | The Dashboard window list, game profiles and auto-arm |
| Executable icons | The icon of each listed program | Shown next to each window |
| Displays | Monitor positions, scaling (DPI), HDR state and SDR white level | Multi-monitor capture and HDR tone mapping |
| Screen contents | The pixels of the window, monitor or area being captured — **only when you press a capture hotkey or click a capture button** | To produce the screenshot |
| Steam | `HKCU\Software\Valve\Steam\SteamPath` or `HKLM\SOFTWARE\Valve\Steam\InstallPath`, then `libraryfolders.vdf` and `appmanifest_*.acf` | The Steam badge and Steam AppId matching in profiles |
| Image files | Pictures in your capture output folders | The Library index and its thumbnails |
| Settings | `settings.json` in the data folder (§2); on the first start of 2.x, the 1.x file in `%LocalAppData%\CaptureImage` is copied | Your preferences |

Keyboard input is **not** monitored. The capture hotkeys are registered with Windows
(`RegisterHotKey`) only while you have them armed; Windows then tells the app when one of those
exact combinations is pressed. While the hotkey recorder in Settings is open, it reads the keys you
press in that field.

**Text recognition (OCR)** runs on your device with the Windows OCR engine, only when you click
**Extract text**; the recognized text is copied to your clipboard and is not stored.

## 2. What the app writes

| Location | Content | Kept until |
|---|---|---|
| `%LocalAppData%\poli0981\CaptureImage\settings.json` (and `settings.*.bak.json` backups made before a reset or when a file can't be read) | Your preferences and game profiles | You reset them or delete the folder |
| `%LocalAppData%\poli0981\CaptureImage\library.json` | Path, time, size, format and game name of each capture | The capture is deleted or the folder is removed |
| `%LocalAppData%\poli0981\CaptureImage\thumbnails\` | Small JPEG previews for the Library | You delete the folder |
| `%LocalAppData%\poli0981\CaptureImage\logs\` | Diagnostic log (§2.1) | 14 daily files, at most 10 MB each |
| Your output folder (default `Pictures\CaptureImage`) | Your captures, and the `.jxr` HDR original when that option is on | You delete them |
| Clipboard | A capture, a file path or recognized text — when you choose to copy, or when the clipboard mode is on | The next thing you copy |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `CaptureImage` | The command that starts the app with Windows — only when **Start with Windows** is on | You turn the option off or uninstall |

Deleting a capture in the Library moves it (and its HDR original) to the **Recycle Bin**.

### 2.1 Logs

Logs contain what the app does: settings changes, capture results with file names and sizes,
hotkey registrations, errors. Your user-profile path is replaced by `%USERPROFILE%`, and window
titles are only logged at the **Debug** level. Logs never contain pixels or the content of your
documents.

### 2.2 Diagnostics export

**Settings → Advanced → Export diagnostics** creates a zip with recent logs, your settings and
system information (Windows and .NET versions, graphics adapter, monitors, scaling and HDR state),
with user paths removed. It is saved where you choose and is never sent anywhere by the app —
attach it to a bug report only if you want to.

### 2.3 Local communication

A second copy of the app talks to the first through a local named pipe that only your Windows
account can open, to bring the existing window forward. Nothing leaves the computer.

## 3. Network access

The app connects to the internet only to **check for and download updates**:

- when you open the **Update** page (one check per session) or click **Check for updates**, it
  downloads the release list (`releases.win.json`) from this project's GitHub Releases over HTTPS;
- when you click **Download**, it downloads the update package from GitHub.

No identifiers, cookies or system information are sent beyond what any HTTPS request carries (your
IP address and a standard user agent). GitHub may log these requests under
[its own privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

When the .NET Desktop Runtime is missing or older than the app needs, Setup and the updater
download it from Microsoft.
Links in the About page open in your browser; the app itself does not fetch them.

CaptureImage does **not** send crash reports, contact analytics or license servers, upload
captures, or collect hardware identifiers.

## 4. Your choices

- Disarm the hotkeys at any time; nothing is captured without your action.
- Delete `%LocalAppData%\poli0981\CaptureImage` to remove settings, the library index, thumbnails
  and logs. Uninstalling removes the program and the "Start with Windows" entry; your captures and
  the data folder stay until you delete them.
- The source code is public under GPL-3.0, so every behavior above can be checked.

Changes to this policy are listed in [`CHANGELOG.md`](../../CHANGELOG.md) and in the date above.

## 5. Contact

Privacy questions: `privacy@poli0981.dev`. Other channels: <https://poli0981.dev/links>.
CaptureImage is a volunteer open-source project with no paid support.
