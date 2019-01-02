using CSLisp.Data;
using CSLisp.Libs;
using System.Collections.Generic;

namespace CSLisp.Core
{
    /// <summary>
    /// Binds together an instance of a compiler, parser, and executor.
    /// </summary>
    public class Context
    {
        public Packages packages;
        public Parser parser;
        public Compiler compiler;
        public Machine vm;

        public Context (bool loadLibraries = true, LoggerCallback logger = null) {
            this.packages = new Packages();
            this.parser = new Parser(packages, logger);
            this.compiler = new Compiler(this);
            this.vm = new Machine(this, logger);

            Primitives.InitializeCorePackage(packages.core);

            if (loadLibraries) {
                Libraries.LoadStandardLibraries(this);
            }
        }

        /// <summary> Processes the input as a string, and returns an array of results </summary>
        public List<Val> Execute (string input) {

            List<Val> outputs = new List<Val>();

            parser.AddString(input);
            List<Val> parseResults = parser.ParseAll();

            foreach (Val result in parseResults) {
                Closure cl = compiler.Compile(result);
                Val output = vm.Execute(cl);
                outputs.Add(output);
            }

            return outputs;
        }
    }

    /// <summary> Type signature for the debug logging function </summary>
    public delegate void LoggerCallback (params object[] args);

}