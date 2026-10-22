using GrandPrix.Application;
using GrandPrix.CLI;
using GrandPrix.CLI.Input;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var session = new GameSession();
var forcePlain = args.Any(argument =>
    argument.Equals("--plain", StringComparison.OrdinalIgnoreCase));
var input = new SystemInputSource(forcePlain);
new TerminalGameApp(session, AnsiConsole.Console, input, forcePlain || !input.IsInteractive).Run();
