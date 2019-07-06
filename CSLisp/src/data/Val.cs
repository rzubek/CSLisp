using CSLisp.Core;
using CSLisp.Error;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace CSLisp.Data
{
    /// <summary>
    /// Tagged struct that holds a variety of results:
    /// strings, symbols, numbers, bools, closures, and others.
    ///
    /// By using a tagged struct we avoid the need to box value types.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Val : IEquatable<Val>
    {
        public enum Type : int
        {
            // value types
            Nil,
            Bool,
            Int,
            Float,

            // reference types
            String,
            Symbol,
            Cons,
            Closure,
            ReturnAddress,
            Object
        }

        // value types need to live at a separate offset from reference types
        [FieldOffset(0)] public ulong rawvalue;
        [FieldOffset(0)] public bool vbool;
        [FieldOffset(0)] public int vint;
        [FieldOffset(0)] public float vfloat;

        [FieldOffset(8)] public string vstring;
        [FieldOffset(8)] public object rawobject;
        [FieldOffset(8)] public Symbol vsymbol;
        [FieldOffset(8)] public Cons vcons;
        [FieldOffset(8)] public Closure vclosure;
        [FieldOffset(8)] public ReturnAddress vreturn;

        [FieldOffset(16)] public Type type;

        public static readonly Val NIL = new Val() { type = Type.Nil };

        public Val (bool value) : this() { type = Type.Bool; vbool = value; }
        public Val (int value) : this() { type = Type.Int; vint = value; }
        public Val (float value) : this() { type = Type.Float; vfloat = value; }
        public Val (string value) : this() { type = Type.String; vstring = value; }
        public Val (Symbol value) : this() { type = Type.Symbol; vsymbol = value; }
        public Val (Cons value) : this() { type = Type.Cons; vcons = value; }
        public Val (Closure value) : this() { type = Type.Closure; vclosure = value; }
        public Val (ReturnAddress value) : this() { type = Type.ReturnAddress; vreturn = value; }
        public Val (object value) : this() { type = Type.Object; rawobject = value; }

        public bool IsNil => type == Type.Nil;
        public bool IsNotNil => type != Type.Nil;
        public bool IsAtom => type != Type.Cons;

        public bool IsNumber => type == Type.Int || type == Type.Float;

        public bool IsBool => type == Type.Bool;
        public bool IsInt => type == Type.Int;
        public bool IsFloat => type == Type.Float;
        public bool IsString => type == Type.String;
        public bool IsSymbol => type == Type.Symbol;
        public bool IsCons => type == Type.Cons;
        public bool IsClosure => type == Type.Closure;
        public bool IsReturnAddress => type == Type.ReturnAddress;
        public bool IsObject => type == Type.Object;

        public bool AsBool => type == Type.Bool ? vbool : throw new CompilerError("Value type was expected to be bool");
        public int AsInt => type == Type.Int ? vint : throw new CompilerError("Value type was expected to be int");
        public float AsFloat => type == Type.Float ? vfloat : throw new CompilerError("Value type was expected to be float");
        public string AsString => type == Type.String ? vstring : throw new CompilerError("Value type was expected to be string");
        public Symbol AsSymbol => type == Type.Symbol ? vsymbol : throw new CompilerError("Value type was expected to be symbol");
        public Cons AsCons => type == Type.Cons ? vcons : throw new CompilerError("Value type was expected to be cons");
        public Closure AsClosure => type == Type.Closure ? vclosure : throw new CompilerError("Value type was expected to be closure");
        public ReturnAddress AsReturnAddress => type == Type.ReturnAddress ? vreturn : throw new CompilerError("Value type was expected to be ret addr");
        public object AsObject => type == Type.Object ? rawobject : throw new CompilerError("Value type was expected to be object");

        public string AsStringOrNull => type == Type.String ? vstring : null;
        public Symbol AsSymbolOrNull => type == Type.Symbol ? vsymbol : null;
        public Cons AsConsOrNull => type == Type.Cons ? vcons : null;
        public Closure AsClosureOrNull => type == Type.Closure ? vclosure : null;

        public bool CastToBool => (type == Type.Bool) ? vbool : (type != Type.Nil) ? true : false;
        public float CastToFloat =>
            (type == Type.Int) ? vint :
            (type == Type.Float) ? vfloat :
            throw new CompilerError("Float cast applied to not a number");

        private bool IsValueType => type == Type.Bool || type == Type.Int || type == Type.Float;

        public static bool Equals (Val a, Val b) {
            if (a.type != b.type) { return false; }

            // same type, if it's a string we need to do a string equals
            if (a.type == Type.String) {
                return string.Equals(a.vstring, b.vstring, StringComparison.InvariantCulture);
            }

            // if it's a value type, simply compare the value data
            if (a.IsValueType) { return a.rawvalue == b.rawvalue; }

            // otherwise if it's a reference type, compare object reference
            return ReferenceEquals(a.rawobject, b.rawobject);
        }

        public bool Equals (Val other) => Equals(this, other);

        public static implicit operator Val (bool val) => new Val(val);
        public static implicit operator Val (int val) => new Val(val);
        public static implicit operator Val (float val) => new Val(val);
        public static implicit operator Val (string val) => new Val(val);
        public static implicit operator Val (Symbol val) => new Val(val);
        public static implicit operator Val (Cons val) => new Val(val);

        public override bool Equals (object obj) => (obj is Val) && Equals((Val)obj, this);
        public override int GetHashCode () => (int)type ^ (rawobject != null ? rawobject.GetHashCode() : ((int)rawvalue));

        private string DebugString => Print(this, true);
        public override string ToString () => Print(this);

        public static string Print (Val val, bool safe = false) {
            switch (val.type) {
                case Type.Nil:
                    return "()";
                case Type.Bool:
                    return val.vbool ? "#t" : "#f";
                case Type.Int:
                    return val.vint.ToString(CultureInfo.InvariantCulture);
                case Type.Float:
                    return val.vfloat.ToString(CultureInfo.InvariantCulture);
                case Type.String:
                    return "\"" + val.vstring + "\"";
                case Type.Symbol:
                    return val.vsymbol.fullName;
                case Type.Cons:
                    return StringifyCons(val.vcons);
                case Type.Closure:
                    return $"[Closure]";
                case Type.ReturnAddress:
                    return $"[Return to pc={val.vreturn.pc}]";
                case Type.Object:
                    return $"[Native {val.rawobject}]";
                default:
                    if (safe) {
                        return null;
                    }
                    throw new CompilerError("Unexpected value type: " + val.type);
            }
        }

        /// <summary> Helper function for cons cells </summary>
        private static string StringifyCons (Cons cell) {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");

            Val val = new Val(cell);
            while (val.IsNotNil) {
                Cons cons = val.AsConsOrNull;
                if (cons != null) {
                    sb.Append(cons.first.ToString());
                    if (cons.rest.IsNotNil) {
                        sb.Append(" ");
                    }
                    val = cons.rest;
                } else {
                    sb.Append(". ");
                    sb.Append(val.ToString());
                    val = Val.NIL;
                }
            }

            sb.Append(")");
            return sb.ToString();
        }

    }
}