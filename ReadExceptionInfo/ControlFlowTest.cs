using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cpp2IL.Core;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Analysis.Domination;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Construction;
using Echo.ControlFlow.Construction.Static;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.ControlFlow.Serialization.Dot;
using Echo.Core.Graphing.Serialization.Dot;
using Echo.DataFlow;
using Echo.Platforms.Iced;
using Iced.Intel;

namespace ReadExceptionInfo
{
    public static class ControlFlowTest
    {
        public static ControlFlowGraph<Instruction> ConstructStaticFlowGraph(byte[] rawCode, long entrypoint, ulong baseAddress)
        {
            var architecture = new X86Architecture();
            var instructionProvider = new X86DecoderInstructionProvider(architecture, new MemoryStream(rawCode, false), 64, baseAddress, DecoderOptions.None);

            var cfgBuilder = new StaticFlowGraphBuilder<Instruction>(
                instructionProvider,
                new X86StaticSuccessorResolver());

            return cfgBuilder.ConstructFlowGraph(entrypoint);
        }
        
        public static (ControlFlowGraph<Instruction> Cfg, DataFlowGraph<Instruction> Dfg) ConstructSymbolicFlowGraph2(ControlFlowGraph<Instruction> cfg, long entrypoint)
        {
            var architecture = new X86Architecture();
            var instructionProvider = new GraphBasedInstructionProvider<Instruction>(architecture, cfg);
            var dfgBuilder = new GraphBasedStateTransitionResolver<Instruction>(architecture, cfg);
            
            var cfgBuilder = new SymbolicFlowGraphBuilder<Instruction>(
                instructionProvider,
                dfgBuilder);

            var symbolicGraph = cfgBuilder.ConstructFlowGraph(entrypoint);
            
            var astBuilder = new AstParser<Instruction>(symbolicGraph, dfgBuilder.DataFlowGraph);
            var parsedAST = astBuilder.Parse();
            var dotWriter = new StringWriter();
            parsedAST.ToDotGraph(dotWriter);
            
            return (symbolicGraph, dfgBuilder.DataFlowGraph);
        }
        
        private static HashSet<long> s_branchEliminatedBBsTemp = new HashSet<long>();
        
        private static void EliminateRuntimeInitNode(ControlFlowNode<Instruction> controlFlowNode)
        {
            var predecessors = controlFlowNode.GetPredecessors().ToArray();
            var originalUnconditionalNeighbour = controlFlowNode.UnconditionalNeighbour;
            Debug.Assert(originalUnconditionalNeighbour != null);
            
            controlFlowNode.Contents.Instructions.Clear();
            controlFlowNode.Disconnect(); // todo: is this safe. i guess it is cos we mutate all predecessors
            
            foreach (var predecessor in predecessors)
            {
                EliminateBranch(predecessor.Contents);
                
                predecessor.ConditionalEdges.Clear();
                predecessor.UnconditionalNeighbour = null;
                predecessor.ConnectWith(originalUnconditionalNeighbour);
                
                if (predecessor.Contents.IsEmpty)
                {
                    EliminateRuntimeInitNode(predecessor);
                }
            }
        }

        private static void EliminateExceptionThrowerNode(ControlFlowNode<Instruction> controlFlowNode)
        {
            var predecessors = controlFlowNode.GetPredecessors().ToArray();
            Debug.Assert(controlFlowNode.UnconditionalNeighbour == null);
            
            controlFlowNode.Contents.Instructions.Clear();
            controlFlowNode.Disconnect();

            foreach (var predecessor in predecessors)
            {
                Debug.Assert(predecessor.UnconditionalNeighbour != null);
                Debug.Assert(predecessor.ConditionalEdges.Count == 0);
                
                EliminateBranch(predecessor.Contents);
                
                /*Debug.Assert(predecessor.UnconditionalNeighbour == null);
                
                EliminateBranch(predecessor.Contents);

                var conditionalEdge = predecessor.GetSuccessors().Single();
                
                predecessor.ConditionalEdges.Clear();
                predecessor.UnconditionalNeighbour = null;

                predecessor.ConnectWith(conditionalEdge);*/
            }
        }
        
        public static void EliminateBranch(BasicBlock<Instruction> basicBlock)
        {
            if (basicBlock.IsEmpty) return;
            if (!s_branchEliminatedBBsTemp.Add(basicBlock.Offset)) return;

            var branchInstruction = basicBlock.Footer;
            Debug.Assert(branchInstruction.FlowControl == FlowControl.ConditionalBranch || branchInstruction.FlowControl == FlowControl.UnconditionalBranch);
            basicBlock.Instructions.Remove(branchInstruction);
            
            if (branchInstruction.FlowControl == FlowControl.UnconditionalBranch)
            {
                return;
            }

            for (var i = basicBlock.Instructions.Count-1; i >= 0; i--)
            {
                var instruction = basicBlock.Instructions[i];
                if ((instruction.RflagsModified & branchInstruction.RflagsRead) != 0)
                {
                    // todo: could compiler reuse same flag set for another test/cmp? would break this
                    
                    basicBlock.Instructions.RemoveAt(i);
                    return;
                }
            }
            Debug.Assert(false);
        }

