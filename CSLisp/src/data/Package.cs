using CSLisp.Error;
using System.Collections.Generic;
using System.Linq;

namespace CSLisp.Data
{
    /// <summary>
    /// Package is a storage for symbols. When the parser reads out symbols from the stream,
    /// it retrieves the appropriate symbol from the package, or if one hasn't been seen before,
    /// it interns a new one.
    /// </summary>
    public class Package
    {
        /// <summary> Name of this package </summary>
        public readonly string name;

        /// <summary> Map from symbol name (string) to instance (Symbol) </summary>
        private readonly Dictionary<string, Symbol> _symbols;

        /// <summary> Map from symbol (Symbol) to its value (*) </summary>
        private readonly Dictionary<Symbol, Val> _bindings;

        /// <summary> Map from macro name (Symbol) to the actual macro body </summary>
        private readonly Dictionary<Symbol, Macro> _macros;

        /// <summary> 
        /// Vector of other packages imported into this one. 
        /// Symbol lookup will use these packages, if the symbol is not found here. 
        /// </summary>
        private List<Package> _imports;

        public Package (string name) {
            this.name = name;
            _symbols = new Dictionary<string, Symbol>();
            _bindings = new Dictionary<Symbol, Val>();
            _macros = new Dictionary<Symbol, Macro>();
            _imports = new List<Package>();
        }

        /// <summary> 
        /// Returns a symbol with the given name if one was interned, undefined otherwise.
        /// If deep is true, it will also search through all packages imported by this one. 
        /// </summary>
        public Symbol Find (string name, bool deep) {
            if (_symbols.TryGetValue(name, out Symbol result)) {
                return result;
            }

            if (deep) {
                foreach (Package pkg in _imports) {
                    result = pkg.Find(name, deep);
                    if (result != null) {
                        return result;
                    }
                }
            }

            return null;
        }

        /// <summary> 
        /// Interns the given name. If a symbol with this name already exists, it is returned.
        /// Otherwise a new symbol is created, added to internal storage, and returned.
        /// </summary>
        public Symbol Intern (string name) {
            Symbol result;
            if (!_symbols.TryGetValue(name, out result)) {
                result = _symbols[name] = new Symbol(name, this);
            }
            return result;
        }


        /// <summary> 
		/// Uninterns the given symbol. If a symbol existed with this name, it will be removed,
		/// and the function returns true; otherwise returns false.
		/// </summary>
        public bool Unintern (string name) {
            if (_symbols.ContainsKey(name)) {
                _symbols.Remove(name);
                return true;
            } else {
                return false;
            }
        }

        /// <summary> Retrieves the value binding for the given symbol, also traversing the import list. </summary>
        public Val GetValue (Symbol symbol) {
            if (symbol.pkg != this) {
                throw new LanguageError("Unexpected package in getBinding: " + symbol.pkg.name);
            }

            if (_bindings.TryGetValue(symbol, out Val val)) {
                return val;
            }

            // try imports
            foreach (Package pkg in _imports) {
                Symbol local = pkg.Find(symbol.name, false);
                if (local != null && local.exported) {
                    if (pkg._bindings.TryGetValue(local, out Val innerval)) {
                        return innerval;
                    }
                }
            }

            return Val.NIL;
        }

        /// <summary> Sets the binding for the given symbol. If NIL, deletes the binding. </summary>
        public void SetValue (Symbol symbol, Val value) {
            if (symbol.pkg != this) {
                throw new LanguageError("Unexpected package in setBinding: " + symbol.pkg.name);
            }

            if (value.IsNil) {
                _bindings.Remove(symbol);
            } else {
                _bindings[symbol] = value;
            }
        }

        /// <summary> Returns true if this package contains the named macro </summary>
        public bool HasMacro (Symbol symbol) => GetMacro(symbol) != null;

        /// <summary> Retrieves the macro for the given symbol, potentially null </summary>
        public Macro GetMacro (Symbol symbol) {
            if (symbol.pkg != this) {
                throw new LanguageError("Unexpected package in getBinding: " + symbol.pkg.name);
            }

            if (_macros.TryGetValue(symbol, out Macro val)) {
                return val;
            }

            // try imports
            foreach (Package pkg in _imports) {
                Symbol s = pkg.Find(symbol.name, false);
                if (s != null && s.exported) {
                    if (pkg._macros.TryGetValue(s, out Macro innerval)) {
                        return innerval;
                    }
                }
            }

            return null;
        }

        /// <summary> Sets the macro for the given symbol. If null, deletes the macro. </summary>
        public void SetMacro (Symbol symbol, Macro value) {
            if (symbol.pkg != this) {
                throw new LanguageError("setMacro called with invalid package");
            }

            if (value == null) {
                _macros.Remove(symbol);
            } else {
                _macros[symbol] = value;
            }
        }

        /// <summary> Adds a new import </summary>
        public void AddImport (Package pkg) {
            if (pkg == this) {
                throw new LanguageError("Package cannot import itself!");
            }

            if (!_imports.Contains(pkg)) {
                _imports.Add(pkg);
            }
        }

        /// <summary> Returns a new vector of all symbols imported by this package </summary>
        public List<Val> ListImports () => _imports.Select(pkg => new Val(pkg.name)).ToList();

        /// <summary> Returns a new vector of all symbols interned and exported by this package </summary>
        public List<Val> ListExports () => _symbols.Values.Where(sym => sym.exported).Select(sym => new Val(sym)).ToList();
    }

}