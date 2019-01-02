namespace CSLisp.Data 
{
	/// <summary>
	/// Encapsulates a macro and code that runs to expand it.
	/// </summary>
	public class Macro 
	{
        /// <summary> Optional debug name </summary>
		public string name;

        /// <summary> List of arguments for the macro </summary>
		public Cons args;

        /// <summary> Body of the macro </summary>
		public Closure body;
		
		public Macro (Symbol name, Cons args, Closure body)
		{
			this.name = name.name;
			this.args = args;
			this.body = body;
		}
	}

}