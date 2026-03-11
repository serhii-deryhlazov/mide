namespace mide;

partial class Program
{
    const string Welcome =
        "//  ███╗   ███╗██╗██████╗ ███████╗\n" +
        "//  ████╗ ████║██║██╔══██╗██╔════╝\n" +
        "//  ██╔████╔██║██║██║  ██║█████╗\n" +
        "//  ██║╚██╔╝██║██║██║  ██║██╔══╝\n" +
        "//  ██║ ╚═╝ ██║██║██████╔╝███████╗\n" +
        "//  ╚═╝     ╚═╝╚═════╝ ╚══════╝\n" +
        "//\n" +
        "//  A terminal IDE powered by SharpConsoleUI\n" +
        "//\n" +
        "//  Press ` to open the command prompt.\n" +
        "//\n" +
        "//  File commands:\n" +
        "//    open | o <path>          Open file (browse mode)\n" +
        "//    edit | e <path>          Open file (edit mode)\n" +
        "//    new  | n <name>          Create new file\n" +
        "//    save | s                 Save current file  [edit mode only]\n" +
        "//\n" +
        "//  Navigation commands  [edit mode only]:\n" +
        "//    :100                     Go to line 100\n" +
        "//    :80:40                   Go to line 80, column 40\n" +
        "//    :40:e                    Go to line 40, end of line\n" +
        "//\n" +
        "//  View commands:\n" +
        "//    tree | t                 Toggle file tree\n" +
        "//\n" +
        "//  Keyboard shortcuts:\n" +
        "//    Enter / any key          Switch to edit mode\n" +
        "//    Esc                      Switch to browse mode\n" +
        "//    Ctrl+D                   Delete current line  [edit mode]\n" +
        "//    ←  (browse mode)         Open file tree\n" +
        "//    →  (tree open)           Close file tree\n" +
        "//\n" +
        "//  Supported syntax highlighting:\n" +
        "//    C# · Python · JavaScript/TypeScript · JSON · Markdown\n";
}
