using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Ast;
using Echo.ControlFlow;
using Echo.Core.Code;
using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo.Ast
{
    public static class DeferredReadFixer
    {
        public static void HandleDeferredReads(ControlFlowGraph<Statement<LiftedAction>> cfg)
        {
            var mapping = new Dictionary<IVariable, IVariable>();
            var blocksToReorder = new HashSet<ControlFlowNode<Statement<LiftedAction>>>();
            
            foreach (var node in cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions)
                {
                    if (statement is not AssignmentStatement<LiftedAction> assignment)
                    {
                        continue;
                    }
                    
                    if (assignment.Expression is not InstructionExpression<LiftedAction> instructionExpression)
                    {
                        throw new NotImplementedException(assignment.Expression.ToString());
                    }

                    if (instructionExpression.Instruction is not IDeferVariableRead deferVariableRead) continue;
                    
                    var targetAction = deferVariableRead.ToAction();
                    var targetAssignment = FindAssignmentForInstruction(cfg, targetAction);
                        
                    blocksToReorder.Add(node);
                    mapping.Add(assignment.Variables[0], targetAssignment.Variables[0]);
                }
            }

            var walker = new VariableRemapWalker(mapping);
            foreach (var node in cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions)
                {
                    statement.Accept(walker, null);
                }
            }

            var writtenVariablesInBlock = new WrittenVariableWalker<LiftedAction>();
            var statementReadVariables = new ReadVariableWalker<LiftedAction>();
            var newStatementOrder = new List<Statement<LiftedAction>>();

            foreach (var block  in blocksToReorder)
            {
                newStatementOrder.Clear();
                writtenVariablesInBlock.Clear();
                foreach (var statement in block.Contents.Instructions)
                {
                    statement.Accept(writtenVariablesInBlock, null);
                }

                var definedVariables = new HashSet<IVariable>();
                var deferredStatements = new Dictionary<IVariable, List<Statement<LiftedAction>>>();
                
                void DefineStatement(Statement<LiftedAction> daStatement)
                {
                    newStatementOrder.Add(daStatement);
                        
                    var statementWrittenVariables = new WrittenVariableWalker<LiftedAction>();
                        
                    statementWrittenVariables.Clear();
                    daStatement.Accept(statementWrittenVariables, null);

                    foreach (var variable in statementWrittenVariables.Variables)
                    {
                        if (!definedVariables.Add(variable)) continue;
                        if (!deferredStatements.Remove(variable, out var waitingList)) continue;

                        foreach (var waitingStatement in waitingList)
                        {
                            DefineStatement(waitingStatement);
                        }
                    }
                }

                foreach (var statement in block.Contents.Instructions)
                {
                    statementReadVariables.Clear();
                    statement.Accept(statementReadVariables, null);

                    statementReadVariables.Variables.IntersectWith(writtenVariablesInBlock.Variables); // remove anything not defined in this block
                    statementReadVariables.Variables.ExceptWith(definedVariables); // remove anything already defined
                    // remaining is variables that are not defined yet

                    if (statementReadVariables.Count != 0)
                    {
                        var firstWaitingVariable = statementReadVariables.Variables.First();

                        if (!deferredStatements.TryGetValue(firstWaitingVariable, out var waitingList))
                        {
                            waitingList = new List<Statement<LiftedAction>>();
                            deferredStatements[firstWaitingVariable] = waitingList;
                        }
                        waitingList.Add(statement);
                        continue;
                    }

                    DefineStatement(statement);
                }
                
                block.Contents.Instructions.Clear();
                foreach (var statement in newStatementOrder)
                {
                    block.Contents.Instructions.Add(statement);
                }
            }
        }

        private static AssignmentStatement<LiftedAction> FindAssignmentForInstruction(ControlFlowGraph<Statement<LiftedAction>> cfg, LiftedAction action)
        {
            foreach (var node in cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions)
                {
                    if (statement is not AssignmentStatement<LiftedAction> assignment)
                    {
                        continue;
                    }
                    if (assignment.Expression is not InstructionExpression<LiftedAction> instructionExpression)
                    {
                        throw new NotImplementedException(assignment.Expression.ToString());
                    }

                    if (instructionExpression.Instruction == action)
                    {
                        return assignment;
                    }
                }
            }
            throw new Exception("couldn't find");
        }
    }
}