# Contributing to CaptureImage

Thanks for wanting to help! Bug reports, translations, documentation fixes and code are all welcome.
Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to help

- **Report a bug or suggest a feature** with the issue templates (see [`SUPPORT.md`](SUPPORT.md)).
- **Proofread a translation.** The user interface is translated into 11 languages, mostly with AI
  assistance. Strings live in `src/CaptureImage.ViewModels/Resources/Strings/Strings.<language>.resx`
  (English is `Strings.resx`). Keep every `{0}`-style placeholder and the names in `{Game}`-style
  file-name tokens unchanged.
- **Fix a bug or build a feature.** For anything larger than a small fix, please open an issue or a
  discussion first so we can agree on the approach.

## Development setup

- Windows 11 22H2 or later, 64-bit.
- The .NET 10 SDK version pinned in [`global.json`](global.json).
- Any editor; JetBrains Rider and Visual Studio 2026 both work.

```bash
dotnet build CaptureImage.sln -c Release
dotnet test --solution CaptureImage.sln -c Release
dotnet run --project src/CaptureImage.App -c Release -- --data-dir ./.devdata --instance-id dev
```

Use `--data-dir` and `--instance-id` so a development copy never touches your real settings or a
copy you have installed. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) explains how the projects fit
together.

End-to-end UI tests move the mouse and type, so they only run with `CAPTUREIMAGE_UI_TESTS=1`:

```bash
CAPTUREIMAGE_UI_TESTS=1 dotnet test --project tests/CaptureImage.App.UiTests -c Release
```

## Pull requests

Every pull request must pass the required checks before it can be merged (squash merge only):

- **build** — warnings are errors, the recommended .NET analyzers are on for `src/`, all tests pass.
- **format** — `dotnet format whitespace` and the style rules below.
- **deadcode** — JetBrains InspectCode finds no unused or redundant code.
- **audit** / **license** — no vulnerable packages; shipped dependencies use a GPL-compatible
  license.
- **publish-smoke** — the published app starts and opens every page.
- **ui-tests** — end-to-end tests drive the built app through UI Automation (FlaUI).
- **pr-title** — the title follows [Conventional Commits](https://www.conventionalcommits.org/), e.g.
  `fix(capture): keep alpha opaque` or `feat(ui)!: …` for breaking changes.
- **dependency-review**, **workflow-lint**, **docs**.

Conventions:

- Every C# file starts with the SPDX header:

  ```csharp
  // SPDX-License-Identifier: GPL-3.0-or-later
  // Copyright (c) CaptureImage Contributors
  ```

- File-scoped namespaces, `using` directives outside the namespace, no unused usings.
- Logging uses source-generated `[LoggerMessage]` methods.
- Every user-visible string is a resource key with a translation in all 11 `.resx` files (a test
  checks this). Add the English text and, if you can't translate, ask in the pull request.
- Packages are managed centrally in `Directory.Packages.props` with lock files; after changing a
  dependency run `dotnet restore --force-evaluate` and commit the updated `packages.lock.json` files.
- Commits must be signed (GPG or SSH) — the `main` branch requires verified signatures.

Before pushing, the quick local check is:

```bash
dotnet build CaptureImage.sln -c Release
dotnet test --solution CaptureImage.sln -c Release
dotnet format whitespace CaptureImage.sln --verify-no-changes
dotnet format style CaptureImage.sln --verify-no-changes --diagnostics IDE0073 IDE0161 IDE0065 IDE0005
```

## AI-assisted contributions

AI tools are fine to use. Please mention it in the pull request, review everything you submit as if
you wrote it, and make sure it builds and passes the tests. Commits with meaningful AI authorship
should carry a `Co-Authored-By` trailer naming the model.

## License

By contributing you agree that your work is released under the
[GPL-3.0-or-later](LICENSE) license, and that you have the right to license it. Contributors are
listed in [`docs/CONTRIBUTORS.md`](docs/CONTRIBUTORS.md).

## Contact

Questions about contributing: `code@poli0981.dev`, or open a discussion.
