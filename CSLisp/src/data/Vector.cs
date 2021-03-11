using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CSLisp.Data
{
    /// <summary>
    /// The Vector type a sequence of Vals that can be accessed in constant time
    /// (although insertions and deletions are linear time).
    /// </summary>
    [DebuggerDisplay("{DebugString}")]
    public class Vector : List<Val>
    {
        public Vector (int capacity) : base(capacity) { }

        public Vector (IEnumerable<Val> elements) : base(elements) { }

        public Vector (Cons elements) : base(elements.ToNativeList()) { }

        /// <summary>
        /// Converts this Vector into a cons list.
        /// </summary>
        public Val ToCons () {
            Val result = Val.NIL;
            for (int i = Count - 1; i >= 0; i--) {
                result = new Cons(this[i], result);
            }

            return result;
        }

        /// <summary>
        /// Converts this Vector into a .NET List[object]
        /// </summary>
        public List<object> ToList () =>
            this.Select(val => val.AsBoxedValue).ToList();

        /// <summary>
        /// Converts this Vector into a .NET ArrayList
        /// </summary>
        public ArrayList ToArrayList () =>
            new ArrayList(ToList());

        /// <summary>
        /// Prints vector contents as a string with specified separator
        /// </summary>
        public string Print (string separator = " ") =>
            string.Join(separator, this.Select(elt => Val.Print(elt)).ToArray());


        private string DebugString => "Vector: " + Print(", ");
        public override string ToString () => DebugString;
    }
}