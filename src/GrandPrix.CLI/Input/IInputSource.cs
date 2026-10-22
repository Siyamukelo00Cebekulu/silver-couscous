namespace GrandPrix.CLI.Input;

public interface IInputSource
{
    bool IsInteractive { get; }
    string? ReadLine();
    ConsoleKeyInfo ReadKey();
    bool TryReadKey(out ConsoleKeyInfo key);
}

public sealed class SystemInputSource : IInputSource
{
    private readonly bool _forcePlain;

    public SystemInputSource(bool forcePlain = false)
    {
        _forcePlain = forcePlain;
    }

    public bool IsInteractive =>
        !_forcePlain && !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public string? ReadLine() => Console.ReadLine();

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    public bool TryReadKey(out ConsoleKeyInfo key)
    {
        if (!IsInteractive || !Console.KeyAvailable)
        {
            key = default;
            return false;
        }

        key = Console.ReadKey(intercept: true);
        return true;
    }
}
