using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SwiftSandbox;

// Intentionally small Luau-style demo interpreter. It never loads Roblox, .NET code, or external files.
public static class SandboxRunner
{
    public static string Run(string source)
    {
        if (source.Length > 32_000) return "Error: script exceeds the 32 KB demo limit.";
        try
        {
            string code = StripComments(source);
            if (Regex.IsMatch(MaskStrings(code), @"\bloadstring\s*\(|\bgame\s*[:.]\s*(?:HttpGet|GetService)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return "Error: This script needs Roblox or downloaded code. Swift Sandbox only runs local demo commands and cannot execute scripts in Roblox. Try a built-in sample.";
            var engine = new Engine(code);
            return engine.Execute();
        }
        catch (DemoException ex) { return "Error: " + ex.Message; }
        catch (Exception) { return "Error: the demo runner could not process this script."; }
    }

    private sealed class DemoException(string message) : Exception(message);

    private static string StripComments(string source)
    {
        var cleaned = new StringBuilder(source.Length);
        char quote = '\0';
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quote != '\0')
            {
                cleaned.Append(c);
                if (c == '\\' && i + 1 < source.Length) cleaned.Append(source[++i]);
                else if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"') { quote = c; cleaned.Append(c); }
            else if (c == '-' && i + 1 < source.Length && source[i + 1] == '-')
            {
                if (i + 3 < source.Length && source[i + 2] == '[' && source[i + 3] == '[')
                {
                    int end = source.IndexOf("]]", i + 4, StringComparison.Ordinal);
                    if (end < 0) throw new DemoException("an unfinished multiline comment was found.");
                    for (int j = i; j < end + 2; j++) cleaned.Append(source[j] is '\n' or '\r' ? source[j] : ' ');
                    i = end + 1;
                }
                else
                {
                    while (i < source.Length && source[i] is not ('\n' or '\r')) { cleaned.Append(' '); i++; }
                    i--;
                }
            }
            else cleaned.Append(c);
        }
        return cleaned.ToString();
    }

    private static string MaskStrings(string source)
    {
        var masked = new StringBuilder(source.Length);
        char quote = '\0';
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quote != '\0')
            {
                masked.Append(c is '\n' or '\r' ? c : ' ');
                if (c == '\\' && i + 1 < source.Length) { masked.Append(' '); i++; }
                if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"') { quote = c; masked.Append(' '); }
            else masked.Append(c);
        }
        return masked.ToString();
    }

    private sealed class Engine
    {
        private readonly string[] lines;
        private readonly Dictionary<string, object> values = new(StringComparer.Ordinal);
        private readonly List<string> output = [];
        private int steps;

        public Engine(string source) => lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        public string Execute()
        {
            ExecuteBlock(0, lines.Length);
            return output.Count == 0 ? "Completed. No output." : string.Join(Environment.NewLine, output);
        }

        private void ExecuteBlock(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (++steps > 2_000) throw new DemoException("execution limit reached (2,000 statements).");
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("--")) continue;
                if (line.Length > 2_048) throw new DemoException($"line {i + 1} is too long.");

                var loop = Regex.Match(line, @"^for\s+([A-Za-z_]\w*)\s*=\s*(.+?)\s*,\s*(.+?)\s+do\s*$", RegexOptions.CultureInvariant);
                if (loop.Success)
                {
                    int stop = FindEnd(i + 1, end);
                    double first = Number(Evaluate(loop.Groups[2].Value));
                    double last = Number(Evaluate(loop.Groups[3].Value));
                    if (Math.Abs(last - first) > 1_000) throw new DemoException("loop range exceeds 1,000 iterations.");
                    if (first != Math.Truncate(first) || last != Math.Truncate(last)) throw new DemoException("loop bounds must be integers.");
                    for (double n = first; n <= last; n++)
                    {
                        values[loop.Groups[1].Value] = n;
                        ExecuteBlock(i + 1, stop);
                    }
                    i = stop;
                    continue;
                }
                if (line == "end") throw new DemoException($"unexpected end on line {i + 1}.");

                var call = Regex.Match(line, @"^(print|warn)\s*\((.*)\)\s*$", RegexOptions.CultureInvariant);
                if (call.Success)
                {
                    if (output.Count >= 200) throw new DemoException("output limit reached (200 lines).");
                    string content = Format(Evaluate(call.Groups[2].Value));
                    output.Add(call.Groups[1].Value == "warn" ? "WARN  " + content : content);
                    continue;
                }

