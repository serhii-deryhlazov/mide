```
  ███╗   ███╗██╗██████╗ ███████╗
  ████╗ ████║██║██╔══██╗██╔════╝
  ██╔████╔██║██║██║  ██║█████╗
  ██║╚██╔╝██║██║██║  ██║██╔══╝
  ██║ ╚═╝ ██║██║██████╔╝███████╗
  ╚═╝     ╚═╝╚═╝╚═════╝ ╚══════╝
```

# mide — terminal IDE

Terminal IDE built with [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx) and .NET 9.

## Features

| Feature | Details |
|---|---|
| **Multi-language syntax highlighting** | C#, Python, JavaScript/TypeScript, JSON, Markdown |
| **File tree** | Recursive directory browser with depth-4 expansion |
| **Line numbers** | Toggle on/off |
| **Keyboard shortcuts** | Full set (see below) |
| **Command prompt** | Press backtick (\`) for quick commands: `tree`, `open <path>`, `new <file>` |

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- macOS, Linux, or Windows with a VT-100 capable terminal

## Build & run

```bash
# Build
dotnet build

# Run from project root (uses pre-built binary)
./mide

# Or run via dotnet (opens mide in the current directory)
dotnet run

# Open a specific directory
./mide /path/to/project
```

## Commands

Press `` ` `` (backtick) to open the command prompt, then type:

| Command | Alias | Description |
|---|---|---|
| `tree` | `t`, `toggle` | Toggle the file tree panel |
| `open` | `o` | Open file via dialog (browse mode) |
| `open <path>` | `o <path>` | Open a specific file (browse mode) |
| `edit` | `e` | Open file via dialog (edit mode) |
| `edit <path>` | `e <path>` | Open a specific file (edit mode) |
| `new <name>` | `n <name>` | Create and open a new file |
| `save` | `s` | Save current file |

Press `` ` `` again to dismiss the prompt without running a command.

## Startup behavior

- Opens `README.md` from the working directory by default (if present).
- File tree starts hidden; toggle it with `Ctrl+B` or the `tree` command.

## Project structure

```
mide/
├── Program.cs            # Main window, layout, IDE state
├── CommandHandling.cs    # Backtick command prompt & command execution
├── Dialogs.cs            # Find, Go-to-line, and other dialogs
├── FileOperations.cs     # Open, save, new file logic
├── FocusHelpers.cs       # Focus management between tree and editor
├── TreeHelpers.cs        # File tree population and navigation
├── SyntaxHighlighter.cs  # Multi-language syntax highlighting
├── WelcomeText.cs        # Welcome screen content
├── mide.csproj           # .NET 9 project file
├── global.json           # Pins SDK to 9.0.311
└── README.md
```

## Dependencies

- [SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI) v2.4.36 — TUI framework
- [Spectre.Console](https://spectreconsole.net/) v0.54.0 — Color/markup (transitive)


