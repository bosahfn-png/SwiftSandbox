using SwiftSandbox;

var checks = new (string Name, string Script, string Expected)[]
{
    ("print", "print(\"Hello\")", "Hello"),
    ("variables", "local x = 4\nprint(x * 3)", "12"),
    ("loop", "for i = 1, 3 do\n print(\"Count: \" .. i)\nend", "Count: 1\nCount: 2\nCount: 3"),
    ("nested loops", "for i = 1, 2 do\n for j = 1, 2 do\n  print(i * j)\n end\nend", "1\n2\n2\n4"),
    ("bad function blocked", "os.execute(\"hello\")", "Error:"),
    ("limit", "for i = 1, 999999 do\n print(i)\nend", "Error:")
};
foreach (var (name, script, expected) in checks)
{
    string result = SandboxRunner.Run(script).Replace("\r\n", "\n");
    if (!result.StartsWith(expected, StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"FAIL {name}: {result}");
        return 1;
    }
    Console.WriteLine($"PASS {name}");
}
return 0;

