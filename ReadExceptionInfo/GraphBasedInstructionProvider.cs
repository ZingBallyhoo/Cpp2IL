using System.Collections.Generic;
using Echo.ControlFlow;
using Echo.Core.Code;

namespace ReadExceptionInfo
{
    public class GraphBasedInstructionProvider<TInstruction> : IStaticInstructionProvider<TInstruction>
    {
        public IInstructionSetArchitecture<TInstruction> Architecture { get; }
        
        private readonly Dictionary<long, TInstruction> m_instructionLookup;
        
        public GraphBasedInstructionProvider(IInstructionSetArchitecture<TInstruction> architecture, ControlFlowGraph<TInstruction> cfg)
        {
            Architecture = architecture;

            m_instructionLookup = new Dictionary<long, TInstruction>();
            foreach (var node in cfg.Nodes)
            {
                foreach (var instruction in node.Contents.Instructions)
                {
                    m_instructionLookup[architecture.GetOffset(instruction)] = instruction;
                }
            }
        }

        public TInstruction GetInstructionAtOffset(long offset)
        {
            return m_instructionLookup[offset];
        }
    }
}