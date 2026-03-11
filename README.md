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
| **File tree** | Recursive directory browser with expand/collapse |
| **Two editor modes** | Browse (read) and Edit — toggle with Enter / Esc |
| **Command prompt** | Press `` ` `` for commands: open, edit, save, go-to, tree |
| **Keyboard shortcuts** | Navigation, delete line, tree toggle via arrow keys |

## Requirements

- macOS with a VT-100 capable terminal
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) *(only needed to build from source)*

## Install

### Homebrew (macOS)

```bash
brew tap serhii-deryhlazov/mide
brew install mide
```

### Manual (macOS)

**Apple Silicon (arm64)**
```bash
curl -L https://github.com/serhii-deryhlazov/mide/releases/download/v1.0.0/mide-osx-arm64.tar.gz | tar xz
chmod +x mide && mv mide /usr/local/bin/
```

**Intel (x64)**
```bash
curl -L https://github.com/serhii-deryhlazov/mide/releases/download/v1.0.0/mide-osx-x64.tar.gz | tar xz
chmod +x mide && mv mide /usr/local/bin/
```

No .NET runtime required — fully self-contained binary.

## Build & run from source

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

## Editor modes

mide has two modes:

| Mode | How to enter | Behaviour |
|---|---|---|
| **Browse** | Esc, or on file open | Read-only, scroll with ↑ ↓ PgUp PgDn Home End |
| **Edit** | Enter, or any printable key | Full editing, cursor visible, syntax highlight active |

## Commands

Press `` ` `` (backtick) to open the command prompt, then type:

### File commands

| Command | Alias | Description |
|---|---|---|
| `open` | `o` | Open file via picker (browse mode) |
| `open <path>` | `o <path>` | Open a specific file (browse mode) |
| `edit` | `e` | Open file via picker (edit mode) |
| `edit <path>` | `e <path>` | Open a specific file (edit mode) |
| `new <name>` | `n <name>` | Create and open a new file (edit mode) |
| `save` | `s` | Save current file *(edit mode only)* |

### Navigation commands *(edit mode only)*

| Command | Description |
|---|---|
| `:100` | Go to line 100, column 1 |
| `:80:40` | Go to line 80, column 40 |
| `:40:e` | Go to line 40, end of line |

### View commands

| Command | Alias | Description |
|---|---|---|
| `tree` | `t`, `toggle` | Toggle the file tree panel |

Press `` ` `` again to dismiss the prompt without running a command.

## Keyboard shortcuts

### Always available

| Key | Action |
|---|---|
| `` ` `` | Open command prompt |

### Browse mode (tree hidden)

| Key | Action |
|---|---|
| ↑ ↓ PgUp PgDn Home End | Scroll through file |
| Enter or any printable key | Switch to edit mode |
| ← | Open file tree |

### Edit mode (tree hidden)

| Key | Action |
|---|---|
| Esc | Switch to browse mode |
| Ctrl+D | Delete current line |

### File tree open

| Key | Action |
|---|---|
| ↑ ↓ | Move selection |
| ← | Expand / collapse folder |
| Enter | Open selected file (edit mode) |
| `d` | Delete selected file (with confirmation) |
| → | Close tree |

## Startup behavior

- Opens `README.md` from the working directory by default (if present).
- File tree starts hidden; toggle it with `` ` `` → `tree` or ← arrow in browse mode.

## Project structure

```
mide/
├── Program.cs                  # Entry point, IDE window, shared state
├── README.md
├── mide.csproj
├── global.json                 # Pins SDK to 9.0.311
├── Settings/
│   └── default.config.json     # All tuneable defaults
├── Core/
│   ├── Config.cs               # Typed config loader
│   └── WelcomeText.cs          # Welcome screen content
├── UI/
│   ├── CommandHandling.cs      # Backtick prompt, key routing, command execution
│   ├── Dialogs.cs              # Confirm-delete, find, go-to-line dialogs
│   └── FocusHelpers.cs         # SetEditorMode — single source of truth for mode transitions
└── Editor/
    ├── FileOperations.cs       # Open, save, new file logic
    ├── SyntaxHighlighter.cs    # Multi-language syntax highlighting
    └── TreeHelpers.cs          # File tree population, expand state, navigation
```

## Dependencies

- [SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI) v2.4.36 — TUI framework
- [Spectre.Console](https://spectreconsole.net/) v0.54.0 — Color/markup (transitive)
