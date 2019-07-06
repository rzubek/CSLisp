using CSLisp.Error;

namespace CSLisp.Data
{
    /// <summary>
    /// Stores variable lookup data: frame index relative to current frame,
    /// and symbol index in the targe frame
    /// </summary>
    public struct VarPos
    {
        public static readonly VarPos INVALID = new VarPos(-1, -1);

        public int frameIndex, symbolIndex;

        public VarPos (Val frameIndex, Val symbolIndex) {
            this.frameIndex = frameIndex.AsInt;
            this.symbolIndex = symbolIndex.AsInt;
        }

        public VarPos (int frameIndex, int symbolIndex) {
            this.frameIndex = frameIndex;
            this.symbolIndex = symbolIndex;
        }

        public bool IsValid => frameIndex >= 0 && symbolIndex >= 0;
        public bool IsNotValid => !IsValid;
    }

    /// <summary>
    /// An Environment instance binds variables to their values.
    /// Variable names are for compilation only - they're not used at runtime
    /// (except for debugging help).
    /// </summary>
    public class Environment
    {
        /// <summary> Parent environment </summary>
        private Environment _parent;

        /// <summary> Symbols defined in this environment </summary>
        private Symbol[] _symbols;

        /// <summary> Values defined for each symbol </summary>
        private Val[] _values;

        public Environment (int count, Environment parent) {
            _symbols = new Symbol[count];
            _values = new Val[count];
            _parent = parent;
        }

        /// <summary> Reference to the parent environment </summary>
        public Environment parent => _parent;

        /// <summary> Creates a new environment from a cons'd list of arguments </summary>
        public static Environment Make (Cons args, Environment parent) {
            int count = Cons.Length(args);
            Environment env = new Environment(count, parent);

            for (int i = 0; i < count; i++) {
                env.SetSymbol(i, args.first.AsSymbol);
                env.SetValue(i, Val.NIL);
                args = args.rest.AsConsOrNull;
            }

            return env;
        }

        /// <summary> Retrieves symbol at the given index </summary>
        public Symbol GetSymbol (int symbolIndex) => _symbols[symbolIndex];

        /// <summary> Sets symbol at the given index </summary>
        private void SetSymbol (int symbolIndex, Symbol symbol) => _symbols[symbolIndex] = symbol;

        /// <summary> Retrieves the index of the given symbol </summary>
        private int IndexOfSymbol (Symbol symbol) {
            for (int i = 0, count = _symbols.Length; i < count; i++) {
                if (_symbols[i] == symbol) { return i; }
            }
            return -1;
        }

        /// <summary> Retrieves value at the given index </summary>
        public Val GetValue (int symbolIndex) => _values[symbolIndex];

        /// <summary> Sets value at the given index </summary>
        public void SetValue (int symbolIndex, Val value) => _values[symbolIndex] = value;

        /// <summary> Returns the number of slots defined in this environment </summary>
        public int Length => _symbols.Length;

        /// <summary> 
        /// Returns coordinates of a symbol, relative to the given environment, or null if not present.
        /// First element of the vector is the index of the environment, in the chain,
        /// and the second element is the index of the variable itself. 
        /// </summary>
        public static VarPos GetVariable (Symbol symbol, Environment frame) {
            int frameIndex = 0;
            while (frame != null) {
                int symbolIndex = frame.IndexOfSymbol(symbol);
                if (symbolIndex >= 0) {
                    return new VarPos(frameIndex, symbolIndex);
                } else {
                    frame = frame._parent;
                    frameIndex++;
                }
            }
            return VarPos.INVALID;
        }

        /// <summary> Retrieves the symbol at the given coordinates, relative to the current environment. </summary>
        public static Symbol GetSymbolAt (VarPos varref, Environment frame)
            => GetFrame(varref.frameIndex, frame).GetSymbol(varref.symbolIndex);

        /// <summary> Sets the symbol at the given coordinates, relative to the current environment. </summary>
        public static void SetSymbolAt (VarPos varref, Symbol symbol, Environment frame)
            => GetFrame(varref.frameIndex, frame).SetSymbol(varref.symbolIndex, symbol);

        /// <summary> Retrieves the value at the given coordinates, relative to the current environment. </summary>
        public static Val GetValueAt (VarPos varref, Environment frame)
            => GetFrame(varref.frameIndex, frame).GetValue(varref.symbolIndex);

        /// <summary> Sets the value at the given coordinates, relative to the current environment. </summary>
        public static void SetValueAt (VarPos varref, Val value, Environment frame)
            => GetFrame(varref.frameIndex, frame).SetValue(varref.symbolIndex, value);

        /// <summary> Returns the specified frame, relative to the current environment </summary>
        private static Environment GetFrame (int frameIndex, Environment frame) {
            for (int i = 0; i < frameIndex; i++) {
                frame = frame._parent;
                if (frame == null) {
                    throw new LanguageError("Invalid frame coordinates detected");
                }
            }
            return frame;
        }
    }

}