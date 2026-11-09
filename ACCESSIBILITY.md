# Accessibility

CaptureImage should work for everyone who plays games on Windows. That includes people who use only
a keyboard, people who use a screen reader, magnification, a contrast theme or large display
scaling, and people who are sensitive to motion. This document describes what the app supports
today, what it does not support yet, how we test it, and how to report a barrier.

It is a statement of goals and of the current state, not a claim of conformance.

## Priorities

- **Target:** [WCAG 2.2](https://www.w3.org/TR/WCAG22/) level AA, applied to desktop software as
  described in the W3C's [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/) guidance. This is a goal.
  No formal audit has been made, so CaptureImage makes no conformance claim.
- **Keyboard:** everything can be done without a mouse, including region selection and the tray
  icon. The one exception is listed under [known limitations](#known-limitations).
- **Screen readers:** every control has a name, and status changes and notifications are
  announced. We test with Narrator.
- **Vision:** the app follows Windows contrast themes. Colour is never the only way information
  is shown. Text stays sharp at any display scaling.
- **Motion and timing:** the app follows Windows' animation and notification-time settings, and
  nothing flashes.
- **Stays out of your way:** notifications never take the keyboard focus away from your game or
  your assistive technology.
- **Language:** the interface is available in 11 languages, and Arabic is laid out right to left.

## What works today

### Keyboard

- **Main window:** Tab and Shift+Tab move between controls. The arrow keys move within lists and
  the navigation pane. Enter or Space activates the focused control.
- **Capture:** each capture mode has its own global hotkey (see the
  [keyboard reference](#keyboard-reference)). The keys are registered only while CaptureImage is
  armed.
- **Region selection:** the selection can be drawn, moved and resized with the keyboard. It works
  on every monitor, even while a full-screen game keeps the keyboard focus.
- **Hotkey recorder:** you can record a new hotkey from the keyboard. Tab and Shift+Tab leave the
  recorder without changing the hotkey.
- **Tray icon:** it can be reached with Win+B. Starting CaptureImage again from the Start menu also
  brings its window back.

### Screen readers

- Buttons, toggles, lists and text fields have names. This includes buttons that show only an
  icon, and each hotkey row's Clear and Reset buttons, which name their capture mode.
- **Status changes are read as they happen:**
  - armed or disarmed
  - the update status
  - hotkey problems
  - an invalid Steam App ID
- **Notifications are read** through UI Automation notification events. This works both when the
  main window is in front and when CaptureImage is in the tray, because the pop-up sends the event
  too. Errors are marked as important.
- **List items are read with the details shown next to them:**
  - Windows: title, process and the Steam, profile, minimized and locked badges.
  - Captures: file name, game, date and HDR.
  - Profiles: what the profile matches, and the auto-arm and disabled badges.
- **Headings:** page titles are level 1, sections are level 2 and the disclaimer headings on the
  About page are level 3. In Narrator's scan mode (Caps Lock+Space), H jumps between them.
- The region overlay is named "Select an area", and its help text lists the keys. The size of the
  selection is read when you change it with the keyboard.

### Vision

- **Themes:**
  - Light, dark and high contrast are available in **Settings → Appearance**.
  - **System** follows Windows, including its contrast themes.
  - The region overlay draws its selection and labels in the contrast theme's colours.
- **Colour is backed by text:**
  - The armed state is also shown as text on the Dashboard, in the tray icon's tooltip and in the
    tray menu.
  - Log levels and error messages are shown as words.
- The app is per-monitor DPI aware, so it stays sharp at any display scaling and across monitors
  with different scaling.

### Motion and timing

- Page transitions are turned off when **Settings → Accessibility → Visual effects → Animation
  effects** is off in Windows.
- Notification pop-ups stay up at least as long as **Dismiss notifications after this amount of
  time** (same page in Windows Settings). Pointing at a pop-up keeps it open.
- Captures never depend on a time limit. The optional countdown (3, 5 or 10 seconds) is a feature
  you choose, and Esc cancels a region selection at any time.

### Language

- English, Tiếng Việt, العربية, 日本語, 简体中文, 한국어, Español, Français, Deutsch, Português (Brasil)
  and Русский.
- The language follows Windows on first run and can be changed in **Settings**.

## Keyboard reference

| Where | Keys | What they do |
|---|---|---|
| Anywhere, while armed | `Ctrl+Shift+F12` | Capture the window in front (or the locked window) |
| Anywhere, while armed | `Ctrl+Shift+F11` | Select a region to capture |
| Anywhere, while armed | `Ctrl+Shift+F10` | Capture the screen under the mouse pointer |
| Region selection | Arrow keys | Move the selection (the first press selects a centred area half the screen's width and height) |
| Region selection | `Shift` + arrow keys | Make the selection wider, narrower, taller or shorter |
| Region selection | `Ctrl` (with the above) | Move or resize by a single pixel |
| Region selection | `Tab` / `Shift+Tab` | Continue on the next / previous monitor |
| Region selection | `Enter` | Capture the selection, or the whole screen if nothing is selected |
| Region selection | `Esc` | Cancel |
| Preview | `Enter` / `Esc` / `Ctrl+C` | Save / discard / copy the capture |
| Settings → Hotkeys | `Enter` or `Space` on a hotkey | Start recording, then press the new combination |
| Settings → Hotkeys, recording | `Esc` / `Backspace` / `Tab` | Cancel / clear the hotkey / leave without changing it |
| Notification area | `Win+B`, arrow keys, then `Enter` | Show the CaptureImage window |
| Notification area | `Win+B`, arrow keys, then `Shift+F10` | Open the tray menu (capture, arm, settings, exit) |

The three capture hotkeys can be changed in **Settings → Hotkeys**. While the region overlay is up,
the arrow keys, Tab, Enter and Esc go to CaptureImage, and other apps get them back as soon as the
overlay closes.

## Supported environments

| Area | Status |
|---|---|
| Windows | Windows 11 22H2 or later, 64-bit (the same as the app) |
| Keyboard only | Supported and tested |
| Narrator | Supported and tested |
| NVDA, JAWS | Should work, because the app uses UI Automation; **not tested** |
| Magnifier | Should work; not tested |
| Voice access | Not tested |
| Contrast themes | Supported (Theme: System or High contrast) |
| Display scaling | Supported per monitor, including mixed scaling |
| Windows "Text size" | **Not supported** (see [known limitations](#known-limitations)) |

## Testing record

| Date | Version | Environment | Method | Result |
|---|---|---|---|---|
| 2026-11-09 | 2.0.3 | Windows 11 (Insider build 29648) | Maintainer: keyboard only with the mouse disconnected, and Narrator, across the main window's pages | No blocking problems found |
| 2026-11-09 | 2.1.0 | Windows 11 (Insider build 29648), real hardware | Maintainer: keyboard only and Narrator. Covered the new keyboard region selection with its size announcements; notifications with the window in front and in the tray; and the tray icon (Win+B, then Enter and Shift+F10) | All passed |

The changes in 2.1.0 are listed in the [changelog](CHANGELOG.md). Besides the manual test above,
unit tests and the UI tests cover them.

On every pull request, CI checks that:

- every page opens and has its automation id;
- the hotkey buttons have distinct names that include the current binding;
- Tab leaves the hotkey recorder without saving a hotkey;
- arming the hotkeys raises a live-region announcement.

## Known limitations

| Limitation | Workaround |
|---|---|
| The app doesn't follow **Settings → Accessibility → Text size** (a WPF limitation). | Increase **Settings → System → Display → Scale**, or use Magnifier (`Win` + `+`). |
| Pinned thumbnails work only with a mouse. They never take the focus, so they can't interrupt a game. | The **Library** offers the same actions from the keyboard: open, copy, show in folder. |
| Notifications close on their own. | Everything they report is also in the **Library** (captures) and on the **Logs** page. Windows' notification-time setting makes them stay longer. |
| A capture has no text description. | **Extract text** (Preview or Library) copies the text inside a capture to the clipboard. |
| While armed, a capture hotkey doesn't reach other apps. A clash with a shortcut of your assistive technology is possible. | Change the hotkey in **Settings → Hotkeys**, or disarm. The keys are only held while armed. |
| Translations are machine-assisted, so wording or pronunciation may be off. | Please report it (see below); corrections from native speakers are welcome. |
| Screen readers other than Narrator haven't been tested. | Reports from NVDA and JAWS users are especially welcome. |

## Contributor expectations

Accessibility is part of "done" for every change to the user interface.

**Testing.** Before opening a pull request that changes the UI:

- Use every changed screen with the keyboard alone, with a visible focus at every step and no
  place where the focus gets stuck.
- Check it once with Narrator (`Ctrl+Win+Enter`). Every control should be read with a name, a role
  and a state, and changes should be announced.
- Check it in a Windows contrast theme and at 200 % display scaling.
- Optionally run [Accessibility Insights for Windows](https://accessibilityinsights.io/docs/windows/overview/)
  FastPass on the changed window.

**User interface (XAML).**

- Give every control that shows only an icon an `AutomationProperties.Name`. Names must be unique
  within a page, so add the context (the mode, the card) when the same action appears twice.
- Give list items an accessible name that includes the badges shown next to them (see
  `TargetItemViewModel.AutomationName`).
- Use the `PageTitle` and `SectionHeader` styles (which set heading levels) for headings, and
  `AutomationProperties.HeadingLevel` for any other heading.
- Text that changes to report a status needs `AutomationProperties.LiveSetting`,
  `a11y:LiveRegion.IsEnabled="True"` and a binding with `NotifyOnTargetUpdated=True`. WPF doesn't
  announce live regions on its own.
- Use theme brushes (`{DynamicResource …}`), not hard-coded colours. Never use colour alone to show
  a state.
- New animations must respect `SystemParameters.ClientAreaAnimation`. Notifications and pop-ups
  must not take the focus.
- Every new string goes into all 11 language files, including names and help text read only by
  screen readers.

**Documentation.**

- Use headings in order, without skipping a level.
- Write link text that says where the link goes.
- Give images alt text that describes them, for example the README screenshots.
- Keep tables for tabular data, and give diagrams (such as the Mermaid diagrams in
  `docs/ARCHITECTURE.md`) a text explanation next to them.

**CI.** There is no automated accessibility scanner for WPF in CI. The `ui-tests` job checks
automation ids, names and announcements through UI Automation. Reviews use the accessibility
checklist in the pull request template.

## Reporting accessibility issues

If something in CaptureImage is hard or impossible to use with your setup, please tell us:

- Open an [accessibility issue](https://github.com/poli0981/captureimg/issues/new?template=accessibility.yml).
- Or, if you'd rather not use GitHub, email `contact@poli0981.dev`.

It helps to include:

- what you were trying to do, what you expected and what happened;
- your assistive technology and its version (or "keyboard only", "contrast theme", …);
- your Windows version, the CaptureImage version (shown on the About page) and the app's
  language.

Screenshots and recordings are optional. You never need to tell us about a disability or any
other personal information.

### Severity

The maintainer assigns a severity when triaging a report:

| Severity | Meaning |
|---|---|
| Critical | Blocks a core task with no workaround: capturing, arming or disarming, changing hotkeys, finding your captures, updating |
| Serious | A core task is possible only with great difficulty or a workaround |
| Moderate | Something is slower, confusing or inconsistent, but works |
| Minor | A small issue with little effect on use |

### How we respond

CaptureImage is maintained by one volunteer, so there are **no fixed response or fix times**. That
is the same as for other reports (see [`SUPPORT.md`](SUPPORT.md)). What you can expect:

- Every report is read and labelled `accessibility`.
- Questions are asked respectfully, without doubting your experience.
- Critical and Serious barriers are fixed before cosmetic work, usually in the next release.
- A workaround is suggested when one exists.
- The fix is listed in the [changelog](CHANGELOG.md). If you're willing, we'll ask you to confirm
  that it works for you.

## Ownership

The maintainer, [poli0981](https://github.com/poli0981), is responsible for accessibility. That
means:

- triaging accessibility reports and keeping their issues up to date;
- running the keyboard and Narrator check before releases that change the user interface;
- keeping this document current: the features, the known limitations and the testing record.

## Feedback

Suggestions for this statement or for our practices are welcome in
[GitHub Discussions](https://github.com/poli0981/captureimg/discussions). An actual barrier is best
reported as described [above](#reporting-accessibility-issues).

This statement is based on the template of the
[Open Source Accessibility toolkit](https://github.com/open-source-accessibility/accessibility-toolkit)
and the [Open Source Guide on accessibility](https://opensource.guide/accessibility-best-practices-for-your-project/).

_Last reviewed: 2026-11-09, for CaptureImage 2.1.0._
