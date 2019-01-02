namespace CSLisp.Data
{
    /// <summary>
	/// Immutable symbol, interned in a specific package.
	/// Interned symbols are unique, so we can test for equality using simple ==
	/// </summary>
    public class Symbol
    {
        /// <summary> string name of this symbol </summary>
        private string _name;

        /// <summary> Package in this symbol is interned </summary>
        private Package _pkg;

        /// <summary> Full (package-prefixed) name of this symbol </summary>
        private string _fullName;

        /// <summary> If true, this symbol is visible outside of its package. This can be adjusted later. </summary>
        public bool exported = false;

        public Symbol (string name, Package pkg) {
            _name = name;
            _pkg = pkg;

            _fullName = (_pkg != null && _pkg.name != null) ? (_pkg.name + ":" + _name) : _name;
        }

        /// <summary> string name of this symbol </summary>
        public string name => _name;

        /// <summary> Package in which this symbol is interned </summary>
        public Package pkg => _pkg;

        /// <summary> Returns the full name, including package prefix </summary>
        public string fullName => _fullName;
    }
}