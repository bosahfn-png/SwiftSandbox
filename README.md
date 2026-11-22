# Swift Sandbox

A Windows Luau-style script editor with a dark, executor-inspired interface. It includes tabs, syntax highlighting, built-in examples, a saved-script sidebar, and a **local demo runner**. It does not attach to Roblox or any other process.

## Download and run

On Windows 10 or 11 (x64), download `SwiftSandbox.exe` from the [Releases](../../releases) page and double-click it. This is a self-contained build; no separate .NET installation is needed. If Windows displays a SmartScreen prompt for the unsigned app, review the publisher information and choose whether to run it. Source code is available in this repository.

For a local build, the executable is created at `release/SwiftSandbox.exe`. The release workflow attaches it to the matching GitHub release.

## Using the app

- Choose a built-in script in the left sidebar or click **+** for a blank tab.
- **Execute** runs the current text in the local demo and shows output below the editor.
- **Clear** clears the editor. **Clear output** clears only the output panel.
- **Save** stores a `.luau` file; the default folder is `%LOCALAPPDATA%\SwiftSandbox\Scripts`. Scripts saved there appear in the sidebar.
- **Open** loads a `.luau` or `.lua` file into a tab.
- Closing a modified tab or the app asks before discarding changes.

## Demo language

The runner supports `print(...)`, `warn(...)`, `local` variables and assignments, numbers, quoted strings, arithmetic (`+`, `-`, `*`, `/`), string joining with `..`, parentheses, and simple integer `for i = start, end do ... end` loops. It is intentionally a small Luau-style subset, **not a full Luau interpreter**. Unsupported statements return an error. Execution has limits on script size, statements, loop range, and output. Script text is never sent to Roblox, a server, or a shell.

## Build from source

Install the .NET 10 SDK on Windows, then run:

```powershell
dotnet build SwiftSandbox.csproj -c Release
dotnet run --project tests/SwiftSandbox.Tests.csproj
dotnet publish SwiftSandbox.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o release
```

The final executable is `release/SwiftSandbox.exe`. The [release workflow](.github/workflows/release.yml) builds and attaches it to every `v*` tag.

## Scope

This project is a standalone coding demo. It contains no Roblox injection, game cheats, anti-cheat bypass, or third-party game integration.

## License

MIT. See [LICENSE](LICENSE).

