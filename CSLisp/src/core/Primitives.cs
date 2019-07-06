using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;
using System.Linq;

namespace CSLisp.Core
{
    public class Primitives
    {
        private static int _gensymIndex = 1;

        ///// <summary> Performs a left fold on the array: +, 0, [1, 2, 3] => (((0 + 1) + 2) + 3) </summary>
        private static Val FoldLeft (System.Func<Val, Val, Val> fn, Val baseElement, List<Val> elements) {
            var result = baseElement;
            for (int i = 0, len = elements.Count; i < len; i++) {
                result = fn(result, elements[i]);
            }
            return result;
        }

        ///// <summary> Performs a right fold on the array: +, 0, [1, 2, 3] => (1 + (2 + (3 + 0))) </summary>
        private static Val FoldRight (System.Func<Val, Val, Val> fn, Val baseElement, List<Val> elements) {
            var result = baseElement;
            for (int i = elements.Count - 1; i >= 0; i--) {
                result = fn(elements[i], result);
            }
            return result;
        }

        private static Dictionary<string, List<Primitive>> ALL_PRIMITIVES_DICT = new Dictionary<string, List<Primitive>>();
        private static readonly List<Primitive> ALL_PRIMITIVES_VECTOR = new List<Primitive>() {
            new Primitive("+", 2, true, new Function((ctx, a, b) => ValAdd(a, b)), true),
            new Primitive("-", 2, true, new Function((ctx, a, b) => ValSub(a, b)), true),
            new Primitive("*", 2, true, new Function((ctx, a, b) => ValMul(a, b)), true),
            new Primitive("/", 2, true, new Function((ctx, a, b) => ValDiv(a, b)), true),

            new Primitive("+", 3, false, new Function((Context ctx, List<Val> args) => 
                FoldLeft((a, b) => ValAdd(a, b), 0, args)), true),
            new Primitive("*", 3, false, new Function((Context ctx, List<Val> args) =>
                FoldLeft((a, b) => ValMul(a, b), 1, args)), true),

            new Primitive("=",  2, true, new Function((ctx, a, b) => Val.Equals(a, b))),
            new Primitive("!=", 2, true, new Function((ctx, a, b) => ! Val.Equals(a, b))),

            new Primitive("<",  2, true, new Function((ctx, a, b) => ValLT(a, b))),
            new Primitive("<=", 2, true, new Function((ctx, a, b) => ValLTE(a, b))),
            new Primitive(">",  2, true, new Function((ctx, a, b) => ValGT(a, b))),
            new Primitive(">=", 2, true, new Function((ctx, a, b) => ValGTE(a, b))),

            new Primitive("cons", 2, true, new Function((ctx, a, b) => new Cons(a, b)), true),
            new Primitive("list", 0, true, new Function((ctx) => Val.NIL), false),
            new Primitive("list", 1, true, new Function((ctx, a) => new Cons(a, Val.NIL)), true),
            new Primitive("list", 2, true, new Function((ctx, a, b) => new Cons(a, new Cons(b, Val.NIL))), true),
            new Primitive("list", 3, false, new Function((Context ctx, List<Val> args) => Cons.MakeList(args)), true),

            new Primitive("append", 1, false, new Function((Context ctx, List<Val> args) =>
                FoldRight(AppendHelper, Val.NIL, args))),

            new Primitive("length", 1, true, new Function((ctx, a) => Cons.Length(a)), true),

            new Primitive("not", 1, true, new Function((ctx, a) => !a.CastToBool)),
            new Primitive("null?", 1, true, new Function((ctx, a) => a.IsNil)),
            new Primitive("cons?", 1, true, new Function((ctx, a) => a.IsCons)),
            new Primitive("string?", 1, true, new Function((ctx, a) => a.IsString)),
            new Primitive("number?", 1, true, new Function((ctx, a) => a.IsNumber)),
            new Primitive("boolean?", 1, true, new Function((ctx, a) => a.IsBool)),
            new Primitive("atom?", 1, true, new Function((ctx, a) => !a.IsCons)),

            new Primitive("car", 1, true, new Function((ctx, a) => a.AsCons.first)),
            new Primitive("cdr", 1, true, new Function((ctx, a) => a.AsCons.rest)),
            new Primitive("cadr", 1, true, new Function((ctx, a) => a.AsCons.second)),
            new Primitive("cddr", 1, true, new Function((ctx, a) => a.AsCons.afterSecond)),
            new Primitive("caddr", 1, true, new Function((ctx, a) => a.AsCons.third)),
            new Primitive("cdddr", 1, true, new Function((ctx, a) => a.AsCons.afterThird)),

            new Primitive("nth", 2, true, new Function((ctx, a, n) => a.AsCons.GetNth(n.AsInt))),
            new Primitive("nth-tail", 2, true, new Function((ctx, a, n) => a.AsCons.GetNthTail(n.AsInt))),
            new Primitive("nth-cons", 2, true, new Function((ctx, a, n) => a.AsCons.GetNthCons(n.AsInt))),

            new Primitive("map", 2, true, new Function((ctx, a, b) => {
                Closure fn = a.AsClosure;
                Cons list = b.AsCons;
                return new Val(MapHelper(ctx, fn, list));
            }), false, true),
						
			// macroexpansion
			new Primitive("mx1", 1, true, new Function((ctx, exp) => ctx.compiler.MacroExpand1Step(exp))),
            new Primitive("mx", 1, true, new Function((ctx, exp) => ctx.compiler.MacroExpandFull(exp))),
			
			// helpers
			new Primitive("trace", 1, false, new Function((Context ctx, List<Val> args) => {
                System.Console.WriteLine(string.Join(" ", args.Select(val => Val.Print(val))));
                return Val.NIL;
            }), false, true ),

            new Primitive("gensym", 0, true, new Function((ctx) => GensymHelper(ctx, "GENSYM-"))),
            new Primitive("gensym", 1, true, new Function((ctx, a) => GensymHelper(ctx, a.AsStringOrNull))),
			
			// packages
			new Primitive("package-set", 1, true, new Function((ctx, a) => {
                string name = a.IsNil ? null : a.AsString; // nil package name == global package
                Package pkg = ctx.packages.Intern(name);
                ctx.packages.current = pkg;
                return a.IsNil ? Val.NIL : new Val(name);
            }), false, true),

            new Primitive("package-get", 0, true, new Function (ctx =>
                new Val(ctx.packages.current.name)),
            false, true),

            new Primitive("package-import", 1, false, new Function ((Context ctx, List<Val> names) => {
                foreach (Val a in names) {
                    string name = a.IsNil ? null : a.AsString;
                    ctx.packages.current.AddImport(ctx.packages.Intern(name));
                }
                return Val.NIL;
            }), false, true),

            new Primitive("package-imports", 0, true, new Function (ctx => {
                List<Val> imports = ctx.packages.current.ListImports();
                return Cons.MakeList(imports);
            }), false, true),

            new Primitive("package-export", 1, true, new Function ((Context ctx, Val a) => {
                Cons names = a.AsConsOrNull;
                while (names != null) {
                    Symbol symbol = names.first.AsSymbol;
                    symbol.exported = true;
                    names = names.rest.AsConsOrNull;
                }
                return Val.NIL;
            }), false, true),

            new Primitive("package-exports", 0, true, new Function (ctx => {
                List<Val> exports = ctx.packages.current.ListExports();
                return Cons.MakeList(exports);
            }), false, true),

        };


