using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;
using System.Linq;

namespace CSLisp.Core
{
    /// <summary>
    /// Stores the results of top-level compilation: a closure that is ready for execution,
    /// and a set of code block handles for code that was just compiled, for debugging purposes.
    /// </summary>
    public struct CompilationResults
    {
        public Closure closure;
        public List<CodeHandle> recents;

        public CompilationResults (Closure closure, List<CodeHandle> blocks) {
            this.closure = closure;
            this.recents = blocks;
        }
    }

    /// <summary>
    /// Compiles source s-expression into bytecode.
    /// </summary>
    public class Compiler
    {
        /// <summary>
        /// Compilation state for the expression being compiled 
        ///
        /// <p> Val and More flags are used for tail-call optimization. "Val" is true when 
        /// the expression returns a value that's then used elsewhere. "More" is false when 
        /// the expression represents the final value, true if there is more to compute
        /// (this determines whether we need to jump and return, or just jump)
        /// 
        /// <p> Examples, when compiling expression X:
        /// <ul>
        /// <li> val = t, more = t ... (if X y z) or (f X y)      </li>
        /// <li> val = t, more = f ... (if p X z) or (begin y X)  </li>
        /// <li> val = f, more = t ... (begin X y)                </li>
        /// <li> val = f, more = f ... impossible                 </li>
        /// </ul>
        /// </summary>
        private struct State
        {
            private bool _val, _more;

            public bool IsUnused => !_val;
            public bool IsFinal => !_more;

            public static readonly State UsedFinal = new State { _val = true, _more = false };
            public static readonly State UsedNonFinal = new State { _val = true, _more = true };
            public static readonly State NotUsedNonFinal = new State { _val = false, _more = true };
        }

        /// <summary> Label counter for each separate compilation block </summary>
        private int _labelNum = 0;

        /// <summary> Internal execution context </summary>
        private readonly Context _ctx = null;

        // some helpful symbol constants, interned only once at the beginning
        private readonly Symbol _quote;
        private readonly Symbol _begin;
        private readonly Symbol _set;
        private readonly Symbol _if;
        private readonly Symbol _ifStar;
        private readonly Symbol _while;
        private readonly Symbol _lambda;
        private readonly Symbol _defmacro;

        public Compiler (Context ctx) {
            var global = ctx.packages.global;
            _quote = global.Intern("quote");
            _begin = global.Intern("begin");
            _set = global.Intern("set!");
            _if = global.Intern("if");
            _ifStar = global.Intern("if*");
            _while = global.Intern("while");
            _lambda = global.Intern("lambda");
            _defmacro = global.Intern("defmacro");

            _ctx = ctx;
        }

        /// <summary>
        /// Top level compilation entry point. Compiles the expression x given an empty environment.
        /// Returns the newly compiled lambda for execution, and a list of all recently
        /// compiled code blocks for debugging purposes.
        /// </summary>
        public CompilationResults Compile (Val x) {
            var before = _ctx.code.LastHandle;
            _labelNum = 0;
            var closure = CompileLambda(Val.NIL, new Cons(x, Val.NIL), null);
            var after = _ctx.code.LastHandle;

            List<CodeHandle> blocks = new List<CodeHandle>();
            for (int i = before.index + 1; i <= after.index; i++) { blocks.Add(new CodeHandle(i)); }

            return new CompilationResults(closure, blocks);
        }

