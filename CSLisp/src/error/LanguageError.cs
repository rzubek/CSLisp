using System;

namespace CSLisp.Error
{
    /// <summary>
	/// Class for errors related to the language engine, not specific to a particular pass.
	/// </summary>
    public class LanguageError : Exception
    {
        public LanguageError (string message) : base(message) { }
    }

}