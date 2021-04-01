using System;

namespace CSLisp.Error
{
    /// <summary>
    /// Class for errors thrown from Lisp code at runtime.
    /// </summary>
    public class RuntimeError : Exception
    {
        public RuntimeError (string message) : base(message) { }
        public RuntimeError (params string[] messages) : base(string.Join(" ", messages)) { }
        public RuntimeError (string message, Exception inner) : base(message, inner) { }
    }
}