        /// <summary> 
        /// Compiles the expression x, given the environment env, into a vector of instructions.
        /// </summary>
        private List<Instruction> Compile (Val x, Environment env, State st) {

            // check if macro
            if (IsMacroApplication(x)) {
                return Compile(MacroExpandFull(x), env, st);
            }

            if (x.IsSymbol) {       // check if symbol
                return CompileVariable(x.AsSymbol, env, st);
            }

            if (x.IsAtom) {         // check if it's not a list
                return CompileConstant(x, st);
            }

            // it's not an atom, it's a list, deal with it.
            VerifyExpression(Cons.IsList(x), "Non-list expression detected!");
            Cons cons = x.AsConsOrNull;
            Symbol name = cons.first.AsSymbolOrNull;

            if (name == _quote) {    // (quote value)
                VerifyArgCount(cons, 1);
                return CompileConstant(cons.second, st); // second element is the constant
            }
            if (name == _begin) {    // (begin ...)
                return CompileBegin(cons.rest, env, st);
            }
            if (name == _set) {      // (set! symbol-name value)
                VerifyArgCount(cons, 2);
                VerifyExpression(cons.second.IsSymbol, "Invalid lvalue in set!, must be a symbol, got: ", cons.second);
                return CompileVarSet(cons.second.AsSymbol, cons.third, env, st);
            }
            if (name == _if) {       // (if pred then else) or (if pred then)
                VerifyArgCount(cons, 2, 3);
                return CompileIf(
                    cons.second,     // pred
                    cons.third,      // then
                    (cons.afterThird.IsNotNil ? cons.fourth : Val.NIL), // else
                    env, st);
            }
            if (name == _ifStar) {   // (if *pred else)
                VerifyArgCount(cons, 2);
                return CompileIfStar(
                    cons.second,    // pred
                    cons.third,     // else
                    env, st);
            }
            if (name == _while) {    // (while pred body ...)
                Cons body = cons.afterSecond.AsConsOrNull;
                return CompileWhile(cons.second, body, env, st);
            }
            if (name == _lambda) {   // (lambda (args...) body...)
                if (st.IsUnused) {
                    return null;    // it's not used, don't compile
                } else {
                    Cons body = cons.afterSecond.AsConsOrNull;
                    Closure f = CompileLambda(cons.second, body, env);
                    //string debug = $"#{f.code.index} : " + Val.DebugPrint(cons.afterSecond);
                    string debug = Val.DebugPrint(cons.afterSecond);
                    return Merge(
                        Emit(Opcode.MAKE_CLOSURE, new Val(f), Val.NIL, debug),
                        EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL)));
                }
            }
            if (name == _defmacro) {
                return CompileAndInstallMacroDefinition(cons.rest.AsConsOrNull, env, st);
            }

