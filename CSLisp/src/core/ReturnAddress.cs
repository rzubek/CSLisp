using CSLisp.Data;

namespace CSLisp.Core
{
    /// <summary>
    /// Stores continuation, for example so that we can resume execution after a function call
    /// </summary>
	public class ReturnAddress
    {
        /// <summary> Closure we're returning to </summary>
		public Closure fn;

        /// <summary> Program counter we're returning to </summary>
		public int pc;

        /// <summary> Environment that needs to be restored </summary>
		public Environment env;

        /// <summary> Return label name for debugging </summary>
        public string debug;

        public ReturnAddress (Closure fn, int pc, Environment env, string debug) {
            this.fn = fn;
            this.pc = pc;
            this.env = env;
            this.debug = debug;
        }
    }
}