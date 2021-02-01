using CSLisp.Core;
using CSLisp.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CSLisp
{
    internal class REPL
    {
        private static bool
            _runRepl = true,
            _showPrompt = true,
            _logCompilation = false,
            _logExecution = false,
            _timeNextExecution = false;

        private class Command
        {
            public string command, description;
            public Action action;

            public Command (string command, string description, Action action) {
                this.command = command;
                this.description = description;
                this.action = action;
            }

            public string Message => $"{command} - {description}";
        }

        private class Logger : ILogger
        {
            public bool EnableParsingLogging => _logExecution;
            public bool EnableInstructionLogging => _logExecution;
            public bool EnableStackLogging => _logExecution;

            public void Log (params object[] args) {
                var strings = args.Select(obj => (obj == null) ? "null" : obj.ToString());
                var message = string.Join(" ", strings);
                Console.WriteLine(message);
            }
        }

        private static readonly List<Command> _commands = new List<Command>() {
            new Command(",exit", "Quits the REPL", () => _runRepl = false),
            new Command(",help", "Shows this help menu",
                () => Console.WriteLine("Valid repl commands:\n" + string.Join("\n", _commands.Select(p => p.Message)))),

            new Command(",logcomp", "Toggles logging of bytecode compilation",
                () => {
                    _logCompilation = !_logCompilation;
                    Console.WriteLine("Logging compilation: " + _logCompilation);
                }),
            new Command(",logexec", "Toggles logging of bytecode execution",
                () => {
                    _logExecution = !_logExecution;
                    Console.WriteLine("Logging execution: " + _logExecution);
                }),
            new Command(",time", "Type ',time (expression ...)' to log and print execution time of that expression",
                () => _timeNextExecution = true)
        };

        public void Run () {

            Context ctx = new Context(logger: new Logger());
            Console.WriteLine(GetInfo(ctx));

            var selfTest = ctx.CompileAndExecute("(+ 1 2)").Select(r => r.output);
            Console.WriteLine();
            Console.WriteLine("SELF TEST: (+ 1 2) => " + string.Join(" ", selfTest));
            Console.WriteLine("Type ,help for list of repl commands or ,exit to quit.\n");

            while (_runRepl) {
                if (_showPrompt) {
                    Console.Write("> ");
                    _showPrompt = false;
                }

                string line = Console.ReadLine();
                var cmd = _commands.Find(c => line.StartsWith(c.command));

                try {
                    if (cmd != null) {
                        line = line.Remove(0, cmd.command.Length).TrimStart();
                        cmd.action.Invoke();
                    }

                    Stopwatch s = _timeNextExecution ? Stopwatch.StartNew() : null;
                    var results = ctx.CompileAndExecute(line);
                    LogCompilation(ctx, results);
                    LogExecutionTime(results, s);

                    results.ForEach(entry => Console.WriteLine(Val.Print(entry.output)));

                    _showPrompt = cmd != null || results.Count > 0;

                } catch (Error.LanguageError e) {
                    Console.Error.WriteLine("ERROR: " + e.Message);
                    _showPrompt = true;

                } catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                }
            }

        }

        private void LogExecutionTime (List<Context.CompileAndExecuteResult> results, Stopwatch s) {
            if (s == null) { return; }

            _timeNextExecution = false;

            foreach (var result in results) {
                Console.WriteLine($"Execution took {result.exectime.TotalSeconds} seconds for: {result.input}");
            }
        }

        private void LogCompilation (Context ctx, List<Context.CompileAndExecuteResult> results) {
            if (!_logCompilation) { return; }

            results.ForEach(result => Console.WriteLine(ctx.code.DebugPrint(result.comp)));
        }

        private string GetInfo (Context ctx) {
            var manifestLocation = ctx.GetType().Assembly.Location;
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(manifestLocation);
            return $"CSLisp REPL. {info.LegalCopyright}. Version {info.ProductVersion}.";
        }

        private static void Main (string[] _) {
            REPL repl = new REPL();
            repl.Run();
        }
    }

    /// <summary>
    /// This class is purely for testing .net interop during development, and may be removed later
    /// </summary>
    public class TestClass
    {
        public int MyIntField;
        public int MyIntGetter => MyIntField;
        public int MyIntProperty { get; set; }

        public string MyStringField;
    }
}
