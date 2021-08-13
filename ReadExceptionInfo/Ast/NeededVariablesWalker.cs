using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Echo.Ast;
using Echo.ControlFlow;
using Echo.Core.Code;
using ReadExceptionInfo.Actions;

namespace ReadExceptionInfo.Ast
{
    public class NeededVariablesWalker
    {
        private readonly ControlFlowGraph<Statement<LiftedAction>> m_cfg;
        public readonly Dictionary<IVariable, IVariable[]> m_neededPhi = new Dictionary<IVariable, IVariable[]>();
        public readonly Dictionary<IVariable, AssignmentStatement<LiftedAction>> m_neededAssignments = new Dictionary<IVariable, AssignmentStatement<LiftedAction>>();
        public readonly HashSet<InstructionExpression<LiftedAction>> m_neededInstructions = new HashSet<InstructionExpression<LiftedAction>>();
        public readonly Dictionary<IVariable, HashSet<IVariable>> m_moveSources = new Dictionary<IVariable, HashSet<IVariable>>();
        public readonly Dictionary<IVariable, IVariable> m_moveOriginalDefinitions = new Dictionary<IVariable, IVariable>();
        public readonly Dictionary<IVariable, ControlFlowNode<Statement<LiftedAction>>> m_variableDefinitionLocations = new Dictionary<IVariable, ControlFlowNode<Statement<LiftedAction>>>();
            
        private readonly HashSet<IVariable> m_pendingMoves = new HashSet<IVariable>();

        public NeededVariablesWalker(ControlFlowGraph<Statement<LiftedAction>> cfg)
        {
            m_cfg = cfg;

            foreach (var node in cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions)
                {
                    if (statement is ExpressionStatement<LiftedAction> expressionStatement)
                    {
                        if (!HandleExpression(expressionStatement.Expression)) continue;
                        AddNeededInstruction(expressionStatement.Expression);
                    } else if (statement is AssignmentStatement<LiftedAction> assignmentStatement)
                    {
                        if (!HandleExpression(assignmentStatement.Expression)) continue;
                        AddAssignmentFromSearch(assignmentStatement);
                    }
                }
            }

            bool HandleExpression(Expression<LiftedAction> expression)
            {
                if (expression is not InstructionExpression<LiftedAction> instructionExpression)
                {
                    throw new NotImplementedException(expression.ToString());
                }

                if (instructionExpression.Instruction is not IPreserveAction)
                {
                    // we don't need this for final control flow
                    return false;
                }

                foreach (var readVariable in instructionExpression.Arguments)
                {
                    FindVariableOriginalAssignment2(readVariable.GetVariable());
                }

                return true;
            }
        }

        private void AddPhi(PhiStatement<LiftedAction> phi)
        {
            m_neededPhi[phi.Target] = phi.Sources.Select(x => x.Variable).ToArray();
            FlushMoves(phi.Target); // flush reads from this phi
        }

        private void AddPendingMove(IVariable dest)
        {
            m_pendingMoves.Add(dest);
        }

        // todo: avoid duplicate work here. function runs many times per assignment
        private void AddAssignmentVariables(AssignmentStatement<LiftedAction> assignment)
        {
            // todo: only one assignment supported due to move flushing
            Debug.Assert(assignment.Variables.Count == 1);

            foreach (var variableSet in assignment.Variables)
            {
                if (m_neededAssignments.TryGetValue(variableSet, out var existingAssignment))
                {
                    Debug.Assert(existingAssignment == assignment);
                }

                m_neededAssignments[variableSet] = assignment;

                FlushMoves(variableSet);

                break;
            }

            AddNeededInstruction(assignment.Expression);
        }

        private void AddAssignmentFromSearch(AssignmentStatement<LiftedAction> assignment)
        {
            // todo: only one assignment supported due to move flushing
            Debug.Assert(assignment.Variables.Count == 1);

            foreach (var variableSet in assignment.Variables)
            {
                m_neededAssignments[variableSet] = assignment;
                break;
            }

            AddNeededInstruction(assignment.Expression);
        }

        private void FlushMoves(IVariable variable)
        {
            if (m_pendingMoves.Count == 0) return;

            if (!m_moveSources.TryGetValue(variable, out var movesFromVar))
            {
                movesFromVar = new HashSet<IVariable>();
                m_moveSources[variable] = movesFromVar;
            }

            movesFromVar.UnionWith(m_pendingMoves);

            foreach (var move in m_pendingMoves)
            {
                m_moveOriginalDefinitions[move] = variable;
            }

            m_pendingMoves.Clear();
        }

        private void AddNeededInstruction(Expression<LiftedAction> expressionStatementExpression)
        {
            m_neededInstructions.Add((InstructionExpression<LiftedAction>) expressionStatementExpression);
        }

        private void FindVariableOriginalAssignment2(IVariable variable)
        {
            START:

            foreach (var node in m_cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions)
                {
                    if (statement is PhiStatement<LiftedAction> phi)
                    {
                        if (!Equals(phi.Target, variable)) continue;

                        AddPhi(phi);
                        m_variableDefinitionLocations[phi.Target] = node;

                        foreach (VariableExpression<LiftedAction> phiSource in phi.Sources)
                        {
                            FindVariableOriginalAssignment2(phiSource.Variable);
                        }

                        return;
                    }

                    if (statement is not AssignmentStatement<LiftedAction> assignment)
                    {
                        continue;
                    }

                    foreach (var assignedVariable in assignment.Variables)
                    {
                        m_variableDefinitionLocations[assignedVariable] = node;
                    }

                    if (!assignment.Variables.Contains(variable))
                    {
                        continue;
                    }

                    if (assignment.Expression is not InstructionExpression<LiftedAction> instructionExpression)
                    {
                        throw new NotImplementedException(assignment.Expression.ToString());
                    } 
                    
                    if (instructionExpression.Instruction is MoveAction)
                    {
                        var moveSource = ((VariableExpression<LiftedAction>) instructionExpression.Arguments[0]).Variable;
                        var moveDest = assignment.Variables[0];

                        AddNeededInstruction(assignment.Expression);
                        AddPendingMove(moveDest);

                        variable = moveSource;
                        goto START;
                    } 
                    
                    AddAssignmentVariables(assignment);

                    foreach (var argument in instructionExpression.Arguments)
                    {
                        if (argument is not VariableExpression<LiftedAction> argumentVariable)
                        {
                            throw new NotImplementedException();
                        }

                        FindVariableOriginalAssignment2(argumentVariable.Variable);
                    }

                    return;
                }
            }

            throw new Exception();
        }

        public void RemoveUnneededInstructions()
        {
            foreach (var node in m_cfg.Nodes)
            {
                foreach (var statement in node.Contents.Instructions.ToArray())
                {
                    if (statement is not AssignmentStatement<LiftedAction> assignment)
                    {
                        continue;
                    }
                    if (assignment.Expression is not InstructionExpression<LiftedAction> instructionExpression)
                    {
                        throw new NotImplementedException(assignment.Expression.ToString());
                    }
                    if (m_neededInstructions.Contains(instructionExpression)) continue;
                    
                    node.Contents.Instructions.Remove(statement);
                }
            }
        }
    }
}