        public static void EliminateUnneededBlocks(ControlFlowGraph<Instruction> cfg)
        {
            foreach (var node in cfg.SortNodes().Reverse()) // since we are merging successor, go backwards. todo: idk how graphs work
            {
                var unconditionalSuccessor = node.UnconditionalNeighbour;
                if (unconditionalSuccessor == null || node.ConditionalEdges.Count > 0) continue;
                
                if (unconditionalSuccessor.GetIncomingEdges().Count() > 1) continue;
                
                if (node.UnconditionalEdge.Type != ControlFlowEdgeType.FallThrough) EliminateBranch(node.Contents);
                node.MergeWithSuccessor();
            }

            foreach (var controlFlowNode in cfg.Nodes.ToArray())
            {
                if (controlFlowNode.GetIncomingEdges().Any() || controlFlowNode == cfg.Entrypoint) continue;
                
                controlFlowNode.ParentGraph.Nodes.Remove(controlFlowNode);
            }
        }
        
        
        public static void DoCFG(byte[] funcCodeBytes, ulong funcBaseVA, KeyFunctionAddresses keyFunctions)
        {
            var controlFlowGraph = ConstructStaticFlowGraph(funcCodeBytes, (long) funcBaseVA, funcBaseVA);

            //EliminateExceptionThrowerNode(controlFlowGraph.Nodes[0x180794908]); // ambig loop
            EliminateExceptionThrowerNode(controlFlowGraph.Nodes[0x1807949B3]); // ambig stream

            foreach (var controlFlowNode in controlFlowGraph.Nodes)
            {
                if (controlFlowNode.Contents.Instructions.Any(x =>
                    x.Mnemonic == Mnemonic.Call &&
                    (x.NearBranchTarget == keyFunctions.il2cpp_codegen_initialize_method && keyFunctions.il2cpp_codegen_initialize_method != 0 ||
                     x.NearBranchTarget == keyFunctions.il2cpp_vm_metadatacache_initializemethodmetadata && keyFunctions.il2cpp_vm_metadatacache_initializemethodmetadata != 0)))
                {
                    // metadata init
                    EliminateRuntimeInitNode(controlFlowNode);
                }

                if (controlFlowNode.Contents.Instructions.Any(x =>
                    x.Mnemonic == Mnemonic.Call &&
                    (x.NearBranchTarget == keyFunctions.il2cpp_runtime_class_init_actual && keyFunctions.il2cpp_runtime_class_init_actual != 0||
                     x.NearBranchTarget == keyFunctions.il2cpp_runtime_class_init_export && keyFunctions.il2cpp_runtime_class_init_export != 0)))
                {
                    // static class init
                    EliminateRuntimeInitNode(controlFlowNode);
                }
            }

            EliminateUnneededBlocks(controlFlowGraph);

            var dotWriter = new StringWriter();
            var dotWriter0 = new DotWriter(dotWriter)
            {
                NodeAdorner = new ControlFlowNodeAdorner<Instruction>(new X86InstructionFormatter()),
                EdgeAdorner = new ControlFlowEdgeAdorner<Instruction>(),
                SubGraphAdorner = new ExceptionHandlerAdorner<Instruction>(),
            };
            dotWriter0.Write(controlFlowGraph);

            //var dotWriter2 = new StringWriter();
            //symbolicGraph.ToDotGraph(dotWriter2);
            //
            //var dotWriter3 = new StringWriter();
            //dataFlowGraph.ToDotGraph(dotWriter3);

            var dotWriter4 = new StringWriter();
            var dotWriter5 = new DotWriter(dotWriter4);
            var dominatorTree = DominatorTree<Instruction>.FromGraph(controlFlowGraph);
            dotWriter5.Write(dominatorTree);
            
            var (symbolicGraph2, dataFlowGraph2) = ConstructSymbolicFlowGraph2(controlFlowGraph, (long)funcBaseVA);
            
            var dotWriter2 = new StringWriter();
            symbolicGraph2.ToDotGraph(dotWriter2);

            var analysis = new NewAnalysis(controlFlowGraph);
            analysis.Do();

            foreach (var node in controlFlowGraph.Nodes)
            {
                var dominanceFrontier = dominatorTree.GetDominanceFrontier(node);
                Console.Out.Write($"dominance frontier of {node}: ");
                foreach (var frontierNode in dominanceFrontier)
                {
                    Console.Out.Write($"{frontierNode} ");
                }

                Console.Out.WriteLine();
            }
        }
    }
}