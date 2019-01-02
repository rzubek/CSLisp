using CSLisp.Core;
using CSLisp.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CSLisp
{
    class REPL
    {
        static void Main (string[] args) {

            Context ctx = new Context();
            Console.WriteLine(GetInfo(ctx));

            Console.Write(string.Format("\nSELF TEST: (+ 1 2) => {0}\n\n", string.Join(" ", ctx.Execute("(+ 1 2)").ToArray())));

            bool showPrompt = true;

            while (true) {
                if (showPrompt) {
                    Console.Write("> ");
                    showPrompt = false;
                }

                string line = Console.ReadLine();
                if (line.ToLowerInvariant().StartsWith("!exit")) {
                    break;
                }

                try {
                    List<Val> results = ctx.Execute(line);
                    results.ForEach(val => Console.WriteLine(val));
                    showPrompt = (results.Count > 0);

                } catch (Error.LanguageError e){
                    Console.Error.WriteLine("ERROR: " + e.Message);
                    showPrompt = true;

                } catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                }
            }

        }

        private static string GetInfo (Context ctx) {
            var manifestLocation = ctx.GetType().Assembly.Location;
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(manifestLocation);
            return $"CSLisp REPL. {info.LegalCopyright}. Version {info.ProductVersion}.";
        }
    }
}
