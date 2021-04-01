using CSLisp.Core;
using CSLisp.Data;
using System.Collections.Generic;
using System.IO;

namespace CSLisp.Libs
{
    /// <summary>
    /// Manages standard libraries
    /// </summary>
    public class Libraries
    {
        /// <summary> All libraries as a list </summary>
        private static List<byte[]> GetAllBuiltInLibraries () =>
            new List<byte[]>() { Resources.Core, Resources.Record, Resources.User };

        /// <summary> Loads all standard libraries into an initialized machine instance </summary>
        public static void LoadStandardLibraries (Context ctx) {
            var allLibs = GetAllBuiltInLibraries();
            foreach (byte[] libBytes in allLibs) {
                using var stream = new MemoryStream(libBytes);
                using var reader = new StreamReader(stream);
                string libText = reader.ReadToEnd();
                LoadLibrary(ctx, libText);
            }
        }

        /// <summary> Loads a single string into the execution context </summary>
        private static void LoadLibrary (Context ctx, string lib) {
            ctx.parser.AddString(lib);

            while (true) {
                Val result = ctx.parser.ParseNext();
                if (Val.Equals(Parser.EOF, result)) {
                    break;
                }

                Closure cl = ctx.compiler.Compile(result).closure;
                Val _ = ctx.vm.Execute(cl);
                // and we drop the output on the floor... for now... :)
            }
        }

    }

}