using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cpp2IL.Core;
using Echo.Ast;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.Core.Code;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using ReadExceptionInfo.Actions;
using ReadExceptionInfo.Ast;
using ReadExceptionInfo.Typing;
using Instruction = Iced.Intel.Instruction;

namespace ReadExceptionInfo
{
    public class NewAnalysis
    {
        private readonly ControlFlowGraph<Instruction> m_cfg;
        private readonly Stack<long> m_theStack = new Stack<long>();

        private readonly Dictionary<long, Il2CppSymbolicProgramState> m_states = new Dictionary<long, Il2CppSymbolicProgramState>();

        public NewAnalysis(ControlFlowGraph<Instruction> cfg)
        {
            m_cfg = cfg;
        }

        public void Do()
        {
            var containingType = Utils.TryLookupTypeDefKnownNotGeneric("MyType");
            Debug.Assert(containingType != null);
            var analysedMethod = containingType.Methods.Single(x => x.Name == "AmbiguousLocalType");
            
            var entrypoint = m_cfg.Entrypoint;
            Debug.Assert(entrypoint != null);

            TraverseBlock(entrypoint);

            var parsedAST = BuildAST();
            DeferredReadFixer.HandleDeferredReads(parsedAST);

            var neededLocals = new NeededVariablesWalker(parsedAST);
            neededLocals.RemoveUnneededInstructions();
            
            var dotWriter = new StringWriter();
            parsedAST.ToDotGraph(dotWriter);

            var ilProcessor = analysedMethod.Body.GetILProcessor();
            ilProcessor.Clear();
            
            var variableTypes = DetermineVariableTypes(neededLocals);
            var variableIds = CreateCilLocals(variableTypes, ilProcessor);

            EmitIL(parsedAST, neededLocals, ilProcessor, variableIds, variableTypes);

            Cpp2IlApi.SaveAssemblies("outManaged", new List<AssemblyDefinition>
            {
                SharedState.AssemblyList.Single(x => x.Name.Name == "MyAttribute")
            });
        }

        private static void EmitIL(ControlFlowGraph<Statement<LiftedAction>> parsedAST, 
            NeededVariablesWalker neededLocals, ILProcessor ilProcessor, Dictionary<IVariable, int> variableIds, 
            Dictionary<IVariable, ITypeSpec> variableTypes)
        {
            var blockHeaderInstructions = new Dictionary<ControlFlowNode<Statement<LiftedAction>>, Mono.Cecil.Cil.Instruction>();

            foreach (var node in parsedAST.SortNodes())
            {
                var phiEmitted = false;

                void EmitPhi()
                {
                    if (phiEmitted) return;
                    phiEmitted = true;

                    foreach (var successor in node.GetSuccessors())
                    {
                        foreach (var successorStatement in successor.Contents.Instructions)
                        {
                            if (successorStatement is not PhiStatement<LiftedAction> phiStatement) continue;

                            var selectedSource = phiStatement.Sources
                                .Single(x => neededLocals.m_variableDefinitionLocations[x.Variable] == node).Variable;
                            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldloc, variableIds[selectedSource]));
                            ilProcessor.Append(ilProcessor.Create(OpCodes.Stloc, variableIds[phiStatement.Target]));
                        }
                    }
                }

                void AppendForBlock(Mono.Cecil.Cil.Instruction instruction)
                {
                    if (!blockHeaderInstructions.ContainsKey(node))
                    {
                        blockHeaderInstructions.Add(node, instruction);
                    }

                    ilProcessor.Append(instruction);
                }

                foreach (var statement in node.Contents.Instructions)
                {
                    Expression<LiftedAction> expression;
                    var assignment = statement as AssignmentStatement<LiftedAction>;
                    if (assignment != null)
                    {
                        expression = assignment.Expression;
                    } else if (statement is ExpressionStatement<LiftedAction> expressionStatement)
                    {
                        expression = expressionStatement.Expression;
                    } else
                    {
                        // phi

                        continue;
                    }

                    var instructionExpression = (InstructionExpression<LiftedAction>) expression;
                    
                    if (!neededLocals.m_neededInstructions.Contains(instructionExpression))
                    {
                        // not needed for any control flow
                        // todo: only here for sanity now, see RemoveUnneededInstructions

                        continue;
                    }

                    if (instructionExpression.Instruction is IBranchInstruction)
                    {
                        // emit phi before branch

                        EmitPhi();
                    }

                    ITypeSpec? typeSpec = null;
                    if (assignment != null)
                    {
                        typeSpec = variableTypes[assignment.Variables.Single()];
                    }

                    foreach (var argument in instructionExpression.Arguments)
                    {
                        // load input stack

                        var argumentVariable = argument.GetVariable();
                        AppendForBlock(ilProcessor.Create(OpCodes.Ldloc, variableIds[argumentVariable]));
                    }

                    var createdInstructions = instructionExpression.Instruction.EmitIL(ilProcessor, typeSpec);

                    if (instructionExpression.Instruction is IBranchInstruction)
                    {
                        // store token for branch resolution

                        var branchInstruction = createdInstructions.Last();
                        if (node.ConditionalEdges.Count > 0)
                        {
                            branchInstruction.Operand = node.ConditionalEdges.Single().Target;
                        } else
                        {
                            Debug.Assert(node.UnconditionalNeighbour != null);
                            branchInstruction.Operand = node.UnconditionalNeighbour;
                        }
                    }

                    foreach (var createdInstruction in createdInstructions)
                    {
                        // add actual work instructions

                        AppendForBlock(createdInstruction);
                    }

                    if (assignment != null)
                    {
                        // store output value

                        AppendForBlock(ilProcessor.Create(OpCodes.Stloc, variableIds[assignment.Variables.Single()]));
                    }
                }

