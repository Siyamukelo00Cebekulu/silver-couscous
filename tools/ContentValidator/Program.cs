using GrandPrix.Data;

var root = args.Length == 0 ? Path.Combine("content") : args[0];
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Content directory was not found: {root}");
    return 1;
}

var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).ToArray();
var failures = new List<string>();
foreach (var file in files)
{
    failures.AddRange(GameContentLoader.ValidateFile(file)
        .Select(error => $"{file}: {error}"));
}

if (failures.Count > 0)
{
    foreach (var failure in failures)
        Console.Error.WriteLine(failure);
    return 1;
}

Console.WriteLine($"Validated {files.Length} JSON content file(s).");
return 0;