            return CompileFunctionCall(cons.first, cons.rest.AsConsOrNull, env, st);
        }

        /// <summary> 
        /// Verifies arg count of the expression (list of operands). 
        /// Min and max are inclusive; default value of max (= -1) is a special value,
        /// causes max to be treated as equal to min (ie., tests for arg count == min)
        /// </summary>
        private void VerifyArgCount (Cons cons, int min, int max = -1) {
            max = (max >= 0) ? max : min;  // default value means: max == min
            int count = Cons.Length(cons.rest);
            if (count < min || count > max) {
                throw new CompilerError("Invalid argument count in expression " + cons +
                    ": " + count + " supplied, expected in range [" + min + ", " + max + "]");
            }
        }

        /// <summary> Verifies that the expression is true, throws the specified error otherwise. </summary>
        private void VerifyExpression (bool condition, string message, Val? val = null) {
            if (!condition) {
                throw new CompilerError(message + (val.HasValue ? (" " + val.Value.type) : ""));
            }
        }

        /// <summary> Returns true if the given value is a macro </summary>
        private bool IsMacroApplication (Val x) {
            var cons = x.AsConsOrNull;
            return
                cons != null &&
                cons.first.IsSymbol &&
                cons.first.AsSymbol.pkg.HasMacro(cons.first.AsSymbol);
        }

        /// <summary> Performs compile-time macroexpansion, one-level deep </summary>
        public Val MacroExpand1Step (Val exp) {
            Cons cons = exp.AsConsOrNull;
            if (cons == null || !cons.first.IsSymbol) { return exp; } // something unexpected

            Symbol name = cons.first.AsSymbol;
            Macro macro = name.pkg.GetMacro(name);
            if (macro == null) { return exp; } // no such macro, ignore

            // now we execute the macro at compile time, in the same context...
            Val result = _ctx.vm.Execute(macro.body, Cons.ToNativeList(cons.rest).ToArray());
            return result;
        }

        /// <summary> Performs compile-time macroexpansion, fully recursive </summary>
        public Val MacroExpandFull (Val exp) {
            Val expanded = MacroExpand1Step(exp);
            Cons cons = expanded.AsConsOrNull;
            if (cons == null || !cons.first.IsSymbol) { return expanded; } // nothing more to expand

            // if we're expanding a list, replace each element recursively
            while (cons != null) {
                Cons elt = cons.first.AsConsOrNull;
                if (elt != null && elt.first.IsSymbol) {
                    Val substitute = MacroExpandFull(cons.first);
                    cons.first = substitute;
                }
                cons = cons.rest.AsConsOrNull;
            }

            return expanded;
        }

        /// <summary> Compiles a variable lookup </summary>
        private List<Instruction> CompileVariable (Symbol x, Environment env, State st) {
            if (st.IsUnused) { return null; }

            var pos = Environment.GetVariable(x, env);
            bool isLocal = pos.IsValid;
            return Merge(
                (isLocal ?
                    Emit(Opcode.LOCAL_GET, pos.frameIndex, pos.symbolIndex, Val.DebugPrint(x)) :
                    Emit(Opcode.GLOBAL_GET, x)),
                EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL)));
        }

        /// <summary> Compiles a constant, if it's actually used elsewhere </summary>
        private List<Instruction> CompileConstant (Val x, State st) {
            if (st.IsUnused) { return null; }

            return Merge(
                Emit(Opcode.PUSH_CONST, x, Val.NIL),
                EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL)));
        }

        /// <summary> Compiles a sequence defined by a BEGIN - we pop all values, except for the last one </summary>
        private List<Instruction> CompileBegin (Val exps, Environment env, State st) {
            if (exps.IsNil) {
                return CompileConstant(Val.NIL, st); // (begin)
            }

            Cons cons = exps.AsConsOrNull;
            VerifyExpression(cons != null, "Unexpected value passed to begin block, instead of a cons:", exps);

            if (cons.rest.IsNil) {  // length == 1
                return Compile(cons.first, env, st);
            } else {
                return Merge(
                    Compile(cons.first, env, State.NotUsedNonFinal),  // note: not the final expression, set val = f, more = t
                    CompileBegin(cons.rest, env, st));
            }
        }

        /// <summary> Compiles a variable set </summary>
        private List<Instruction> CompileVarSet (Symbol x, Val value, Environment env, State st) {
            var pos = Environment.GetVariable(x, env);
            bool isLocal = pos.IsValid;
            return Merge(
                Compile(value, env, State.UsedNonFinal),
                (isLocal ?
                        Emit(Opcode.LOCAL_SET, pos.frameIndex, pos.symbolIndex, Val.DebugPrint(x)) :
                        Emit(Opcode.GLOBAL_SET, x)),
                EmitIf(st.IsUnused, Emit(Opcode.STACK_POP)),
                EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL))
                );
        }

        /// <summary> Compiles an if statement (fun!) </summary>
        private List<Instruction> CompileIf (Val pred, Val then, Val els, Environment env, State st) {
            // (if #f x y) => y
            if (pred.IsBool && !pred.AsBool) { return Compile(els, env, st); }

            // (if #t x y) => x, or (if 5 ...) or (if "foo" ...)
            bool isConst = (pred.IsBool) || (pred.IsNumber) || (pred.IsString);
            if (isConst) { return Compile(then, env, st); }

            // actually produce the code for if/then/else clauses
            // note that those clauses will already contain a return opcode if they're final.
            List<Instruction> PredCode = Compile(pred, env, State.UsedNonFinal);
            List<Instruction> ThenCode = Compile(then, env, st);
            List<Instruction> ElseCode = els.IsNotNil ? Compile(els, env, st) : CompileConstant(els, st);

            // (if p x x) => (begin p x)
            if (CodeEquals(ThenCode, ElseCode)) {
                return Merge(
                    Compile(pred, env, State.NotUsedNonFinal),
                    ElseCode);
            }

            // (if p x y) => p (FJUMP L1) x L1: y 
            //         or    p (FJUMP L1) x (JUMP L2) L1: y L2:
            // depending on whether this is the last exp, or if there's more
            if (st.IsFinal) {
                string l1 = MakeLabel();
                return Merge(
                    PredCode,
                    Emit(Opcode.JMP_IF_FALSE, l1),
                    ThenCode,
                    Emit(Opcode.LABEL, l1),
                    ElseCode);
            } else {
                string l1 = MakeLabel();
                string l2 = MakeLabel();
                return Merge(
                    PredCode,
                    Emit(Opcode.JMP_IF_FALSE, l1),
                    ThenCode,
                    Emit(Opcode.JMP_TO_LABEL, l2),
                    Emit(Opcode.LABEL, l1),
                    ElseCode,
                    Emit(Opcode.LABEL, l2));
            }
        }

        /// <summary> Compiles an if* statement </summary>
        private List<Instruction> CompileIfStar (Val pred, Val els, Environment env, State st) {

            // (if* x y) will return x if it's not false, otherwise it will return y

            // (if* #f x) => x
            if (pred.IsBool && !pred.AsBool) {
                return Compile(els, env, st);
            }

            List<Instruction> PredCode = Compile(pred, env, State.UsedNonFinal);

            var elseState = st.IsFinal ? State.UsedFinal : State.UsedNonFinal;
            List<Instruction> ElseCode = els.IsNotNil ? Compile(els, env, elseState) : null;

            // (if* p x) => p (DUPE) (TJUMP L1) (POP) x L1: (POP?)
            string l1 = MakeLabel();
            return Merge(
                PredCode,
                Emit(Opcode.DUPLICATE),
                Emit(Opcode.JMP_IF_TRUE, l1),
                Emit(Opcode.STACK_POP),
                ElseCode,
                Emit(Opcode.LABEL, l1),
                EmitIf(st.IsUnused, Emit(Opcode.STACK_POP)),
                EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL)));
        }

        /// <summary> Compiles a while loop </summary>
        private List<Instruction> CompileWhile (Val pred, Cons body, Environment env, State st) {
            // (while p ...) => (PUSH '()) L1: p (FJUMP L2) (POP) (begin ...) (JUMP L1) L2:
            List<Instruction> PredCode = Compile(pred, env, State.UsedNonFinal);
            List<Instruction> BodyCode = CompileBegin(body, env, State.UsedNonFinal); // keep result on stack

            string l1 = MakeLabel(), l2 = MakeLabel();
            return Merge(
                Emit(Opcode.PUSH_CONST, Val.NIL),
                Emit(Opcode.LABEL, l1),
                PredCode,
                Emit(Opcode.JMP_IF_FALSE, l2),
                Emit(Opcode.STACK_POP),
                BodyCode,
                Emit(Opcode.JMP_TO_LABEL, l1),
                Emit(Opcode.LABEL, l2),
                EmitIf(st.IsUnused, Emit(Opcode.STACK_POP)),
                EmitIf(st.IsFinal, Emit(Opcode.RETURN_VAL)));
        }

        /// <summary> Compiles code to produce a new closure </summary>
        private Closure CompileLambda (Val args, Cons body, Environment env) {
            Environment newEnv = Environment.Make(MakeTrueList(args), env);
            List<Instruction> instructions = Merge(
                EmitArgs(args, newEnv.DebugPrintSymbols()),
                CompileBegin(new Val(body), newEnv, State.UsedFinal));

            var debug = newEnv.DebugPrintSymbols() + " => " + Val.DebugPrint(body);
            CodeHandle handle = _ctx.code.AddBlock(Assemble(instructions), debug);
            return new Closure(handle, env, args.AsConsOrNull, "");
        }

        /// <summary> Compile a list, leaving all elements on the stack </summary>
        private List<Instruction> CompileList (Cons exps, Environment env) =>
            (exps == null)
                ? null
                : Merge(
                    Compile(exps.first, env, State.UsedNonFinal),
                    CompileList(exps.rest.AsConsOrNull, env));

        /// <summary> 
        /// Compiles a macro, and sets the given symbol to point to it. NOTE: unlike all other expressions,
        /// which are executed by the virtual machine, this happens immediately, during compilation.
        /// </summary>
        private List<Instruction> CompileAndInstallMacroDefinition (Cons cons, Environment env, State st) {

            // example: (defmacro foo (x) (+ x 1))
            Symbol name = cons.first.AsSymbol;
            Cons args = cons.second.AsCons;
            Cons bodylist = cons.afterSecond.AsConsOrNull;
            Closure body = CompileLambda(new Val(args), bodylist, env);
            Macro macro = new Macro(name, args, body);

            // install it in the package
            name.pkg.SetMacro(name, macro);
            return CompileConstant(Val.NIL, st);
        }

        /// <summary> Compile the application of a function to arguments </summary>
        private List<Instruction> CompileFunctionCall (Val f, Cons args, Environment env, State st) {
            if (f.IsCons) {
                var fcons = f.AsCons;
                if (fcons.first.IsSymbol && fcons.first.AsSymbol.fullName == "lambda" && fcons.second.IsNil) {
                    // ((lambda () body)) => (begin body)
                    VerifyExpression(args == null, "Too many arguments supplied!");
                    return CompileBegin(fcons.afterSecond, env, st);
                }
            }

            if (st.IsFinal) {
                // function call as rename plus goto
                return Merge(
                    CompileList(args, env),
                    Compile(f, env, State.UsedNonFinal),
                    Emit(Opcode.JMP_CLOSURE, Cons.Length(args)));
            } else {
                // need to save the continuation point
                string k = MakeLabel("R");
                return Merge(
                    Emit(Opcode.SAVE_RETURN, k),
                    CompileList(args, env),
                    Compile(f, env, State.UsedNonFinal),
                    Emit(Opcode.JMP_CLOSURE, Cons.Length(args)),
                    Emit(Opcode.LABEL, k),
                    EmitIf(st.IsUnused, Emit(Opcode.STACK_POP)));
            }
        }

        /// <summary> Generates an appropriate ARGS or ARGSDOT sequence, making a new stack frame </summary>
        private List<Instruction> EmitArgs (Val args, string debug, int nSoFar = 0) {
            // recursively detect whether it's a list or ends with a dotted cons, and generate appropriate arg

            // terminal case
            if (args.IsNil) { return Emit(Opcode.MAKE_ENV, nSoFar, debug); }        // (lambda (a b c) ...)
            if (args.IsSymbol) { return Emit(Opcode.MAKE_ENVDOT, nSoFar, debug); }  // (lambda (a b . c) ...)

            // if not at the end, recurse
            var cons = args.AsConsOrNull;
            if (cons != null && cons.first.IsSymbol) { return EmitArgs(cons.rest, debug, nSoFar + 1); }

            throw new CompilerError("Invalid argument list");           // (lambda (a b 5 #t) ...) or some other nonsense
        }

        /// <summary> Converts a dotted cons list into a proper non-dotted one </summary>
        private Cons MakeTrueList (Val dottedList) {

            // we reached a terminating nil - return as is
            if (dottedList.IsNil) { return null; }

            // we reached a terminating cdr in a dotted pair - convert it
            if (dottedList.IsAtom) { return new Cons(dottedList, Val.NIL); }

            var cons = dottedList.AsCons;
            return new Cons(cons.first, MakeTrueList(cons.rest)); // keep recursing
        }

        /// <summary> Generates a sequence containing a single instruction </summary>
        private List<Instruction> Emit (Opcode type, Val first, Val second, string debug = null) =>
            new List<Instruction>() { new Instruction(type, first, second, debug) };

        /// <summary> Generates a sequence containing a single instruction </summary>
        private List<Instruction> Emit (Opcode type, Val first, string debug = null) =>
            new List<Instruction>() { new Instruction(type, first, debug) };

        /// <summary> Generates a sequence containing a single instruction with no arguments </summary>
        private List<Instruction> Emit (Opcode type) =>
            new List<Instruction>() { new Instruction(type) };


        /// <summary> Creates a new unique label </summary>
        private string MakeLabel (string prefix = "L") =>
            prefix + _labelNum++.ToString();

        /// <summary> Merges sequences of instructions into a single sequence </summary>
        private List<Instruction> Merge (params List<Instruction>[] elements) =>
            elements.Where(list => list != null).SelectMany(instr => instr).ToList();

        /// <summary> Returns the value if the condition is true, null if it's false </summary>
        private List<Instruction> EmitIf (bool test, List<Instruction> value) => test ? value : null;

        /// <summary> Returns the value if the condition is false, null if it's true </summary>
        private List<Instruction> EmitIfNot (bool test, List<Instruction> value) => !test ? value : null;

        /// <summary> Compares two code sequences, and returns true if they're equal </summary>
        private bool CodeEquals (List<Instruction> a, List<Instruction> b) {
            if (a == null && b == null) { return true; }
            if (a == null || b == null || a.Count != b.Count) { return false; }

            for (int i = 0; i < a.Count; i++) {
                if (!Instruction.Equal(a[i], b[i])) {
                    return false;
                }
            }
            return true;
        }

        /// <summary> 
        /// "Assembles" the compiled code, by resolving label references and converting them to index offsets. 
        /// Modifies the code data structure in place, and returns it back to the caller.
        /// </summary>
        private List<Instruction> Assemble (List<Instruction> code) {
            var positions = new LabelPositions(code);

            for (int i = 0; i < code.Count; i++) {
                Instruction inst = code[i];

                if (inst.IsJump) {
                    int pos = positions.FindPosition(inst.first);
                    if (pos >= 0) {
                        inst.UpdateJumpDestination(pos);
                    } else {
                        throw new CompilerError($"Can't find jump label {inst.first} during assembly");
                    }
                }
            }

            return code;
        }

        /// <summary>
        /// Temporary data structure used during assembly: holds code positions for all labels
        /// </summary>
        private class LabelPositions : Dictionary<string, int>
        {
            public LabelPositions (List<Instruction> code) {
                for (int i = 0; i < code.Count; i++) {
                    Instruction inst = code[i];
                    if (inst.type == Opcode.LABEL) {
                        string label = inst.first.AsString;
                        this[label] = i;
                    }
                }
            }

            /// <summary> Returns code position of the given label, or -1 if not found or the value is not a label. </summary>
            public int FindPosition (Val label) {
                if (!label.IsString) { return -1; }
                return TryGetValue(label.AsString, out int pos) ? pos : -1;
            }
        }

    }

}