using CSLisp.Data;
using System.Collections.Generic;

namespace CSLisp.Core
{
    /// <summary>
    /// Virtual machine state
    /// </summary>
    public class State
    {
        /// <summary> List of instructions we're executing </summary>
        public List<Instruction> code = null;

        /// <summary> Reference back to the closure in which these instructions live </summary>
        public Closure fn = null;

        /// <summary> Program counter; index into the instruction list </summary>
        public int pc = 0;

        /// <summary> Reference to the current environment (head of the chain of environments) </summary>
        public Environment env = null;

        /// <summary> Stack of heterogeneous values (numbers, symbols, strings, closures, etc) </summary>
        public Stack<Val> stack = new Stack<Val>();

        /// <summary> Transient argument count register, used when calling functions </summary>
        public int nargs = 0;

        /// <summary> Helper flag, stops the REPL </summary>
        public bool done = false;
    }
}