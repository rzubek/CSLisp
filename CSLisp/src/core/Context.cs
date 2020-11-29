using CSLisp.Data;
using CSLisp.Libs;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CSLisp.Core
{
    /// <summary>
    /// Definition of a logger that will capture debug info about compilation and execution.
    /// </summary>
    public interface ILogger
    {
        /// <summary> When true, we will log expression parsing details </summary>
        bool EnableParsingLogging { get; }

        /// <summary> When true, we will log every executed instruction </summary>
        bool EnableInstructionLogging { get; }

        /// <summary> When true, we will log stack state at the end of each executed instruction </summary>
        bool EnableStackLogging { get; }

        /// <summary> Type signature for the actual debug logging function </summary>
        void Log (params object[] args);
    }

    /// <summary>
    /// Binds together an instance of a compiler, parser, and executor.
    /// </summary>
    public class Context
    {
        public readonly Code code;
        public readonly Packages packages;
        public readonly Parser parser;
        public readonly Compiler compiler;
        public readonly Machine vm;

        public Context (bool loadLibraries = true, ILogger logger = null) {
            this.code = new Code();
            this.packages = new Packages();
            this.parser = new Parser(packages, logger);
            this.compiler = new Compiler(this);
            this.vm = new Machine(this, logger);

            Primitives.InitializeCorePackage(this, packages.core);

            if (loadLibraries) {
                Libraries.LoadStandardLibraries(this);
            }
        }

        /// <summary> Stores the result of compiling a given code block and executing it </summary>
        public struct CompileAndExecuteResult
        {
            public string input;
            public CompilationResults comp;
            public Val output;
            public TimeSpan exectime;
        }

        /// <summary> Convenience wrapper that processes the input as a string, and returns an array of results. </summary>
        public List<CompileAndExecuteResult> CompileAndExecute (string input) {

            var outputs = new List<CompileAndExecuteResult>();

            parser.AddString(input);
            var parseResults = parser.ParseAll();

            foreach (Val result in parseResults) {
                var cr = compiler.Compile(result);

                Stopwatch s = Stopwatch.StartNew();
                var output = vm.Execute(cr.closure);
                s.Stop();

                outputs.Add(new CompileAndExecuteResult {
                    input = input, comp = cr, output = output, exectime = s.Elapsed
                });
            }

            return outputs;
        }
    }
}