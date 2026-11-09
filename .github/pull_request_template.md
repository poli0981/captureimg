<!-- markdownlint-disable-file MD041 -->
<!-- Thanks for contributing to CaptureImage! The PR title must follow Conventional Commits,
     e.g. "fix(capture): keep alpha opaque" — it becomes the squash-commit subject. -->

## Summary

<!-- What does this change and why? -->

## Related issues

<!-- Fixes #123 / Closes #456 / Refs #789 -->

## Testing

<!-- How did you verify it? For UI changes, add screenshots
     (`CaptureImage.App.exe --screenshots <dir> --data-dir <temp>` renders every page). -->

- [ ] `dotnet build CaptureImage.sln -c Release` passes (warnings are errors)
- [ ] `dotnet test --solution CaptureImage.sln -c Release` passes
- [ ] New or changed behavior is covered by tests
- [ ] Tried it in the app (describe what you checked)

## Accessibility (UI changes)

<!-- See ACCESSIBILITY.md → "Contributor expectations". Delete this section if no UI changed. -->

- [ ] Everything changed works with the keyboard alone, with a visible focus and no focus trap
- [ ] Controls have a unique accessible name (icon-only buttons too) and status text is announced
- [ ] Colour is not the only signal; theme brushes are used, and it looks right in a contrast theme
- [ ] New animations respect Windows' "Animation effects"; nothing takes the focus unasked
- [ ] Checked once with Narrator

## Checklist

- [ ] I read [`CONTRIBUTING.md`](../CONTRIBUTING.md) and license this contribution under GPL-3.0-or-later.
- [ ] User-visible text is in all 11 `Strings*.resx` files (or I asked for help translating).
- [ ] `CHANGELOG.md` has an entry under `[Unreleased]` for user-visible changes.
- [ ] No secrets, credentials or personal screenshots are included.
- [ ] AI assistance, if any, is mentioned below.

## AI assistance

<!-- e.g. "Drafted with Claude Code, reviewed and tested by me", or "None". -->
