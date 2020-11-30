using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;

namespace CSLisp.Core
{
    public delegate Val FnThunk (Context ctx);
    public delegate Val FnUnary (Context ctx, Val a);
    public delegate Val FnBinary (Context ctx, Val a, Val b);
    public delegate Val FnTernary (Context ctx, Val a, Val b, Val c);
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
        public FnTernary fnTernary;
        public FnVarArg fnVarArg;

        public Function (FnThunk fn) { fnThunk = fn; }
        public Function (FnUnary fn) { fnUnary = fn; }
        public Function (FnBinary fn) { fnBinary = fn; }
        public Function (FnTernary fn) { fnTernary = fn; }
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

        public Val Call (Context ctx, Val a, Val b, Val c) {
            if (fnTernary != null) {
                return fnTernary(ctx, a, b, c);
            } else if (fnVarArg != null) {
                return fnVarArg(ctx, new List<Val>() { a, b, c });
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
    /// Describes whether a primitive function has constant or variable number of arguments
    /// </summary>
    public enum FnType { ConstArgs, VarArgs };

    /// <summary>
    /// Describes whether a primitive function may cause side effects, or whether it's a pure function.
    /// Pure functions may be optimized away if their outputs are never consumed.
    /// </summary>
    public enum SideFx { None, Possible }

    /// <summary>
    /// Built-in primitive functions, which all live in the core package.
    /// </summary>
    public class Primitive
    {
        public readonly string name;
        public readonly int minargs;
        public readonly Function fn;
        public readonly FnType argsType; // is this a function with exact or variable number of arguments?
        public readonly SideFx sideFx;   // does this primitive cause side effects? if so, it should never be optimized away

        public Primitive (string name, int minargs, Function fn, FnType argsType = FnType.ConstArgs, SideFx sideFx = SideFx.None)
        {
            this.name = name;
            this.minargs = minargs;
            this.fn = fn;
            this.argsType = argsType;
            this.sideFx = sideFx;
        }

        public bool IsExact => argsType == FnType.ConstArgs;
        public bool IsVarArg => argsType == FnType.VarArgs;

        public bool HasSideEffects => sideFx == SideFx.Possible;
        public bool IsPureFunction => sideFx == SideFx.None;

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
                case 3: {
                        Val third = state.Pop();
                        Val second = state.Pop();
                        Val first = state.Pop();
                        return fn.Call(ctx, first, second, third);
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