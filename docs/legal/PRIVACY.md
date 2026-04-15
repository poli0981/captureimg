# Privacy Policy

_Last updated: 2026-04-15_

This policy describes what data **CaptureImage** reads from your computer,
what it stores, and what it sends over the network. It applies to the
official builds distributed from the project's GitHub Releases page.

## Summary

- CaptureImage is a **local, offline-first** desktop application.
- It **does not** collect telemetry, analytics, usage metrics, crash
  reports, or any personal information.
- It does not create an account, ask for credentials, or authenticate you
  with any service.
- The only network access the app performs is the **update check** against
  a configured GitHub repository's Releases API (see §3), and only when you
  explicitly enable auto-check in Settings or click "Check for updates".
- Optional cloud upload providers (Google Drive, Dropbox) are **post-1.0
  stretch features** and are not present in this release.

## 1. Data the app reads from your machine

To build the list of capture targets and to save your settings, the app
reads the following on each launch or when you refresh the Dashboard:

| Source | What is read | Why |
|---|---|---|
| Running processes | Process IDs, process names, executable paths, main window handles and titles | Populate the Dashboard list; resolve a target HWND for capture |
| Executable icons | The associated icon of each target's `.exe` | Display next to each target in the Dashboard |
| Windows registry | `HKLM\SOFTWARE\Valve\Steam\InstallPath` (32- and 64-bit views) | Locate the Steam install directory |
| File system | `steamapps\libraryfolders.vdf` and `steamapps\appmanifest_*.acf` under every detected Steam library | Attribute capture targets to Steam games so the warning badge can be shown |
| File system | `%LocalAppData%\CaptureImage\settings.json` | Load your persisted preferences |
| File system | The captured window's pixel buffer via Direct3D11 / GDI | Produce the output image |

None of this data leaves your machine. Everything is kept in memory for the
duration of the process, except settings and the captures you explicitly
save.

## 2. Data the app writes to your machine

| Location | What is written | Retention |
|---|---|---|
| `%LocalAppData%\CaptureImage\settings.json` | Your preferences (language, hotkey, format, quality sliders, output folder, toggles) | Until you delete the file or uninstall the app |
| `%LocalAppData%\CaptureImage\logs\captureimg-YYYYMMDD.log` | Diagnostic log: process list, capture events, errors, settings changes | Rolling daily; max 14 files, 10 MB each |
| Your chosen output folder (default: `%USERPROFILE%\Pictures\CaptureImage\`) | The PNG / JPEG / WebP / TIFF files you capture | Until you delete them |

Logs are written in plain text and contain only information the app
already knows about your local environment (process names, window titles,
file paths, error stacks). They do **not** contain window contents, pixel
data, or personal information from your documents.

## 3. Network access

The only built-in network access is the **update check**, and only when
both of the following are true:

1. `UiSettings.AutoCheck` is enabled (off by default) **or** you click the
   "Check for updates" button manually.
2. You are connected to the internet.

The update check performs an HTTPS GET against the GitHub Releases API for
the configured repository. No cookies, headers, or identifiers are sent
beyond the standard `User-Agent` that the .NET HTTP client adds, and
Velopack's default metadata. GitHub may log the request in its own access
logs under GitHub's privacy policy — CaptureImage does not control or
receive those logs.

If an update is available, clicking **Download** issues a second HTTPS
request to GitHub's Releases CDN for the package bytes. Download progress
is kept entirely in memory.

CaptureImage does **not**:

- Send crash reports.
- Ping any analytics or metrics endpoint.
- Upload any captured image anywhere automatically.
- Check in with a license server.
- Phone home with system information, hardware fingerprints, or GUIDs.

## 4. Optional cloud upload (future)

The project plan includes optional cloud upload providers (Google Drive,
Dropbox) as a post-1.0 stretch feature. If/when those land, they will be
**off by default**, documented separately in this file, and require the
user to:

1. Explicitly enable the provider in Settings.
2. Complete an OAuth authorization flow that grants the app access to a
   single folder.
3. Opt in per-capture or via a global toggle.

No cloud upload code is present or compiled into the binary in this
release.

## 5. Your rights

Because no data leaves your machine, there is nothing for the project to
delete, export, or access on request. You can:

- Delete `%LocalAppData%\CaptureImage\` to clear all settings and logs.
- Uninstall the app via its installer or by deleting its folder.
- Review the source code on GitHub — the project is licensed under
  GPL-3.0 so every network call is auditable.

If a future release changes this policy, the changes will be noted in
[`CHANGELOG.md`](../../CHANGELOG.md) and in the `Last updated` field at the
top of this file.

## 6. Contact

CaptureImage is a volunteer-run open-source project. Privacy questions can
be raised as GitHub issues on the repository. There is no dedicated
privacy officer, data processing agreement, or paid support channel.
