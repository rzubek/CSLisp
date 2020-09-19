using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;
using System.Text;

namespace CSLisp.Core
{
    /// <summary>
    /// Storage for compiled code blocks, putting them in one place for easier debugging
    /// </summary>
    public class Code
    {
        /// <summary> Opaque handle to a code block entry in storage </summary>
        public struct Handle
        {
            public int index;

            public bool IsValid => index > 0; // index at 0 is always null

            public bool Equals (Handle other) => other.index == index;
            public override bool Equals (object obj) => obj is Handle handle && handle.Equals(this);
            public override int GetHashCode () => index;
        }

        /// <summary> Code block stores a collection of instructions and some additional debugging data </summary>
        public class Block
        {
            public readonly List<Instruction> instructions;
            public readonly string debug;

            public Block (List<Instruction> instructions, string debug) {
                this.instructions = instructions;
                this.debug = debug;
            }
        }

        private readonly List<Block> _blocks = new List<Block>() { null }; // make sure index starts at 1

        /// <summary> Registers a new code block and returns its handle </summary>
        public Handle Register (List<Instruction> instructions, string debug) {
            _blocks.Add(new Block(instructions, debug));
            return new Handle { index = _blocks.Count - 1 };
        }

        /// <summary> Retrieves a code block registered for a given handle </summary>
        public Block Get (Handle handle) {
            if (handle.index < 0 || handle.index >= _blocks.Count) {
                throw new LanguageError("Invalid code block handle!");
            }

            return _blocks[handle.index];
        }

        /// <summary> Deregisters the specified code block and replaces it with null. </summary>
        public void Unregister (Handle handle) {
            if (handle.index < 0 || handle.index >= _blocks.Count) {
                throw new LanguageError("Invalid code block handle!");
            }

            _blocks[handle.index] = null;
        }


        /// <summary> Returns an iterator over all code blocks. Some may be null! </summary>
        public IEnumerable<Block> GetAll () => _blocks;

        /// <summary> Converts a set of instructions to a string </summary>
        public string DebugPrint (Closure cl, int indentLevel = 1) => DebugPrint(cl.code, indentLevel);

        /// <summary> Converts a set of instructions to a string </summary>
        private string DebugPrint (Handle handle, int indentLevel) => DebugPrint(Get(handle).instructions, indentLevel);

        /// <summary> Converts a set of instructions to a string </summary>
        private string DebugPrint (List<Instruction> instructions, int indentLevel) {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < instructions.Count; i++) {
                Instruction instruction = instructions[i];

                // tab out and print current instruction
                int tabs = indentLevel + (instruction.type == Opcode.MAKE_LABEL ? -1 : 0);
                sb.Append('\t', tabs);
                sb.Append(i);
                sb.Append('\t');
                sb.AppendLine(instruction.DebugPrint());

                if (instruction.type == Opcode.MAKE_CLOSURE) {
                    // if function, recurse
                    Closure closure = instruction.first.AsClosure;
                    sb.Append(DebugPrint(closure, indentLevel + 1));
                }
            }
            return sb.ToString();
        }

        /// <summary> Converts all sets of instructions to a string </summary>
        public string DebugPrintAll () {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _blocks.Count; i++) {
                Block block = _blocks[i];
                if (block != null) {
                    sb.AppendLine($"********** CODE BLOCK # {i} (count = {block.instructions.Count})   {block.debug}");
                    sb.AppendLine(DebugPrint(block.instructions, 1));
                }
            }
            return sb.ToString();
        }
    }
}