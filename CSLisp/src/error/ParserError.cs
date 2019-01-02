using System;

namespace CSLisp.Error
{
    /// <summary>
	/// Class for errors encountered during the parsing phase
	/// </summary>
    public class ParserError : Exception
    {
        public ParserError (string message) : base(message) { }
    }

}