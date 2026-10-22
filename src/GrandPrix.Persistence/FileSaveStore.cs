using GrandPrix.Core.Abstractions;

namespace GrandPrix.Persistence;

public sealed class FileSaveStore : ISaveStore
{
    private const string SaveExtension = ".sav";
    private readonly string _directory;

    public FileSaveStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A save directory is required.", nameof(directory));

        _directory = Path.GetFullPath(directory);
    }

    public void Save(string slot, byte[] data)
    {
        ValidateSlot(slot);
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        Directory.CreateDirectory(_directory);

        var destination = GetPath(slot);
        var temporary = Path.Combine(_directory, $"{slot}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, data);
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public byte[] Load(string slot)
    {
        ValidateSlot(slot);
        var path = GetPath(slot);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Save slot '{slot}' does not exist.", path);

        return File.ReadAllBytes(path);
    }

    public IReadOnlyList<string> ListSlots()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        return Directory.GetFiles(_directory, $"*{SaveExtension}")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(slot => slot, StringComparer.Ordinal)
            .ToArray();
    }

    private string GetPath(string slot) => Path.Combine(_directory, slot + SaveExtension);

    private static void ValidateSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) ||
            slot.Any(character =>
                !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            throw new ArgumentException(
                "Save slot names may contain only letters, digits, hyphens, and underscores.",
                nameof(slot));
    }
}
