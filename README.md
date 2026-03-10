# mide — terminal IDE

A feature-rich terminal IDE built with [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx) and .NET 9.

```
  ███╗   ███╗██╗██████╗ ███████╗
  ████╗ ████║██║██╔══██╗██╔════╝
  ██╔████╔██║██║██║  ██║█████╗
  ██║╚██╔╝██║██║██║  ██║██╔══╝
  ██║ ╚═╝ ██║██║██████╔╝███████╗
  ╚═╝     ╚═╝╚═╝╚═════╝ ╚══════╝
```

## Features

| Feature | Details |
|---|---|
| **Multi-language syntax highlighting** | C#, Python, JavaScript/TypeScript, JSON, Markdown |
| **File tree** | Recursive directory browser with depth-4 expansion |
| **Line numbers** | Toggle on/off |
| **Status bar** | Live filename, line/column, edit mode (INS/OVR/BROWSE), char count, wrap mode |
| **Keyboard shortcuts** | Full set (see below) |
| **Command prompt** | Press backtick (`) for quick commands: `tree`, `open <path>`, `new <file>` |
| **File dialogs** | Open file, open folder, save, save-as |
| **Find in file** | Case-insensitive substring search |
| **Go to line** | Jump to any line number |
| **Themes** | Switch between Classic and ModernGray built-in themes |
| **Word wrap** | Toggle wrap mode |

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- macOS, Linux, or Windows with a VT-100 capable terminal

## Build & run

```bash
# Build
dotnet build

# Run in current directory
dotnet run

# Run with a specific folder open
dotnet run -- /path/to/your/project
```

Or use the published binary:

```bash
dotnet publish -c Release -r osx-arm64 --self-contained
./bin/Release/net9.0/osx-arm64/publish/mide
```

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file |
| `Ctrl+N` | New file |
| `Ctrl+G` | Go to line |
| `Ctrl+F` | Find in file |
| `Ctrl+B` | Toggle file tree |
| `` ` `` (backtick) | Command prompt (tree / open / new) |
| `F1` | About |
| `Ctrl+Q` | Quit |
| `Enter` | Start editing (in editor) |
| `Esc` | Stop editing / browse mode |

## Startup behavior

- Opens `README.md` from the working directory by default (if present).
- File tree starts hidden; toggle it with `Ctrl+B` or the `tree` command.

## Project structure

```
mide/
├── Program.cs            # Main IDE application
├── SyntaxHighlighter.cs  # Multi-language syntax highlighting
├── mide.csproj           # .NET 9 project file
├── global.json           # Pins SDK to 9.0.311
└── README.md
```

## Dependencies

- [SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI) v2.4.36 — TUI framework
- [Spectre.Console](https://spectreconsole.net/) v0.54.0 — Color/markup (transitive)
