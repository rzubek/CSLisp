using CSLisp.Data;
using CSLisp.Error;
using System.Collections.Generic;

namespace CSLisp.Core
{
    /// <summary>
    /// Virtual machine that will interpret compiled bytecode
    /// </summary>
    public class Machine
    {
        /// <summary> If set, instructions will be logged to this function as they're executed. </summary>
        private readonly ILogger _logger = null;

        /// <summary> Internal execution context </summary>
        private readonly Context _ctx = null;

        public Machine (Context ctx, ILogger logger) {
            _ctx = ctx;
            _logger = logger;
        }

        /// <summary> Runs the given piece of code, and returns the value left at the top of the stack. </summary>
        public Val Execute (Closure fn, params Val[] args) {
            State st = new State(fn, args);
            CodeHandle code = default;
            List<Instruction> instructions = null;

            if (_logger.EnableInstructionLogging) {
                _logger.Log("Executing: ", fn.name);
                _logger.Log(_ctx.code.DebugPrint(fn));
            }

            while (!st.done) {
                if (!code.Equals(st.fn.code)) {
                    code = st.fn.code;
                    instructions = _ctx.code.Get(code).instructions;
                }

                if (st.pc >= instructions.Count) {
                    throw new LanguageError("Runaway opcodes!");
                }

                // fetch instruction
                Instruction instr = instructions[st.pc++];

                if (_logger.EnableStackLogging) {
                    _logger.Log("                                    " + State.PrintStack(st));
                    _logger.Log(string.Format("[{0,2}] {1,3} : {2}", st.stack.Count, st.pc - 1, instr.DebugPrint()));
                }

                // and now a big old switch statement. not handler functions - this is much faster.

                switch (instr.type) {
                    case Opcode.LABEL:
                        // no op :)
                        break;

                    case Opcode.PUSH_CONST: {
                            st.Push(instr.first);
                        }
                        break;

                    case Opcode.LOCAL_GET: {
                            VarPos pos = new VarPos(instr.first, instr.second);
                            Val value = Environment.GetValueAt(pos, st.env);
                            st.Push(value);
                        }
                        break;

                    case Opcode.LOCAL_SET: {
                            VarPos pos = new VarPos(instr.first, instr.second);
                            Val value = st.Peek();
                            Environment.SetValueAt(pos, value, st.env);
                        }
                        break;

                    case Opcode.GLOBAL_GET: {
                            Symbol symbol = instr.first.AsSymbol;
                            Val value = symbol.pkg.GetValue(symbol);
                            st.Push(value);
                        }
                        break;

                    case Opcode.GLOBAL_SET: {
                            Symbol symbol = instr.first.AsSymbol;
                            Val value = st.Peek();
                            symbol.pkg.SetValue(symbol, value);
                        }
                        break;

                    case Opcode.STACK_POP:
                        st.Pop();
                        break;

                    case Opcode.JMP_IF_TRUE: {
                            Val value = st.Pop();
                            if (value.CastToBool) {
                                st.pc = GetLabelPosition(instr);
                            }
                        }
                        break;

                    case Opcode.JMP_IF_FALSE: {
                            Val value = st.Pop();
                            if (!value.CastToBool) {
                                st.pc = GetLabelPosition(instr);
                            }
                        }
                        break;

                    case Opcode.JMP_TO_LABEL: {
                            st.pc = GetLabelPosition(instr);
                        }
                        break;

                    case Opcode.MAKE_ENV: {
                            int argcount = instr.first.AsInt;
                            if (st.argcount != argcount) { throw new LanguageError($"Argument count error, expected {argcount}, got {st.argcount}"); }

                            // make an environment for the given number of named args
                            st.env = new Environment(st.argcount, st.env);

                            // move named arguments onto the stack frame
                            for (int i = argcount - 1; i >= 0; i--) {
                                st.env.SetValue(i, st.Pop());
                            }
                        }
                        break;

                    case Opcode.MAKE_ENVDOT: {
                            int argcount = instr.first.AsInt;
                            if (st.argcount < argcount) { throw new LanguageError($"Argument count error, expected {argcount} or more, got {st.argcount}"); }

                            // make an environment for all named args, +1 for the list of remaining varargs
                            int dotted = st.argcount - argcount;
                            st.env = new Environment(argcount + 1, st.env);

                            // cons up dotted values from the stack
                            for (int dd = dotted - 1; dd >= 0; dd--) {
                                Val arg = st.Pop();
                                st.env.SetValue(argcount, new Val(new Cons(arg, st.env.GetValue(argcount))));
                            }

                            // and move the named ones onto the environment stack frame
                            for (int i = argcount - 1; i >= 0; i--) {
                                st.env.SetValue(i, st.Pop());
                            }
                        }
                        break;

                    case Opcode.DUPLICATE: {
                            if (st.stack.Count == 0) { throw new LanguageError("Cannot duplicate on an empty stack!"); }
                            st.Push(st.Peek());
                        }
                        break;

                    case Opcode.JMP_CLOSURE: {
                            st.env = st.env.parent; // discard the top environment frame
                            Val top = st.Pop();
                            Closure closure = top.AsClosureOrNull;

                            // set vm state to the beginning of the closure
                            st.fn = closure ?? throw new LanguageError($"Unknown function during function call around: {DebugRecentInstructions(st, instructions)}");
                            st.env = closure.env;
                            st.pc = 0;
                            st.argcount = instr.first.AsInt;
                        }
                        break;

                    case Opcode.SAVE_RETURN: {
                            // save current vm state to a return value
                            st.Push(new Val(new ReturnAddress(st.fn, GetLabelPosition(instr), st.env, instr.first.AsStringOrNull)));
                        }
                        break;

                    case Opcode.RETURN_VAL:
                        if (st.stack.Count > 1) {
                            // preserve return value on top of the stack
                            Val retval = st.Pop();
                            ReturnAddress retaddr = st.Pop().AsReturnAddress;
                            st.Push(retval);

                            // restore vm state from the return value
                            st.fn = retaddr.fn;
                            st.env = retaddr.env;
                            st.pc = retaddr.pc;
                        } else {
                            st.done = true; // this will force the virtual machine to finish up
                        }
                        break;

                    case Opcode.MAKE_CLOSURE: {
                            var cl = instr.first.AsClosure;
                            st.Push(new Closure(cl.code, st.env, null, cl.name));
                        }
                        break;

                    case Opcode.CALL_PRIMOP: {
                            string name = instr.first.AsString;
                            int argn = (instr.second.IsInt) ? instr.second.AsInt : st.argcount;

                            Primitive prim = Primitives.FindNary(name, argn);
                            if (prim == null) { throw new LanguageError($"Invalid argument count to primitive {name}, count of {argn}"); }

                            Val result = prim.Call(_ctx, argn, st);
                            st.Push(result);
                        }
                        break;

                    default:
                        throw new LanguageError("Unknown instruction type: " + instr.type);
                }
            }

            // return whatever's on the top of the stack
            if (st.stack.Count == 0) {
                throw new LanguageError("Stack underflow!");
            }

            return st.Peek();
        }

        /// <summary> Very naive helper function, finds the position of a given label in the instruction set </summary>
        private static int GetLabelPosition (Instruction inst) {
            if (inst.second.IsInt) {
                return inst.second.AsInt;
            } else {
                throw new LanguageError("Unknown jump label: " + inst.first);
            }
        }

        /// <summary> A bit of debug info </summary>
        private static string DebugRecentInstructions (State st, List<Instruction> instructions) {
            string result = $"Closure {st.fn.code}, around instr pc {st.pc - 1}:";
            for (int i = st.pc - 5; i <= st.pc; i++) {
                if (i >= 0 && i < instructions.Count) {
                    result += $"{i}: {instructions[i].DebugPrint()}\n";
                }
            }
            return result;
        }
    }
}
