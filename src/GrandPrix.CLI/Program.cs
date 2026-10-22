using GrandPrix.Application;
using GrandPrix.CLI;
using GrandPrix.CLI.Input;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var sessionFactory = new GameSessionFactory();
var loadArgument = Array.FindIndex(args, argument =>
    argument.Equals("--load", StringComparison.OrdinalIgnoreCase));
IGameSession session;
if (loadArgument >= 0)
{
    if (loadArgument + 1 >= args.Length)
    {
        Console.Error.WriteLine("Specify a save slot after --load.");
        return;
    }

    try
    {
        session = sessionFactory.Load(args[loadArgument + 1]);
    }
    catch (InvalidDataException exception)
    {
        Console.Error.WriteLine($"The save is invalid: {exception.Message}");
        return;
    }
    catch (IOException exception)
    {
        Console.Error.WriteLine($"Could not load the save: {exception.Message}");
        return;
    }
}
else
{
    session = sessionFactory.CreateNew();
}
var forcePlain = args.Any(argument =>
    argument.Equals("--plain", StringComparison.OrdinalIgnoreCase));
var input = new SystemInputSource(forcePlain);
new TerminalGameApp(
    session,
    AnsiConsole.Console,
    input,
    forcePlain || !input.IsInteractive,
    sessionFactory).Run();
