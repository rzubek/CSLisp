using CSLisp.Core;
using CSLisp.Error;
using System;
using System.Diagnostics;
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
    [DebuggerDisplay("{DebugString}")]
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct Val : IEquatable<Val>
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
            Vector,
            Closure,
            ReturnAddress,
            Object
        }

        // value types need to live at a separate offset from reference types
        [FieldOffset(0)] public readonly ulong rawvalue;
        [FieldOffset(0)] public readonly bool vbool;
        [FieldOffset(0)] public readonly int vint;
        [FieldOffset(0)] public readonly float vfloat;

        [FieldOffset(8)] public readonly string vstring;
        [FieldOffset(8)] public readonly object rawobject;
        [FieldOffset(8)] public readonly Symbol vsymbol;
        [FieldOffset(8)] public readonly Cons vcons;
        [FieldOffset(8)] public readonly Vector vvector;
        [FieldOffset(8)] public readonly Closure vclosure;
        [FieldOffset(8)] public readonly ReturnAddress vreturn;

        [FieldOffset(16)] public readonly Type type;

        public static readonly Val NIL = new Val(Type.Nil);

        private Val (Type type) : this() { this.type = type; }

        public Val (bool value) : this() { type = Type.Bool; vbool = value; }
        public Val (int value) : this() { type = Type.Int; vint = value; }
        public Val (float value) : this() { type = Type.Float; vfloat = value; }
        public Val (string value) : this() { type = Type.String; vstring = value; }
        public Val (Symbol value) : this() { type = Type.Symbol; vsymbol = value; }
        public Val (Cons value) : this() { type = Type.Cons; vcons = value; }
        public Val (Vector value) : this() { type = Type.Vector; vvector = value; }
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
        public bool IsVector => type == Type.Vector;
        public bool IsClosure => type == Type.Closure;
        public bool IsReturnAddress => type == Type.ReturnAddress;
        public bool IsObject => type == Type.Object;

        public bool AsBool => type == Type.Bool ? vbool : throw new CompilerError("Value type was expected to be bool");
        public int AsInt => type == Type.Int ? vint : throw new CompilerError("Value type was expected to be int");
        public float AsFloat => type == Type.Float ? vfloat : throw new CompilerError("Value type was expected to be float");
        public string AsString => type == Type.String ? vstring : throw new CompilerError("Value type was expected to be string");
        public Symbol AsSymbol => type == Type.Symbol ? vsymbol : throw new CompilerError("Value type was expected to be symbol");
        public Cons AsCons => type == Type.Cons ? vcons : throw new CompilerError("Value type was expected to be cons");
        public Vector AsVector => type == Type.Vector ? vvector : throw new CompilerError("Value type was expected to be vector");
        public Closure AsClosure => type == Type.Closure ? vclosure : throw new CompilerError("Value type was expected to be closure");
        public ReturnAddress AsReturnAddress => type == Type.ReturnAddress ? vreturn : throw new CompilerError("Value type was expected to be ret addr");
        public object AsObject => type == Type.Object ? rawobject : throw new CompilerError("Value type was expected to be object");

        public string AsStringOrNull => type == Type.String ? vstring : null;
        public Symbol AsSymbolOrNull => type == Type.Symbol ? vsymbol : null;
        public Cons AsConsOrNull => type == Type.Cons ? vcons : null;
        public Vector AsVectorOrNull => type == Type.Vector ? vvector : null;
        public Closure AsClosureOrNull => type == Type.Closure ? vclosure : null;
        public object AsObjectOrNull => type == Type.Object ? rawobject : null;

        public T GetObjectOrNull<T> () where T : class =>
            type == Type.Object && rawobject is T obj ? obj : null;

        public object AsBoxedValue =>
            type switch {
                Type.Nil => null,
                Type.Bool => vbool,
                Type.Int => vint,
                Type.Float => vfloat,
                Type.String => vstring,
                Type.Symbol => vsymbol,
                Type.Cons => vcons,
                Type.Vector => vvector,
                Type.Closure => vclosure,
                Type.ReturnAddress => vreturn,
                Type.Object => rawobject,
                _ => throw new LanguageError("Unexpected value type: " + type),
            };

        public static Val TryUnbox (object boxed) =>
            boxed switch {
                null => NIL,
                bool bval => bval,
                int ival => ival,
                float fval => fval,
                string sval => sval,
                Symbol symval => symval,
                Cons cval => cval,
                Vector vval => vval,
                Closure closval => closval,
                _ => new Val(boxed),
            };

        public bool CastToBool => (type == Type.Bool) ? vbool : (type != Type.Nil);
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

        public static bool operator == (Val a, Val b) => Equals(a, b);
        public static bool operator != (Val a, Val b) => !Equals(a, b);

        public static implicit operator Val (bool val) => new Val(val);
        public static implicit operator Val (int val) => new Val(val);
        public static implicit operator Val (float val) => new Val(val);
        public static implicit operator Val (string val) => new Val(val);
        public static implicit operator Val (Symbol val) => new Val(val);
        public static implicit operator Val (Cons val) => new Val(val);
        public static implicit operator Val (Vector val) => new Val(val);
        public static implicit operator Val (Closure val) => new Val(val);

        public override bool Equals (object obj) => (obj is Val val) && Equals(val, this);
        public override int GetHashCode () => (int) type ^ (rawobject != null ? rawobject.GetHashCode() : ((int) rawvalue));

        private string DebugString => $"{Print(this, false)} [{type}]";
        public override string ToString () => Print(this, true);

        public static string DebugPrint (Val val) => Print(val, false);
        public static string Print (Val val) => Print(val, true);

        private static string Print (Val val, bool fullName) {
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
                    return fullName ? val.vsymbol.fullName : val.vsymbol.name;
                case Type.Cons:
                    return StringifyCons(val.vcons, fullName);
                case Type.Vector: {
                        var elements = val.vvector.Print(" ");
                        return $"[Vector {elements}]";
                    }
                case Type.Closure:
                    return string.IsNullOrEmpty(val.vclosure.name) ? "[Closure]" : $"[Closure/{val.vclosure.name}]";
                case Type.ReturnAddress:
                    return $"[{val.vreturn.debug}/{val.vreturn.pc}]";
                case Type.Object: {
                        var typedesc = val.rawobject == null ? "null" : $"{val.rawobject.GetType()} {val.rawobject}";
                        return $"[Native {typedesc}]";
                    }
                default:
                    throw new CompilerError("Unexpected value type: " + val.type);
            }
        }

        /// <summary> Helper function for cons cells </summary>
        private static string StringifyCons (Cons cell, bool fullName) {
            StringBuilder sb = new StringBuilder();
            sb.Append('(');

            Val val = new Val(cell);
            while (val.IsNotNil) {
                Cons cons = val.AsConsOrNull;
                if (cons != null) {
                    sb.Append(Print(cons.first, fullName));
                    if (cons.rest.IsNotNil) {
                        sb.Append(' ');
                    }
                    val = cons.rest;
                } else {
                    sb.Append(". ");
                    sb.Append(Print(val, fullName));
                    val = NIL;
                }
            }

            sb.Append(')');
            return sb.ToString();
        }

    }
}