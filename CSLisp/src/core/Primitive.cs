using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;

namespace CSLisp.Core
{
    public delegate Val FnThunk (Context ctx);
    public delegate Val FnUnary (Context ctx, Val a);
    public delegate Val FnBinary (Context ctx, Val a, Val b);
    public delegate Val FnVarArg (Context ctx, List<Val> args);

    /// <summary>
    /// Holds a reference to a primitive function.
    /// This wrapper is intended to simplify function calls of various arities
    /// without going through reflection, and incurring the perf cost.
    /// </summary>
    public class Function
    {
        public FnThunk fnThunk;
        public FnUnary fnUnary;
        public FnBinary fnBinary;
        public FnVarArg fnVarArg;

        public Function (FnThunk fn) { fnThunk = fn; }
        public Function (FnUnary fn) { fnUnary = fn; }
        public Function (FnBinary fn) { fnBinary = fn; }
        public Function (FnVarArg fn) { fnVarArg = fn; }

        public Val Call (Context ctx) {
            if (fnThunk != null) {
                return fnThunk(ctx);
            } else if (fnVarArg != null) {
                return fnVarArg(ctx, new List<Val>());
            } else {
                throw new LanguageError("Primitive function call of incorrect zero arity");
            }
        }

        public Val Call (Context ctx, Val a) {
            if (fnUnary != null) {
                return fnUnary(ctx, a);
            } else if (fnVarArg != null) {
                return fnVarArg(ctx, new List<Val>() { a });
            } else {
                throw new LanguageError("Primitive function call of incorrect unary arity");
            }
        }

        public Val Call (Context ctx, Val a, Val b) {
            if (fnBinary != null) {
                return fnBinary(ctx, a, b);
            } else if (fnVarArg != null) {
                return fnVarArg(ctx, new List<Val>() { a, b });
            } else {
                throw new LanguageError("Primitive function call of incorrect binary arity");
            }
        }

        public Val Call (Context ctx, List<Val> args) {
            if (fnVarArg != null) {
                return fnVarArg(ctx, args);
            } else {
                throw new LanguageError("Primitive function call of incorrect variable arity");
            }
        }
    }

    /// <summary>
    /// Built-in primitive functions, which all live in the core package.
    /// </summary>
    public class Primitive
    {
        public readonly string name;
        public readonly int minargs;
        public readonly bool exact; // if not exact, the function accepts arbitrary varargs
        public readonly Function fn;
        public readonly bool alwaysNotNull;
        public readonly bool hasSideEffects;

        public Primitive (string name, int minargs, bool exact, Function fn, bool alwaysNotNull = false, bool hasSideEffects = false) {
            this.name = name;
            this.minargs = minargs;
            this.exact = exact;
            this.fn = fn;
            this.alwaysNotNull = alwaysNotNull;
            this.hasSideEffects = hasSideEffects;
        }

        /// <summary> Calls the primitive function with argn operands waiting for it on the stack </summary>
        public Val Call (Context ctx, int argn, State state) {
            switch (argn) {
                case 0: {
                        return fn.Call(ctx);
                    }
                case 1: {
                        Val first = state.Pop();
                        return fn.Call(ctx, first);
                    }
                case 2: {
                        Val second = state.Pop();
                        Val first = state.Pop();
                        return fn.Call(ctx, first, second);
                    }
                default: {
                        List<Val> args = RemoveArgsFromStack(state, argn);
                        return fn.Call(ctx, args);
                    }
            }
        }

        private List<Val> RemoveArgsFromStack (State state, int count) {
            List<Val> result = new List<Val>();
            for (int i = 0; i < count; i++) {
                result.Add(state.Pop());
            }
            result.Reverse();
            return result;
        }
    }

}