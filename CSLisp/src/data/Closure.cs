using System.Collections.Generic;

namespace CSLisp.Data
{
    /// <summary>
    /// Encapsulates everything needed for a function call
    /// </summary>
    public class Closure
    {
        /// <summary> Compiled sequence of instructions </summary>
        public List<Instruction> instructions;

        /// <summary> Environment in which we're running </summary>
        public Environment env;

        /// <summary> List of arguments this function expects </summary>
        public Cons args;

        /// <summary> Optional closure name, for debugging purposes only </summary>
        public string name;

        public Closure (List<Instruction> instructions, Environment env, Cons args, string name) {
            this.instructions = instructions;
            this.env = env;
            this.args = args;
            this.name = name;
        }
    }

}