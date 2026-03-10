namespace mide;

partial class Program
{
    // ── Welcome screen ─────────────────────────────────────────────────────
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
        "//  Commands (press ` to open bar):\n" +
        "//    tree | t                 Toggle file tree\n" +
        "//    open | o <path>          Open file (picker if no path)\n" +
        "//    edit | e <path>          Open + enter edit mode (picker if no path)\n" +
        "//    new  | n <path>          Create new file and start editing\n" +
        "//    save | s                 Save current file\n" +
        "//\n" +
        "//  Usage tips:\n" +
        "//    Enter on the tree opens the file; Esc exits edit mode.\n" +
        "//\n" +
        "//  Supported syntax highlighting:\n" +
        "//    C# (.cs)\n" +
        "//    Python (.py)\n" +
        "//    JavaScript / TypeScript (.js/.ts/.tsx)\n" +
        "//    JSON (.json)\n" +
        "//    Markdown (.md/.markdown)\n" +
        "//    Plain text (fallback)\n";
}
