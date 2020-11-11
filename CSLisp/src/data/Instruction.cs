using CSLisp.Error;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CSLisp.Data
{
    /// <summary> Enum of instructions produced by the compiler </summary>
    public enum Opcode
    {
        /// <summary>
        /// Just a label, doesn't do anything, only used during compilation
        /// </summary>
        LABEL = 0,

        /// <summary>
        /// PUSH_CONST x - pushes x onto the stack
        /// </summary>
        PUSH_CONST = 1,

        /// <summary>
        /// LOCAL_GET i j -  push local variable onto the stack, where <b>i</b> is the frame index relative
        ///                  to current frame and <b>j</b> is the symbol index 
        /// </summary>
        LOCAL_GET = 2,

        /// <summary>
        /// LOCAL_SET i, j - set local variable from what's on top of the stack, without popping from the stack,
        ///                  where <b>i</b> is the frame index relative to current frame and <b>j</b> is the symbol index 
        /// </summary>
        LOCAL_SET = 3,

        /// <summary>
        /// GLOBAL_GET name - push global variable onto the stack
        /// </summary>
        GLOBAL_GET = 4,

        /// <summary>
        /// GLOBAL_SET name - set global variable from what's on top of the stack, without popping the stack
        /// </summary>
        GLOBAL_SET = 5,

        /// <summary>
        /// STACK_POP - pops the top value from the stack, discarding it
        /// </summary>
        STACK_POP = 6,

        /// <summary>
        /// DUPLICATE - duplicates (pushes a second copy of) the topmost value on the stack
        /// </summary>
        DUPLICATE = 7,

        /// <summary>
        /// JMP_IF_TRUE label - pop the stack, and jump to label if the value is true
        /// </summary>
        JMP_IF_TRUE = 8,

        /// <summary>
        /// JMP_IF_FALSE label - pop the stack, and jump to label if the value is not true
        /// </summary>
        JMP_IF_FALSE = 9,

        /// <summary>
        /// JMP_TO_LABEL label - jump to label without modifying or looking up the stack
        /// </summary>
        JMP_TO_LABEL = 10,

        /// <summary>
        /// SAVE - save continuation point on the stack, as a combo of specific function, program counter,
        ///        and environment
        /// </summary>
        SAVE_RETURN = 11,

        /// <summary>
        /// JMP_CLOSURE n - jump to the start of the function on top of the stack; n is arg count
        /// </summary>
        JMP_CLOSURE = 12,

        /// <summary>
        /// RETURN - return to a previous execution point (second on the stack) but preserving
        ///          the return value (top of the stack)
        /// </summary>
        RETURN_VAL = 13,

        /// <summary>
        /// MAKE_ENV n - make a new environment frame, pop n values from stack onto it,
        ///              and push it on the environment stack
        /// </summary>
        MAKE_ENV = 14,

        /// <summary>
        /// MAKE_ENVDOT n - make a new environment frame with n-1 named args and one for varargs,
        ///                 pop values from stack onto it, and push on the environment stack
        /// </summary>
        MAKE_ENVDOT = 15,

        /// <summary>
        /// MAKE_CLOSURE fn - create a closure fn from arguments and current environment, and push onto the stack
        /// </summary>
        MAKE_CLOSURE = 16,

        /// <summary>
        /// CALL_PRIMOP name - performs a primitive function call right off of the stack, where callee performs
        ///             stack maintenance (i.e. the primitive will pop its args, and push a return value)
        /// </summary>
        CALL_PRIMOP = 17,
    }

    /// <summary>
	/// Instructions produced by the compiler
	/// </summary>
    [DebuggerDisplay("{DebugString}")]
    public class Instruction
    {
        /// <summary> ArrayList of human readable names for all constants </summary>
        private static readonly string[] _NAMES = Enum.GetNames(typeof(Opcode));

        /// <summary> Names of all jump instructions that need to be fixed up at assembly time </summary>
        private static readonly List<Opcode> JUMP_TYPES = new List<Opcode>() {
            Opcode.JMP_TO_LABEL,
            Opcode.JMP_IF_FALSE,
            Opcode.JMP_IF_TRUE,
            Opcode.SAVE_RETURN
        };

        /// <summary> Instruction type, one of the constants in this class </summary>
        public Opcode type { get; private set; }

        /// <summary> First instruction parameter (context-sensitive) </summary>
        public Val first { get; private set; }

        /// <summary> Second instruction parameter (context-sensitive) </summary>
        public Val second { get; private set; }

        /// <summary> Debug information (printed to the user as needed) </summary>
        public readonly string debug;

        public Instruction (Opcode type) : this(type, Val.NIL, Val.NIL, null) { }
        public Instruction (Opcode type, Val first, string debug = null) : this(type, first, Val.NIL, debug) { }
        public Instruction (Opcode type, Val first, Val second, string debug = null) {
            this.type = type;
            this.first = first;
            this.second = second;
            this.debug = debug;
        }

        /// <summary> Is this instruction one of the jump instructions that needs to be modified during assembly? </summary>
        public bool IsJump => JUMP_TYPES.Contains(type);

        /// <summary> If this is a jump instruction, updates the second parameter to contain the destination </summary>
        public void UpdateJumpDestination (int pc) {
            if (!IsJump) { throw new LanguageError($"Attempting to set jump destination for non-jump instruction {type}"); }
            second = new Val(pc);
        }

        private string DebugString => DebugPrint(" ");

        /// <summary> Converts an instruction to a string </summary>
        public string DebugPrint (string sep = "\t") {
            StringBuilder sb = new StringBuilder();
            sb.Append(_NAMES[(int) type]);

            if (first.IsNotNil || type == Opcode.PUSH_CONST) {
                sb.Append(sep);
                sb.Append(Val.DebugPrint(first));
            }

            if (second.IsNotNil) {
                sb.Append(sep);
                sb.Append(Val.DebugPrint(second));
            }
            if (debug != null) {
                sb.Append(sep);
                sb.Append("; ");
                sb.Append(debug);
            }
            return sb.ToString();
        }

        /// <summary> Returns true if two instructions are equal </summary>
        public static bool Equal (Instruction a, Instruction b)
            => a.type == b.type && Val.Equals(a.first, b.first) && Val.Equals(a.second, b.second);
    }

}