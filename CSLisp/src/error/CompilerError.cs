using System;

namespace CSLisp.Error
{
    /// <summary>
	/// Class for errors thrown during the compilation phase 
	/// </summary>
    public class CompilerError : Exception
    {
        public CompilerError (string message) : base(message) { }
    }

}