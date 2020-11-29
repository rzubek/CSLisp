using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;
using System.Linq;

namespace CSLisp.Core
{
    /// <summary>
    /// Virtual machine state
    /// </summary>
    public sealed class State
    {
        /// <summary> Closure containing current code block </summary>
        public Closure fn = null;

        /// <summary> Program counter; index into the instruction list </summary>
        public int pc = 0;

        /// <summary> Reference to the current environment (head of the chain of environments) </summary>
        public Environment env = null;

        /// <summary> Stack of heterogeneous values (numbers, symbols, strings, closures, etc).
        /// Last item on the list is the top of the stack. </summary>
        public List<Val> stack = new List<Val>();

        /// <summary> Transient argument count register, used when calling functions </summary>
        public int argcount = 0;

        /// <summary> Helper flag, stops the REPL </summary>
        public bool done = false;

        public State (Closure closure, Val[] args) {
            fn = closure;
            env = fn.env;
            foreach (Val arg in args) { stack.Add(arg); }
            argcount = args.Length;
        }

        public void Push (Val v) => stack.Add(v);

        public Val Pop () {
            var count = stack.Count;
            if (count > 0) {
                var result = stack[count - 1];
                stack.RemoveAt(count - 1);
                return result;
            }

            throw new LanguageError("Stack underflow!");
        }

        public Val Peek () {
            var count = stack.Count;
            if (count > 0) {
                return stack[count - 1];
            }

            throw new LanguageError("Stack underflow!");
        }

        internal static string PrintStack (State st) =>
            string.Format("{0,3}: [ {1} ]", st.stack.Count,
                string.Join(" ", st.stack.Select(val => Val.DebugPrint(val))));
    }
}