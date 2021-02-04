using CSLisp.Data;
using CSLisp.Error;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSLisp.Core
{
    /// <summary> Opaque handle to a code block entry in storage </summary>
    public struct CodeHandle : IEquatable<CodeHandle>
    {
        public int index;

        public CodeHandle (int index) { this.index = index; }

        public bool IsValid => index > 0; // index at 0 is always null

        public static bool Equals (CodeHandle a, CodeHandle b) => a.index == b.index;

        public bool Equals (CodeHandle other) => Equals(this, other);

        public override bool Equals (object obj) => obj is CodeHandle handle && Equals(this, handle);
        public override int GetHashCode () => index;
    }

    /// <summary> Code block stores a collection of instructions and some additional debugging data </summary>
    public class CodeBlock
    {
        public CodeHandle handle;
        public readonly List<Instruction> instructions;
        public readonly string debug;

        public CodeBlock (CodeHandle handle, List<Instruction> instructions, string debug) {
            this.handle = handle;
            this.instructions = instructions;
            this.debug = debug;
        }
    }

    /// <summary>
    /// Storage for compiled code blocks, putting them in one place for easier debugging
    /// </summary>
    public class Code
    {
        // block storage as a list to ensure fast lookup
        private readonly List<CodeBlock> _blocks = new List<CodeBlock>() { null }; // make sure index starts at 1

        public CodeHandle LastHandle => new CodeHandle(_blocks.Count - 1);

        /// <summary> Registers a new code block and returns its handle </summary>
        public CodeHandle AddBlock (List<Instruction> instructions, string debug) {
            var handle = new CodeHandle { index = _blocks.Count };
            _blocks.Add(new CodeBlock(handle, instructions, debug));
            return handle;
        }

        /// <summary> Deregisters the specified code block and replaces it with null. </summary>
        public void RemoveBlock (CodeHandle handle) {
            if (handle.index <= 0 || handle.index >= _blocks.Count) {
                throw new LanguageError("Invalid code block handle!");
            }

            _blocks[handle.index] = null; // note we just leave a hole, so we don't renumber other ones
        }

        /// <summary> Retrieves a code block registered for a given handle </summary>
        public CodeBlock Get (CodeHandle handle) {
            if (handle.index <= 0 || handle.index >= _blocks.Count) {
                throw new LanguageError("Invalid code block handle!");
            }

            return _blocks[handle.index];
        }

        /// <summary> Returns an iterator over all code blocks. Some may be null! </summary>
        public IEnumerable<CodeBlock> GetAll () => _blocks;

        /// <summary> Converts a compilation result to a string </summary>
        public string DebugPrint (CompilationResults comp, int indentLevel = 1) =>
            string.Join("\n", comp.recents.Select(h => DebugPrint(h, indentLevel)));

        /// <summary> Converts a set of instructions to a string </summary>
        public string DebugPrint (Closure cl, int indentLevel = 1) =>
            DebugPrint(cl.code, indentLevel);

        /// <summary> Converts a set of instructions to a string </summary>
        private string DebugPrint (CodeHandle handle, int indentLevel) {
            var block = Get(handle);
            StringBuilder sb = new StringBuilder();

            sb.Append('\t', indentLevel);
            //sb.AppendLine($"CODE BLOCK # {block.handle.index} ; {block.debug}");
            sb.AppendLine($"CODE BLOCK ; {block.debug}");

            for (int i = 0, count = block.instructions.Count; i < count; i++) {
                Instruction instruction = block.instructions[i];

                // tab out and print current instruction
                int tabs = indentLevel + (instruction.type == Opcode.LABEL ? -1 : 0);
                sb.Append('\t', tabs);
                sb.Append(i);
                sb.Append('\t');
                sb.AppendLine(instruction.DebugPrint());

                //if (instruction.type == Opcode.MAKE_CLOSURE) {
                //    // if function, recurse
                //    Closure closure = instruction.first.AsClosure;
                //    sb.Append(DebugPrint(closure, indentLevel + 1));
                //}
            }
            return sb.ToString();
        }

        /// <summary> Converts all sets of instructions to a string </summary>
        public string DebugPrintAll () {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n\n*** ALL CODE BLOCKS\n");

            for (int i = 0; i < _blocks.Count; i++) {
                CodeBlock block = _blocks[i];
                if (block != null) {
                    sb.AppendLine(DebugPrint(block.handle, 1));
                }
            }

            sb.AppendLine("*** END OF ALL CODE BLOCKS\n");
            return sb.ToString();
        }
    }
}