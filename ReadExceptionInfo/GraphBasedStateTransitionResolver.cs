using System;
using System.Collections.Generic;
using System.Linq;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.Core.Code;
using Echo.DataFlow.Emulation;

namespace ReadExceptionInfo
{
    public class GraphBasedStateTransitionResolver<TInstruction> : StateTransitionResolverBase<TInstruction>
    {
        private readonly Dictionary<ControlFlowNode<TInstruction>, long> m_cfgNodeHeads;
        private readonly Dictionary<long, ControlFlowNode<TInstruction>> m_cfgNodeTails;
        private readonly Dictionary<long, long> m_nextInstructions;

        public GraphBasedStateTransitionResolver(IInstructionSetArchitecture<TInstruction> architecture,
            ControlFlowGraph<TInstruction> cfg) : base(architecture)
        {
            m_cfgNodeHeads = new Dictionary<ControlFlowNode<TInstruction>, long>();
            m_cfgNodeTails = new Dictionary<long, ControlFlowNode<TInstruction>>();
            m_nextInstructions = new Dictionary<long, long>();
            
            foreach (var node in cfg.Nodes)
            {
                long previousInstructionOffset = 0;
                
                foreach (var instruction in node.Contents.Instructions)
                {
                    var currentInstructionOffset = architecture.GetOffset(instruction);

                    if (previousInstructionOffset != 0)
                    {
                        m_nextInstructions.Add(previousInstructionOffset, currentInstructionOffset);
                    }

                    previousInstructionOffset = currentInstructionOffset;
                }
                
                m_cfgNodeHeads[node] = architecture.GetOffset(node.Contents.Header);
                m_cfgNodeTails[architecture.GetOffset(node.Contents.Footer)] = node;
            }
        }

        public override int GetTransitionCount(in SymbolicProgramState<TInstruction> currentState, in TInstruction instruction)
        {
            var offset = Architecture.GetOffset(instruction);
            if (m_nextInstructions.ContainsKey(offset))
            {
                // fallthrough
                return 1;
            }
            
            var node = m_cfgNodeTails[offset];
            var hasUnconditional = node.UnconditionalNeighbour != null;
            var conditionalCount = node.ConditionalEdges.Count;
            var abnormalCount = node.AbnormalEdges.Count;
            return (hasUnconditional ? 1 : 0) + conditionalCount + abnormalCount;
        }

        public override int GetTransitions(in SymbolicProgramState<TInstruction> currentState, in TInstruction instruction, Span<StateTransition<TInstruction>> transitionBuffer)
        {
            var nextState = ApplyDefaultBehaviour(currentState, instruction);
            
            var offset = Architecture.GetOffset(instruction);
            if (m_nextInstructions.TryGetValue(offset, out var nextInstructionPtr))
            {
                var fallthroughState = nextState.WithProgramCounter(nextInstructionPtr);
                transitionBuffer[0] = new StateTransition<TInstruction>(fallthroughState, ControlFlowEdgeType.FallThrough);
                return 1;
            }
            var node = m_cfgNodeTails[offset];

            var outgoingEdges = node.GetOutgoingEdges().ToArray();
            for (var i = 0; i < outgoingEdges.Length; i++)
            {
                var edge = outgoingEdges[i];
                
                var branchState = nextState.WithProgramCounter(m_cfgNodeHeads[edge.Target]);
                transitionBuffer[i] = new StateTransition<TInstruction>(branchState, edge.Type);
            }
            return outgoingEdges.Length;
        }
    }
}