        /// <summary> 
        /// If f is a symbol that refers to a primitive, and it's not shadowed in the local environment,
        /// returns an appropriate instance of Primitive for that argument count.
        /// </summary>
        public static Primitive FindGlobal (Val f, Environment env, int nargs) {
            return (f.IsSymbol && (Environment.GetVariable(f.AsSymbol, env).IsNotValid))
                ? FindNary(f.AsSymbol.name, nargs)
                : null;
        }

        /// <summary> Helper function, searches based on name and argument count </summary>
        public static Primitive FindNary (string symbol, int nargs) {
            List<Primitive> primitives = ALL_PRIMITIVES_DICT[symbol];
            foreach (Primitive p in primitives) {
                if (symbol == p.name && (p.exact ? nargs == p.minargs : nargs >= p.minargs)) {
                    return p;
                }
            }
            return null;
        }

        /// <summary> Initializes the core package with stub functions for primitives </summary>
        public static void InitializeCorePackage (Package pkg) {

            // clear out and reinitialize the dictionary.
            // also, intern all primitives in their appropriate package
            ALL_PRIMITIVES_DICT = new Dictionary<string, List<Primitive>>();

            foreach (Primitive p in ALL_PRIMITIVES_VECTOR) {
                // dictionary update
                if (!ALL_PRIMITIVES_DICT.TryGetValue(p.name, out List<Primitive> v)) {
                    v = ALL_PRIMITIVES_DICT[p.name] = new List<Primitive>();
                }

                // add to the list of primitives of that name
                v.Add(p);

                // also intern in package, if it hasn't been interned yet
                if (pkg.Find(p.name, false) == null) {
                    Symbol name = pkg.Intern(p.name);
                    name.exported = true;
                    List<Instruction> instructions = new List<Instruction>() {
                        new Instruction(Opcode.PRIM, p.name),
                        new Instruction(Opcode.RETURN)};

                    string debug = $"primitive: {name.fullName}";
                    pkg.SetValue(name, new Val(new Closure(instructions, null, null, debug)));
                }
            }
        }

