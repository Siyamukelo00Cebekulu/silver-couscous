using System.Collections.Generic;
using GrandPrix.Application;
using GrandPrix.CLI;
using GrandPrix.CLI.Input;
using Spectre.Console.Testing;

namespace GrandPrix.CLI.Tests;

public sealed class TerminalGameAppTests
{
    [Fact]
    public void PlainScriptCanHireAdvanceRunRacePitAndSettle()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grand-prix-cli-{Guid.NewGuid():N}");
        try
        {
            var console = new TestConsole();
            var input = new ScriptedInputSource(
                "staff",
                "1",
                "y",
                "advance",
                "event",
                "weekend",
                "setup",
                "soft",
                "normal",
                "2",
                "pit 11 hard",
                "exit",
                "y");
            var factory = new GameSessionFactory(
                new GrandPrix.Persistence.FileSaveStore(directory));
            var session = (GameSession)factory.CreateNew();
            var app = new TerminalGameApp(
                session, console, input, plainMode: true, sessionFactory: factory);

            app.Run();

            Assert.Single(session.GetStaff().Engineers);
            Assert.Equal(1, session.GetCalendar().Count(item => item.Completed));
            Assert.True(session.GetStandings().Sum(item => item.IsPlayer ? item.Points : 0) > 0);
            Assert.Contains("FINAL CLASSIFICATION", console.Output);
            Assert.Contains("pit", console.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Race settled", console.Output);
            Assert.Contains("autosave", factory.ListSaves());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InvalidCommandsAreVisibleAndEndOfInputStopsCleanly()
    {
        var console = new TestConsole();
        var input = new ScriptedInputSource("not-a-command");
        var app = new TerminalGameApp(new GameSession(), console, input, plainMode: true);

        app.Run();

        Assert.Contains("Unknown command", console.Output);
        Assert.Contains("GRAND PRIX TEAM PRINCIPAL", console.Output);
    }

    [Fact]
    public void InteractiveNavigationUsesArrowKeysAndConfirmedQuit()
    {
        var console = new TestConsole();
        var input = new ScriptedKeyInputSource(
            new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
            new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false),
            new ConsoleKeyInfo('y', ConsoleKey.Y, false, false, false),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        var app = new TerminalGameApp(new GameSession(), console, input);

        app.Run();

        Assert.Contains("SEASON CALENDAR", console.Output);
        Assert.Contains("Quit the current session", console.Output);
    }

    [Fact]
    public void PlainCommandsCanSaveAndRestoreASession()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grand-prix-cli-{Guid.NewGuid():N}");
        try
        {
            var factory = new GameSessionFactory(new GrandPrix.Persistence.FileSaveStore(directory));
            var console = new TestConsole();
            var input = new ScriptedInputSource(
                "advance",
                "week",
                "save career",
                "advance",
                "day",
                "load career",
                "exit",
                "y");
            var app = new TerminalGameApp(
                factory.CreateNew(),
                console,
                input,
                plainMode: true,
                sessionFactory: factory);

            app.Run();

            Assert.Contains("Saved game to 'career'.", console.Output);
            Assert.Contains("Loaded game from 'career'.", console.Output);
            Assert.Equal(
                new System.DateTime(2026, 3, 8),
                factory.Load("career").GetDashboard().Today);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ScriptedInputSource : IInputSource
    {
        private readonly Queue<string> _lines;

        public ScriptedInputSource(params string[] lines)
        {
            _lines = new Queue<string>(lines);
        }

        public bool IsInteractive => false;

        public string? ReadLine() => _lines.Count > 0 ? _lines.Dequeue() : null;

        public ConsoleKeyInfo ReadKey() => throw new InvalidOperationException(
            "The scripted plain-mode input does not produce keys.");

        public bool TryReadKey(out ConsoleKeyInfo key)
        {
            key = default;
            return false;
        }
    }

    private sealed class ScriptedKeyInputSource : IInputSource
    {
        private readonly Queue<ConsoleKeyInfo> _keys;

        public ScriptedKeyInputSource(params ConsoleKeyInfo[] keys)
        {
            _keys = new Queue<ConsoleKeyInfo>(keys);
        }

        public bool IsInteractive => true;

        public string? ReadLine() => throw new InvalidOperationException(
            "The scripted interactive input produces key presses.");

        public ConsoleKeyInfo ReadKey() => _keys.Count > 0
            ? _keys.Dequeue()
            : throw new InvalidOperationException("The interactive key script ended unexpectedly.");

        public bool TryReadKey(out ConsoleKeyInfo key)
        {
            key = default;
            return false;
        }
    }
}
