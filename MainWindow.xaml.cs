using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SwiftSandbox;

public partial class MainWindow : Window
{
    private sealed class ScriptTab(string title, string text, string? path = null)
    {
        public string Title { get; set; } = title;
        public string Text { get; set; } = text;
        public string? Path { get; set; } = path;
        public bool Dirty { get; set; }
    }

    private static readonly (string Title, string Text)[] Samples =
    [
        ("Hello World", "-- Welcome to Swift Sandbox\nprint(\"Hello from the local demo!\")\nwarn(\"This does not connect to Roblox.\")"),
        ("Counter", "-- Count from one to five\nfor i = 1, 5 do\n    print(\"Count: \" .. i)\nend"),
        ("Math Demo", "-- A few arithmetic expressions\nlocal coins = 12\nlocal bonus = 8\nprint(\"Total coins: \" .. (coins + bonus))\nprint(\"Half: \" .. (coins + bonus) / 2)"),
        ("Variables", "-- Variables and string concatenation\nlocal player = \"Explorer\"\nlocal level = 3\nprint(\"Welcome, \" .. player)\nprint(\"Demo level: \" .. level)")
    ];
    private readonly List<ScriptTab> tabs = [];
    private ScriptTab? active;
    private bool changing;
    private readonly string scriptsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwiftSandbox", "Scripts");
    private static readonly Regex Tokens = new(@"(--[^\n]*|\""(?:\\.|[^\""\\])*\""|'(?:\\.|[^'\\])*'|\b(?:local|for|do|end|if|then|else|function|return|while)\b|\b(?:print|warn)\b|\b\d+(?:\.\d+)?\b)", RegexOptions.Compiled);
    private static readonly Brush Plain = Brush("#E8E8F2"), Comment = Brush("#7E829A"), StringColor = Brush("#B5D88B"), Keyword = Brush("#BC8FF5"), Function = Brush("#75BFFF"), NumberColor = Brush("#E7B777");

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(scriptsDir);
        BuiltInList.ItemsSource = Samples.Select(s => "✦  " + s.Title).ToList();
        RefreshSaved();
        AddTab("Untitled", "-- Write a Luau-style demo script here\nprint(\"Ready to explore!\")");
        OutputBox.Text = "Local demo ready. Supported: print, warn, variables, arithmetic, string joins, and simple for loops.";
        Editor.Focus();
    }

    private static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    private static string GetEditorText(RichTextBox editor)
    {
        string text = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Replace("\r\n", "\n");
        return text.EndsWith('\n') ? text[..^1] : text;
    }

    private void AddTab(string title, string text, string? path = null)
    {
        var existing = path is null ? null : tabs.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { Activate(existing); return; }
        var tab = new ScriptTab(title, text, path);
        tabs.Add(tab);
        Activate(tab);
    }

    private void Activate(ScriptTab tab)
    {
        if (active is not null) active.Text = GetEditorText(Editor);
        active = tab;
        changing = true;
        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(new Paragraph(new Run(tab.Text)) { Margin = new Thickness(0) });
        Highlight();
        changing = false;
        EditorTitle.Text = tab.Title.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) ? tab.Title : tab.Title + ".luau";
        StatusText.Text = tab.Path is null ? "Unsaved script" : "Saved script";
        RenderTabs();
        Editor.Focus();
    }

    private void RenderTabs()
    {
        TabsPanel.Children.Clear();
        foreach (var tab in tabs.ToArray())
        {
            var outer = new Border { Background = tab == active ? Brush("#252338") : Brush("#191A25"), BorderBrush = tab == active ? Brush("#9E72F5") : Brush("#2A2C3A"), BorderThickness = new Thickness(0, 0, 0, tab == active ? 2 : 1), CornerRadius = new CornerRadius(7, 7, 0, 0), Margin = new Thickness(0, 0, 4, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var label = new Button { Content = "⌘  " + tab.Title + (tab.Dirty ? " •" : ""), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12, 7, 5, 7), MinWidth = 90 };
            label.Click += (_, _) => Activate(tab);
            var close = new Button { Content = "×", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(5, 7, 10, 7), Foreground = Brush("#9193A7") };
            close.Click += (_, _) => CloseTab(tab);
            row.Children.Add(label); row.Children.Add(close); outer.Child = row; TabsPanel.Children.Add(outer);
        }
    }

    private void CloseTab(ScriptTab tab)
    {
        if (tab == active) tab.Text = GetEditorText(Editor);
        if (tab.Dirty && MessageBox.Show($"Close {tab.Title} without saving changes?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int index = tabs.IndexOf(tab);
        tabs.Remove(tab);
        if (tabs.Count == 0) { active = null; AddTab("Untitled", ""); }
        else if (active == tab) { active = null; Activate(tabs[Math.Min(index, tabs.Count - 1)]); }
        else RenderTabs();
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (changing || active is null) return;
        active.Text = GetEditorText(Editor);
        active.Dirty = true;
        Highlight();
        RenderTabs();
        StatusText.Text = "Modified";
    }

    private void Highlight()
    {
        if (active is null) return;
        string source = active.Text;
        int caretOffset = new TextRange(Editor.Document.ContentStart, Editor.CaretPosition).Text.Replace("\r\n", "\n").Length;
        caretOffset = Math.Clamp(caretOffset, 0, source.Length);
        changing = true;
        var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 22 };
        int cursor = 0;
        foreach (Match match in Tokens.Matches(source))
        {
            if (match.Index > cursor) paragraph.Inlines.Add(new Run(source[cursor..match.Index]) { Foreground = Plain });
            string token = match.Value;
            Brush color = token.StartsWith("--") ? Comment : token.StartsWith('"') || token.StartsWith('\'') ? StringColor : char.IsDigit(token[0]) ? NumberColor : token is "print" or "warn" ? Function : Keyword;
            paragraph.Inlines.Add(new Run(token) { Foreground = color });
            cursor = match.Index + match.Length;
        }
        if (cursor < source.Length) paragraph.Inlines.Add(new Run(source[cursor..]) { Foreground = Plain });
        if (source.Length == 0) paragraph.Inlines.Add(new Run(""));
        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(paragraph);
        Editor.CaretPosition = PositionAt(caretOffset);
        changing = false;
    }

    private TextPointer PositionAt(int chars)
    {
        TextPointer pointer = Editor.Document.ContentStart;
        while (pointer.CompareTo(Editor.Document.ContentEnd) < 0)
        {
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                string run = pointer.GetTextInRun(LogicalDirection.Forward);
                if (chars <= run.Length) return pointer.GetPositionAtOffset(chars) ?? pointer;
                chars -= run.Length;
                pointer = pointer.GetPositionAtOffset(run.Length) ?? pointer;
            }
            else pointer = pointer.GetNextContextPosition(LogicalDirection.Forward) ?? Editor.Document.ContentEnd;
        }
        return Editor.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
    }

    private void RefreshSaved()
    {
        SavedList.ItemsSource = Directory.EnumerateFiles(scriptsDir, "*.luau").OrderBy(System.IO.Path.GetFileName).Select(System.IO.Path.GetFileName).ToList();
    }
    private void NewButton_Click(object sender, RoutedEventArgs e) => AddTab("Untitled " + (tabs.Count + 1), "");
    private void BuiltInList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BuiltInList.SelectedIndex < 0) return;
        var sample = Samples[BuiltInList.SelectedIndex];
        AddTab(sample.Title, sample.Text);
        BuiltInList.SelectedIndex = -1;
    }
    private void SavedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedList.SelectedItem is not string name) return;
        string path = System.IO.Path.Combine(scriptsDir, name);
        try { AddTab(System.IO.Path.GetFileNameWithoutExtension(path), File.ReadAllText(path), path); }
        catch (IOException ex) { MessageBox.Show(ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error); }
        SavedList.SelectedIndex = -1;
    }
    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (active is null) return;
        active.Text = GetEditorText(Editor);
        OutputBox.Text = SandboxRunner.Run(active.Text);
        StatusText.Text = OutputBox.Text.StartsWith("Error:") ? "Demo error" : "Demo completed";
    }
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (active is null) return;
        Editor.SelectAll(); Editor.Selection.Text = "";
        Editor.Focus();
    }
    private void ClearOutputButton_Click(object sender, RoutedEventArgs e) => OutputBox.Clear();
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (active is null) return;
        active.Text = GetEditorText(Editor);
        string? path = active.Path;
        if (path is null)
        {
            var dialog = new SaveFileDialog { Title = "Save Luau script", Filter = "Luau scripts (*.luau)|*.luau|All files (*.*)|*.*", DefaultExt = ".luau", InitialDirectory = scriptsDir, FileName = active.Title + ".luau", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;
        }
        try
        {
            File.WriteAllText(path, active.Text);
            active.Path = path; active.Title = System.IO.Path.GetFileNameWithoutExtension(path); active.Dirty = false;
            EditorTitle.Text = active.Title + ".luau"; RenderTabs(); RefreshSaved(); StatusText.Text = "Saved";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Open Luau script", Filter = "Luau scripts (*.luau;*.lua)|*.luau;*.lua|All files (*.*)|*.*", InitialDirectory = scriptsDir };
        if (dialog.ShowDialog(this) != true) return;
        try { AddTab(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), File.ReadAllText(dialog.FileName), dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (active is not null) active.Text = GetEditorText(Editor);
        if (tabs.Any(t => t.Dirty) && MessageBox.Show("Close Swift Sandbox with unsaved changes?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
        base.OnClosing(e);
    }
}

