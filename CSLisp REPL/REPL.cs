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
            _logExecution = false;
        private static Dictionary<string, Action> _specialPragmas = new Dictionary<string, Action>() {
            { "!exit", () => _runRepl = false },
            { "!help", () => Console.WriteLine("Valid pragmas: " + string.Join(" ", _specialPragmas.Keys)) },
            { "!logcomp", () => {
                _logCompilation = !_logCompilation;
                Console.WriteLine("Logging compilation: " + _logCompilation);
            } },
            { "!logexec", () => {
                _logExecution = !_logExecution;
                Console.WriteLine("Logging execution: " + _logExecution);
            } },
        };

        private static void Main (string[] _) {

            Context ctx = new Context(logger: Logger);
            Console.WriteLine(GetInfo(ctx));

            var selfTest = ctx.CompileAndExecute("(+ 1 2)").Select(r => r.output);
            Console.Write(string.Format("\nSELF TEST: (+ 1 2) => {0}\n\n", string.Join(" ", selfTest)));

            while (_runRepl) {
                if (_showPrompt) {
                    Console.Write("> ");
                    _showPrompt = false;
                }

                string line = Console.ReadLine();
                string lower = line.ToLowerInvariant();
                bool isPragma = _specialPragmas.ContainsKey(lower);

                try {
                    if (isPragma) {
                        _specialPragmas[lower].Invoke();
                        _showPrompt = true;

                    } else {
                        var results = ctx.CompileAndExecute(line);
                        LogCompilation(ctx, results);

                        results.ForEach(entry => Console.WriteLine(Val.Print(entry.output)));
                        _showPrompt = (results.Count > 0);
                    }

                } catch (Error.LanguageError e) {
                    Console.Error.WriteLine("ERROR: " + e.Message);
                    _showPrompt = true;

                } catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                }
            }

        }

        private static void LogCompilation (Context ctx, List<Context.CompileAndExecuteResult> results) {
            if (!_logCompilation) { return; }

            results.ForEach(result => Console.WriteLine(ctx.code.DebugPrint(result.comp)));
        }

        private static void Logger (object[] args) {
            if (!_logExecution) { return; }

            var strings = args.Select(obj => (obj == null) ? "null" : obj.ToString());
            var message = string.Join(" ", strings);
            Console.WriteLine(message);
        }

        private static string GetInfo (Context ctx) {
            var manifestLocation = ctx.GetType().Assembly.Location;
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(manifestLocation);
            return $"CSLisp REPL. {info.LegalCopyright}. Version {info.ProductVersion}.";
        }
    }
}
