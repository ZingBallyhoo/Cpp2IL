using System;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.DataFlow.Emulation;
using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo
{
    public class Il2CppStateTransitionResolver : StateTransitionResolverBase<LiftedAction>
    {
        public Il2CppStateTransitionResolver(Il2CppArchitecture architecture) : base(architecture)
        {
        }

        public override int GetTransitionCount(in SymbolicProgramState<LiftedAction> currentState,
            in LiftedAction instruction)
        {
            if (instruction is ReturnAction)
            {
                return 0;
            }

            if (instruction is BranchIfEqual)
            {
                return 2;
            }

            return 1;
        }

        public override int GetTransitions(in SymbolicProgramState<LiftedAction> currentState,
            in LiftedAction instruction, Span<StateTransition<LiftedAction>> transitionBuffer)
        {
            var nextState = ApplyDefaultBehaviour(currentState, instruction);

            if (instruction is ReturnAction)
            {
                return 0;
            }

            transitionBuffer[0] = new StateTransition<LiftedAction>(nextState, ControlFlowEdgeType.FallThrough);

            if (instruction is BranchIfEqual)
            {
                // todo:
                return 2;
            }

            return 1;
        }
    }
}