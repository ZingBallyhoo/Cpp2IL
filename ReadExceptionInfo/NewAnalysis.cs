using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Echo.ControlFlow;
using Iced.Intel;

namespace ReadExceptionInfo
{
    public class NewAnalysis
    {
        private readonly ControlFlowGraph<Instruction> m_cfg;
        private readonly Stack<long> m_theStack = new Stack<long>();
        
        public NewAnalysis(ControlFlowGraph<Instruction> cfg)
        {
            m_cfg = cfg;
        }

        public void Do()
        {
            var entrypoint = m_cfg.Entrypoint;
            Debug.Assert(entrypoint != null);

            TraverseBlock(entrypoint);
        }

        private void TraverseBlock(ControlFlowNode<Instruction> node)
        {
            if (m_theStack.Contains(node.Offset)) return;
            
            Console.Out.WriteLine($"{string.Join("", Enumerable.Repeat(" ", m_theStack.Count))}{node.Id}");
            
            m_theStack.Push(node.Id);
            foreach (var successor in node.GetSuccessors())
            {
                TraverseBlock(successor);
            }
            m_theStack.Pop();
        }
    }

    public class AnalysisContext
    {
        
    }
}