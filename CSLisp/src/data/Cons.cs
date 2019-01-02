using CSLisp.Error;
using System.Collections.Generic;

namespace CSLisp.Data
{
    /// <summary>
    /// Cons cell, contains car and cdr elements.
    /// </summary>
    public class Cons
    {
        /// <summary> First value of this cons cell </summary>
        public Val car;

        /// <summary> Second value of this cons cell </summary>
        public Val cdr;

        public Cons (Val car, Val cdr) {
            this.car = car;
            this.cdr = cdr;
        }

        /// <summary> Shorthand for car, the first element of the list </summary>
        public Val first => car;

        /// <summary> Shorthand for the second element of the list </summary>
        public Val cadr => cdr.GetCons.car;
        /// <summary> Shorthand for the second element of the list </summary>
        public Val second => cdr.GetCons.car;

        /// <summary> Shorthand for the third element of the list </summary>
        public Val caddr => cddr.GetCons.car;
        /// <summary> Shorthand for the third element of the list </summary>
        public Val third => cddr.GetCons.car;

        /// <summary> Shorthand for the fourth element of the list </summary>
        public Val cadddr => cdddr.GetCons.car;
        /// <summary> Shorthand for the third element of the list </summary>
        public Val fourth => cdddr.GetCons.car;

        /// <summary> Shorthand for cdr, the sublist after the first element (so second element and beyond) </summary>
        public Val rest => cdr;
        /// <summary> Shorthand for cdr, the sublist after the first element (so second element and beyond) </summary>
        public Val afterFirst => cdr;

        /// <summary> Shorthand for the sublist after the second element (so third element and beyond) </summary>
        public Val cddr => cdr.GetCons.cdr;
        /// <summary> Shorthand for the sublist after the second element (so third element and beyond) </summary>
        public Val afterSecond => cdr.GetCons.cdr;

        /// <summary> Shorthand for the sublist after the third element (so fourth element and beyond) </summary>
        public Val cdddr => cddr.GetCons.cdr;
        /// <summary> Shorthand for the sublist after the third element (so fourth element and beyond) </summary>
        public Val afterThird => cddr.GetCons.cdr;

        /// <summary> 
		/// Helper function: converts an array of arguments to a cons list.
		/// Whether it's null-terminated or not depends on the existence of a "." in the penultimate position.
		/// </summary>
        public static Val MakeList (List<Val> values) {
            int len = values.Count;
            bool dotted = (len >= 3 && values[len - 2].IsSymbol && (values[len - 2].GetSymbol.fullName == "."));

            // the tail should be either the last value, or a cons containing the last value
            Val result =
                dotted ? values[len - 1] :
                len >= 1 ? new Val(new Cons(values[len - 1], Val.NIL)) :
                Val.NIL;

            int iterlen = dotted ? len - 3 : len - 2;
            for (int i = iterlen; i >= 0; i--) {
                result = new Val(new Cons(values[i], result));
            }
            return result;
        }

        /// <summary> Helper function: converts a single value to a cons list. </summary>
        public static Cons MakeList (Val first) => new Cons(first, Val.NIL);

        /// <summary> Helper function: converts a two-value pair to a cons list. </summary>
        public static Cons MakeList (Val first, Val second) => new Cons(first, new Cons(second, Val.NIL));

        /// <summary> Helper function: converts a cons list into a native list </summary>
        public static List<Val> ToNativeList (Val element) {
            List<Val> results = new List<Val>();
            while (element.IsNotNil) {
                if (element.IsAtom) {
                    throw new LanguageError("Only null-terminated lists of cons cells can be converted to arrays!");
                }
                Cons cons = element.GetCons;
                results.Add(cons.car);
                element = cons.cdr;
            }
            return results;
        }

        /// <summary> Returns true if the value is a cons cell </summary>
        public static bool IsCons (Val value) => value.IsCons;

        /// <summary> Returns true if the value is an atom, ie. not a cons cell </summary>
        public static bool IsAtom (Val value) => !value.IsCons;

        /// <summary> Returns true if the value is nil </summary>
        public static bool IsNil (Val value) => value.IsNil;

        /// <summary> Returns true if the value is a properly nil-terminated cons list </summary>
        public static bool IsList (Val value) {
            if (value.IsNil) { return true; }

            Cons cons = value.GetAsConsOrNull;
            while (cons != null) {
                if (cons.cdr.IsNil) { return true; } // found our terminating null
                cons = cons.cdr.GetAsConsOrNull;
            }
            return false;
        }

        /// <summary> Returns the number of cons cells in the list, starting at value. O(n) operation. </summary>
        public static int Length (Val value) => Length(value.GetAsConsOrNull);

        /// <summary> Returns the number of cons cells in the list, starting at value. O(n) operation. </summary>
        public static int Length (Cons cons) {
            int result = 0;
            while (cons != null) {
                result++;
                cons = cons.cdr.GetAsConsOrNull;
            }
            return result;
        }
    }
}