        /// <summary> Performs the append operation on two lists, by creating a new cons
        /// list that copies elements from the first value, and its tail is the second value </summary>
        private static Val AppendHelper (Val aval, Val bval) {

            Cons alist = aval.AsConsOrNull;
            Cons head = null, current = null, previous = null;

            // copy all nodes from a, set cdr of the last one to b
            while (alist != null) {
                current = new Cons(alist.first, Val.NIL);
                if (head == null) { head = current; }
                if (previous != null) { previous.rest = current; }
                previous = current;
                alist = alist.rest.AsConsOrNull;
            }

            if (current != null) {
                // a != () => head points to the first new node
                current.rest = bval;
                return head;
            } else {
                // a == (), we should return b
                return bval;
            }
        }

        /// <summary> Generates a new symbol </summary>
        private static Val GensymHelper (Context ctx, string prefix) {
            while (true) {
                string gname = prefix + _gensymIndex;
                _gensymIndex++;
                Package current = ctx.packages.current;
                if (current.Find(gname, false) == null) {
                    return new Val(current.Intern(gname));
                }
            };
        }

        /// <summary> Maps a function over elements of the list, and returns a new list with the results </summary>
        private static Cons MapHelper (Context ctx, Closure fn, Cons list) {

            Cons head = null;
            Cons current = null;
            Cons previous = null;

            // apply fn over all elements of the list, making a copy as we go
            while (list != null) {
                Val input = list.first;
                Val output = ctx.vm.Execute(fn, input);
                current = new Cons(output, Val.NIL);
                if (head == null) { head = current; }
                if (previous != null) { previous.rest = current; }
                previous = current;
                list = list.rest.AsConsOrNull;
            }

            return head;
        }

        /// <summary> Collapses a native path (expressed as a Cons list) into a fully qualified name </summary>
        private static string CollapseIntoNativeName (Cons path) {
            string name = "";
            while (path != null) {
                if (name.Length > 0) {
                    name += ".";
                }
                name += (path.first.AsSymbol).name;
                path = path.rest.AsCons;
            }
            return name;
        }

        private static Val ValAdd (Val a, Val b) {
            if (a.IsInt && b.IsInt) { return new Val(a.AsInt + b.AsInt); }
            if (a.IsNumber && b.IsNumber) { return new Val(a.CastToFloat + b.CastToFloat); }
            throw new LanguageError("Add applied to non-numbers");
        }

        private static Val ValSub (Val a, Val b) {
            if (a.IsInt && b.IsInt) { return new Val(a.AsInt - b.AsInt); }
            if (a.IsNumber && b.IsNumber) { return new Val(a.CastToFloat - b.CastToFloat); }
            throw new LanguageError("Add applied to non-numbers");
        }

        private static Val ValMul (Val a, Val b) {
            if (a.IsInt && b.IsInt) { return new Val(a.AsInt * b.AsInt); }
            if (a.IsNumber && b.IsNumber) { return new Val(a.CastToFloat * b.CastToFloat); }
            throw new LanguageError("Add applied to non-numbers");
        }

        private static Val ValDiv (Val a, Val b) {
            if (a.IsInt && b.IsInt) { return new Val(a.AsInt / b.AsInt); }
            if (a.IsNumber && b.IsNumber) { return new Val(a.CastToFloat / b.CastToFloat); }
            throw new LanguageError("Add applied to non-numbers");
        }

        private static Val ValLT (Val a, Val b) => a.CastToFloat < b.CastToFloat;
        private static Val ValLTE (Val a, Val b) => a.CastToFloat <= b.CastToFloat;
        private static Val ValGT (Val a, Val b) => a.CastToFloat > b.CastToFloat;
        private static Val ValGTE (Val a, Val b) => a.CastToFloat >= b.CastToFloat;
    }
}