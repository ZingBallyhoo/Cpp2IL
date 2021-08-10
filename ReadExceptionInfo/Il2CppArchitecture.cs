using System;
using System.Collections.Generic;
using Echo.Core.Code;
using Echo.Platforms.Iced;
using Iced.Intel;
using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo
{
    public class Il2CppArchitecture : IInstructionSetArchitecture<LiftedAction>
    {
        public readonly Dictionary<LiftedAction, long> m_instructionsToOffsets;
        public readonly Dictionary<long, LiftedAction> m_offsetsToInstructions;

        private readonly IDictionary<Register, X86GeneralRegister> m_gpr =
            new Dictionary<Register, X86GeneralRegister>();

        public Il2CppArchitecture(List<List<LiftedAction>> blocks)
        {
            m_instructionsToOffsets = new Dictionary<LiftedAction, long>();
            m_offsetsToInstructions = new Dictionary<long, LiftedAction>();

            var offset = 0;

            foreach (var instructions in blocks)
            {
                for (var i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];

                    m_instructionsToOffsets[instruction] = offset;
                    m_offsetsToInstructions[offset] = instructions[i];

                    offset += GetSize(instruction);
                }

                offset += 100;
            }

            foreach (Register register in Enum.GetValues(typeof(Register)))
            {
                m_gpr[register] = new X86GeneralRegister(register);
            }
        }

        public long GetOffset(in LiftedAction instruction)
        {
            return m_instructionsToOffsets[instruction];
        }

        public int GetSize(in LiftedAction instruction)
        {
            return 10;
        }

        public InstructionFlowControl GetFlowControl(in LiftedAction instruction)
        {
            if (instruction is ReturnAction)
            {
                return InstructionFlowControl.IsTerminator;
            }

            if (instruction is BranchIfEqual)
            {
                return InstructionFlowControl.CanBranch;
            }

            return InstructionFlowControl.Fallthrough;
        }

        public int GetStackPushCount(in LiftedAction instruction)
        {
            return 0;
        }

        public int GetStackPopCount(in LiftedAction instruction)
        {
            return 0;
        }

        public int GetReadVariablesCount(in LiftedAction instruction)
        {
            var read = instruction.GetReadRegisters();
            return read.Length;
        }

        public int GetReadVariables(in LiftedAction instruction, Span<IVariable> variablesBuffer)
        {
            var read = instruction.GetReadRegisters();
            for (var i = 0; i < read.Length; i++)
            {
                variablesBuffer[i] = m_gpr[read[i]];
            }

            return read.Length;
        }

        public int GetWrittenVariablesCount(in LiftedAction instruction)
        {
            var written = instruction.GetWrittenRegisters();
            return written.Length;
        }

        public int GetWrittenVariables(in LiftedAction instruction, Span<IVariable> variablesBuffer)
        {
            var written = instruction.GetWrittenRegisters();
            for (var i = 0; i < written.Length; i++)
            {
                variablesBuffer[i] = m_gpr[written[i]];
            }

            return written.Length;
        }
    }
}