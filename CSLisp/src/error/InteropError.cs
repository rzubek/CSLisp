using System;

namespace CSLisp.Error
{
    /// <summary>
    /// Class for errors related to .net interop; see inner exception for details.
    /// </summary>
    public class InteropError : Exception
    {
        public InteropError (string message) : base(message) { }
        public InteropError (string message, Exception inner) : base(message, inner) { }
    }
}