namespace CSLisp.Data
{
    /// <summary>
	/// Immutable symbol, interned in a specific package.
	/// Interned symbols are unique, so we can test for equality using simple ==
	/// </summary>
    public class Symbol
    {
        /// <summary> String name of this symbol </summary>
        public string name { get; private set; }

        /// <summary> Package in this symbol is interned </summary>
        public Package pkg { get; private set; }

        /// <summary> Full (package-prefixed) name of this symbol </summary>
        public string fullName { get; private set; }

        /// <summary> If true, this symbol is visible outside of its package. This can be adjusted later. </summary>
        public bool exported = false;

        public Symbol (string name, Package pkg) {
            this.name = name;
            this.pkg = pkg;

            this.fullName = (pkg != null && pkg.name != null) ? (pkg.name + ":" + name) : name;
        }
    }
}