                // if fallthrough, emit phi
                EmitPhi();
            }

            foreach (var instruction in ilProcessor.Body.Instructions)
            {
                if (instruction.Operand is not ControlFlowNode<Statement<LiftedAction>> destNode)
                {
                    continue;
                }

                instruction.Operand = blockHeaderInstructions[destNode];
            }

            ilProcessor.Body.OptimizeMacros();
        }

        private ControlFlowGraph<Statement<LiftedAction>> BuildAST()
        {
            var architecture = new Il2CppArchitecture(m_states.Select(x => x.Value.m_actions).ToList());

            var oldToNewOffsets = new Dictionary<long, long>();

            ControlFlowGraph<LiftedAction> newCfg = new ControlFlowGraph<LiftedAction>(architecture);
            foreach (var state in m_states)
            {
                var newOffset = architecture.GetOffset(state.Value.m_actions[0]);
                oldToNewOffsets[state.Key] = newOffset;

                var newNode = new ControlFlowNode<LiftedAction>(newOffset, state.Value.m_actions);
                newCfg.Nodes.Add(newNode);
            }

            foreach (var state in m_states)
            {
                var origNode = m_cfg.Nodes[state.Key];
                var newNode = newCfg.Nodes[oldToNewOffsets[state.Key]];
                foreach (var outgoingEdge in origNode.GetOutgoingEdges())
                {
                    newNode.ConnectWith(newCfg.Nodes[oldToNewOffsets[outgoingEdge.Target.Id]], outgoingEdge.Type);
                }
            }

            newCfg.Entrypoint = newCfg.Nodes[oldToNewOffsets[m_cfg.Entrypoint.Id]];

            var instructionProvider = new GraphBasedInstructionProvider<LiftedAction>(architecture, newCfg);
            var dfgBuilder = new GraphBasedStateTransitionResolver<LiftedAction>(architecture, newCfg);

            var cfgBuilder = new SymbolicFlowGraphBuilder<LiftedAction>(
                instructionProvider,
                dfgBuilder);

            var symbolicGraph = cfgBuilder.ConstructFlowGraph(0, new long[0]);

            var astBuilder = new AstParser<LiftedAction>(symbolicGraph, dfgBuilder.DataFlowGraph);
            var parsedAST = astBuilder.Parse();
            
            return parsedAST;
        }

        private static Dictionary<IVariable, int> CreateCilLocals(Dictionary<IVariable, ITypeSpec> variableTypes, ILProcessor ilProcessor)
        {
            Dictionary<IVariable, int> variableIds = new Dictionary<IVariable, int>();
            foreach (var variablePair in variableTypes)
            {
                TypeReference managedType;
                if (variablePair.Value is KnownManagedTypeSpec knownManagedType)
                {
                    managedType = knownManagedType.m_baseManagedType;
                } else if (variablePair.Value is HintedImmediateTypeSpec hintedImmediateTypeSpec)
                {
                    // todo: common handling
                    
                    if (hintedImmediateTypeSpec.m_immediateType == ImmediateType.Int32)
                    {
                        var int32 = Utils.TryLookupTypeDefKnownNotGeneric("System.Int32");
                        Debug.Assert(int32 != null);
                        managedType = int32;
                    } else
                    {
                        throw new NotImplementedException();
                    }
                } else
                {
                    throw new NotImplementedException();
                }

                var variableDefinition = new VariableDefinition(ilProcessor.ImportReference(managedType));
                ilProcessor.Body.Variables.Add(variableDefinition);
                variableIds.Add(variablePair.Key, variableDefinition.Index);
            }

            return variableIds;
        }

        private static Dictionary<IVariable, ITypeSpec> DetermineVariableTypes(NeededVariablesWalker neededLocals)
        {
            Dictionary<IVariable, ITypeSpec> variableTypes = new Dictionary<IVariable, ITypeSpec>();

            foreach (var neededAssignmentPair in neededLocals.m_neededAssignments)
            {
                var instructionExpression = (InstructionExpression<LiftedAction>) neededAssignmentPair.Value.Expression;
                var instruction = instructionExpression.Instruction;

                var typeSpec = instruction.GetInitialTypeSpec();
                var variableAssignedTo = neededAssignmentPair.Value.Variables.Single();

                Console.Out.Write($"{typeSpec}");

                variableTypes[variableAssignedTo] = typeSpec;
            }

            // todo: propagate arguments

            foreach (var phiVariable in neededLocals.m_neededPhi)
            {
                IVariable[] sourceVariables = new IVariable[phiVariable.Value.Length];
                ITypeSpec[] sourceTypeSpecs = new ITypeSpec[phiVariable.Value.Length];
                for (var i = 0; i < sourceTypeSpecs.Length; i++)
                {
                    var sourceVariable = phiVariable.Value[i];
                    if (neededLocals.m_moveOriginalDefinitions.TryGetValue(sourceVariable, out var moveSource))
                    {
                        sourceVariable = moveSource;
                    }

                    sourceVariables[i] = sourceVariable;
                    sourceTypeSpecs[i] = variableTypes[sourceVariable];
                }

                ITypeSpec phiSpec;

                if (sourceTypeSpecs.Any(x => x is KnownManagedTypeSpec))
                {
                    var inputTypes = sourceTypeSpecs.OfType<KnownManagedTypeSpec>().Select(x => x.m_baseManagedType).ToHashSet();
                    Debug.Assert(inputTypes.Count > 0);

                    if (inputTypes.Any(x => x.IsValueType))
                    {
                        // todo: untested codepath

                        Debug.Assert(inputTypes.All(x => x.IsValueType));
                        Debug.Assert(inputTypes.Count == 1); // todo: should all be same..?

                        phiSpec = new KnownManagedTypeSpec(inputTypes.Single());
                    } else
                    {
                        var lowestType = inputTypes.First();

                        foreach (var inputType in inputTypes)
                        {
                            if (inputType == lowestType) continue;

                            // todo: better common base handling
                            // todo: interfaces
                            var lowestTypeBase = lowestType.BaseType;
                            while (lowestTypeBase != null)
                            {
                                if (lowestTypeBase == inputType)
                                {
                                    lowestType = inputType;
                                    break;
                                }

                                lowestTypeBase = lowestTypeBase.Resolve().BaseType;
                            }
                        }

                        phiSpec = new KnownManagedTypeSpec(lowestType);
                    }

                    for (var i = 0; i < sourceTypeSpecs.Length; i++)
                    {
                        var sourceTypeSpec = sourceTypeSpecs[i];
                        if (sourceTypeSpec is not KnownManagedTypeSpec)
                        {
                            variableTypes[sourceVariables[i]] = phiSpec;
                        }
                    }
                } else
                {
                    var highestType = sourceTypeSpecs.OfType<HintedImmediateTypeSpec>().Max(x => x.m_immediateType);
                    phiSpec = new HintedImmediateTypeSpec(highestType);
                }

                variableTypes[phiVariable.Key] = phiSpec;
            }

            foreach (var moveSourcePair in neededLocals.m_moveSources)
            {
                var sourceType = variableTypes[moveSourcePair.Key];
                foreach (var variable in moveSourcePair.Value)
                {
                    variableTypes[variable] = sourceType;
                }
            }

            return variableTypes;
        }

        private void TraverseBlock(ControlFlowNode<Instruction> node)
        {
            if (m_theStack.Contains(node.Id)) return;

            Il2CppSymbolicProgramState? previousState = null;
            if (m_theStack.Count > 0)
            {
                var prevOffset = m_theStack.Peek();
                previousState = m_states[prevOffset];
            }

            var state = new Il2CppSymbolicProgramState(previousState);

            Console.Out.WriteLine($"{string.Join("", Enumerable.Repeat(" ", m_theStack.Count))}{node.Id:X}");
            foreach (var instruction in node.Contents.Instructions)
            {
                state.ProcessInstruction(instruction);
            }
            
            // todo: only need to actually handle one of the paths to this bb
            
            m_states[node.Id] = state;
            m_theStack.Push(node.Id);
            foreach (var successor in node.GetSuccessors())
            {
                TraverseBlock(successor);
            }

            m_theStack.Pop();
        }
    }
}