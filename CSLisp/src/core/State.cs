using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        /// <summary> Stack of heterogeneous values (numbers, symbols, strings, closures, etc).
        /// Last item on the list is the top of the stack. </summary>
        public List<Val> stack = new List<Val>();

        /// <summary> Transient argument count register, used when calling functions </summary>
        public int nargs = 0;

        /// <summary> Helper flag, stops the REPL </summary>
        public bool done = false;

        public State (Closure closure, Val[] args) {
            fn = closure;
            code = fn.instructions;
            env = fn.env;
            foreach (Val arg in args) { stack.Add(arg); }
            nargs = args.Length;
        }

        public void Push (Val v) => stack.Add(v);

        public Val Pop () {
            Val result = Peek();
            stack.RemoveAt(stack.Count - 1);
            return result;
        }

        public Val Peek () {
            if (stack.Count == 0) { throw new LanguageError("Stack underflow!"); }
            return stack[stack.Count - 1];
        }

        internal static string PrintStack (State st) =>
            string.Format("{0,3}: [ {1} ]", st.stack.Count, string.Join(" ", st.stack.Select(val => Val.Print(val))));
    }
}