                var assignment = Regex.Match(line, @"^(?:local\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.CultureInvariant);
                if (assignment.Success)
                {
                    values[assignment.Groups[1].Value] = Evaluate(assignment.Groups[2].Value);
                    continue;
                }
                throw new DemoException($"unsupported statement on line {i + 1}: {line}");
            }
        }

        private int FindEnd(int start, int end)
        {
            int depth = 1;
            for (int i = start; i < end; i++)
            {
                string line = lines[i].Trim();
                if (Regex.IsMatch(line, @"^for\s+.+\s+do\s*$")) depth++;
                else if (line == "end" && --depth == 0) return i;
            }
            throw new DemoException("a for loop is missing end.");
        }

        private object Evaluate(string expression) => new ExpressionParser(expression, values).Parse();
        private static double Number(object value) => value is double number ? number : throw new DemoException("a number was expected.");
        private static string Format(object value) => value is double number ? number.ToString("G", CultureInfo.InvariantCulture) : value.ToString() ?? "";
    }

    private sealed class ExpressionParser
    {
        private readonly string input;
        private readonly Dictionary<string, object> values;
        private int pos;
        public ExpressionParser(string input, Dictionary<string, object> values) { this.input = input; this.values = values; }
        public object Parse()
        {
            object value = ParseConcat();
            Skip();
            if (pos != input.Length) throw new DemoException($"unexpected expression near '{input[pos..]}'.");
            return value;
        }
        private object ParseConcat()
        {
            object left = ParseAdd();
            while (Take("..")) left = Format(left) + Format(ParseAdd());
            return left;
        }
        private object ParseAdd()
        {
            object left = ParseMultiply();
            while (true)
            {
                if (Take("+")) left = Number(left) + Number(ParseMultiply());
                else if (Take("-")) left = Number(left) - Number(ParseMultiply());
                else return left;
            }
        }
        private object ParseMultiply()
        {
            object left = ParseAtom();
            while (true)
            {
                if (Take("*")) left = Number(left) * Number(ParseAtom());
                else if (Take("/"))
                {
                    double divisor = Number(ParseAtom());
                    if (divisor == 0) throw new DemoException("division by zero.");
                    left = Number(left) / divisor;
                }
                else return left;
            }
        }
        private object ParseAtom()
        {
            Skip();
            if (Take("-")) return -Number(ParseAtom());
            if (Take("("))
            {
                object value = ParseConcat();
                if (!Take(")")) throw new DemoException("missing closing parenthesis.");
                return value;
            }
            if (pos < input.Length && (input[pos] == '"' || input[pos] == '\''))
            {
                char quote = input[pos++]; var sb = new StringBuilder();
                while (pos < input.Length && input[pos] != quote)
                {
                    char c = input[pos++];
                    if (c == '\\' && pos < input.Length)
                    {
                        c = input[pos++] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', var other => other };
                    }
                    sb.Append(c);
                }
                if (pos >= input.Length) throw new DemoException("unterminated string.");
                pos++; return sb.ToString();
            }
            int start = pos;
            while (pos < input.Length && (char.IsDigit(input[pos]) || input[pos] == '.' && !(pos + 1 < input.Length && input[pos + 1] == '.'))) pos++;
            if (pos > start && double.TryParse(input[start..pos], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
            start = pos;
            while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_')) pos++;
            if (pos > start)
            {
                string name = input[start..pos];
                if (values.TryGetValue(name, out var value)) return value;
                throw new DemoException($"unknown variable '{name}'.");
            }
            throw new DemoException("an expression was expected.");
        }
        private bool Take(string token) { Skip(); if (!input.AsSpan(pos).StartsWith(token, StringComparison.Ordinal)) return false; pos += token.Length; return true; }
        private void Skip() { while (pos < input.Length && char.IsWhiteSpace(input[pos])) pos++; }
        private static double Number(object value) => value is double number ? number : throw new DemoException("a number was expected.");
        private static string Format(object value) => value is double number ? number.ToString("G", CultureInfo.InvariantCulture) : value.ToString() ?? "";
    }
}

