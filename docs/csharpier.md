# CSharpier – Code Formatter

This repository uses [CSharpier](https://csharpier.com/) to enforce consistent C# code formatting.

## Quick start

```bash
# Restore local tools (includes CSharpier)
dotnet tool restore

# Format all files
dotnet csharpier format .

# Check formatting without modifying files
dotnet csharpier check .
```

## Configuration

| File | Purpose |
|---|---|
| `.csharpierrc.yaml` | Formatting options (tabs, print width, …) |
| `.csharpierignore` | Files/folders excluded from formatting |
| `.config/dotnet-tools.json` | Pinned CSharpier CLI version |

Current settings (`.csharpierrc.yaml`):

- **Tabs** for indentation (`useTabs: true`)
- **Print width** 120 characters
- **Indent size** 4

## MSBuild integration

Every project in the solution references the `CSharpier.MsBuild` NuGet package
via `Directory.Build.props`. This means:

- **Debug build** – automatically formats files on build.
- **Release build** – runs `csharpier --check` and fails the build if any file
  is not formatted.

## Editor integration

### Visual Studio

The CSharpier extension is listed in `.vsconfig` and will be suggested when
opening the solution. Install it from **Extensions → Manage Extensions** or
accept the prompt.

### Visual Studio Code

Recommended extensions are defined in `.vscode/extensions.json`. Open the
Extensions sidebar and install **CSharpier - Code Formatter**
(`csharpier.csharpier-vscode`).

## CI

The GitHub Actions CI workflow (`.github/workflows/ci.yml`) includes a
`formatting` job that runs `dotnet csharpier check .` on every push to `main`.

## Ignoring code

- Add paths to `.csharpierignore` (gitignore syntax).
- Use `// csharpier-ignore` comment to skip the next statement/member.
- Use `// csharpier-ignore-start` / `// csharpier-ignore-end` for larger blocks.

See <https://csharpier.com/docs/Ignore> for details.
