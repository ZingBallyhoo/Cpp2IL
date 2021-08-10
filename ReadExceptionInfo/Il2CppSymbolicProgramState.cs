using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cpp2IL.Core;
using Iced.Intel;
using LibCpp2IL;
using ReadExceptionInfo.Actions;
using ReadExceptionInfo.Tracing;

namespace ReadExceptionInfo
{
    public class Il2CppSymbolicProgramState
    {
        public readonly List<LiftedAction> m_actions;
        private readonly Dictionary<Register, IInternalValue> m_runtimeInternalValues;

        private static KeyFunctionAddresses s_keyFunctions => Program.s_keyFunctions;

        public Il2CppSymbolicProgramState(Il2CppSymbolicProgramState? existing)
        {
            if (existing == null)
            {
                m_runtimeInternalValues = new Dictionary<Register, IInternalValue>();
            } else
            {
                m_runtimeInternalValues = existing.m_runtimeInternalValues.ToDictionary(x => x.Key, y => y.Value);
            }
            
            m_actions = new List<LiftedAction>();
        }
        
        public void ProcessInstruction(Instruction instruction)
        {
            if (instruction.Mnemonic == Mnemonic.Mov && instruction.Op0Kind == OpKind.Register)
            {
                if (instruction.Op1Kind == OpKind.Memory)
                {
                    if (instruction.MemoryBase == Register.RSP)
                    {
                        // todo: stack
                        return;
                    }
                    
                    if (instruction.MemoryBase != Register.RIP)
                    {
                        var internalValueInRegister = (IStructurePointer) m_runtimeInternalValues[instruction.MemoryBase];
                        var readValue = internalValueInRegister.GetValueAtOffset(instruction.MemoryDisplacement32);
                        SetRuntimeInternalValue(instruction.Op0Register, readValue);
                        return;
                    }

                    var address = instruction.IPRelativeMemoryAddress;
                    if (LibCpp2IlGlobalMapper.TypeRefsByAddress.TryGetValue(address, out var typeRefTemp))
                    {
                        var resolvedType = Utils.TryResolveTypeReflectionData(typeRefTemp.AsType());
                        Debug.Assert(resolvedType != null);
                        SetRuntimeInternalValue(instruction.Op0Register, new ConcreteTypeDefinitionValue(resolvedType.Resolve()));
                    } else
                    {
                        var strLiteral = LibCpp2IlMain.GetLiteralByAddress(address);
                        if (strLiteral != null)
                        {
                            SetRuntimeInternalValue(instruction.Op0Register, new ConstantStringValue(strLiteral));
                        } else
                        {
                            throw new NotImplementedException();
                        }
                    }
                } else if (instruction.Op1Kind == OpKind.Register)
                {
                    AddAction(new MoveAction(instruction.Op1Register, instruction.Op0Register));
                    if (m_runtimeInternalValues.TryGetValue(instruction.Op1Register, out var currentInternalValue))
                    {
                        m_runtimeInternalValues[instruction.Op0Register] = currentInternalValue;
                    }
                } else if (instruction.Op1Kind.IsImmediate())
                {
                    AddAction(new SetRegAction(instruction.Op0Register, instruction.GetImmediate(1)));
                }
            } else if (instruction.Mnemonic == Mnemonic.Xor && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Register && instruction.Op0Register == instruction.Op1Register)
            {
                AddAction(new ClearRegAction(instruction.Op0Register));
                m_runtimeInternalValues.Remove(RegisterExtensions.GetFullRegister(instruction.Op0Register)); // todo: widen, is e**
            } else if (instruction.Mnemonic == Mnemonic.Call)
            {
                var jumpTarget = instruction.NearBranchTarget;

                if (instruction.MemoryBase != Register.None)
                {
                    VirtualMethodValue? virtualMethodValue = null;

                    // todo: remove hack
                    foreach (var VARIABLE in new []
                    {
                        Register.RDX, Register.R8
                    })
                    {
                        if (!m_runtimeInternalValues.TryGetValue(VARIABLE, out var internalValue)) continue;
                        if (internalValue is VirtualMethodValue virtualMethodValue1)
                        {
                            virtualMethodValue = virtualMethodValue1;
                            break;
                        }
                    }
                    
                    Debug.Assert(virtualMethodValue != null);
                    AddAction(new CallManagedVirtualFunctionAction(virtualMethodValue));
                    return;
                }
                
                if (SharedState.MethodsByAddress.TryGetValue(jumpTarget, out var managedFunction))
                {
                    AddManagedFunctionCall(jumpTarget);
                } else if (jumpTarget == s_keyFunctions.il2cpp_codegen_object_new || jumpTarget == s_keyFunctions.il2cpp_vm_object_new || jumpTarget == s_keyFunctions.il2cpp_object_new)
                {
                    var typeObj = (ConcreteTypeDefinitionValue) m_runtimeInternalValues[Register.RCX];
                    var newObj = new NewObjectAction(typeObj);
                    AddAction(newObj);
                    
                    SetRuntimeInternalValue(Register.RAX, new NewlyCreatedReferenceTypeValue(newObj), false);
                }
            } else if (instruction.Mnemonic == Mnemonic.Add && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind.IsImmediate())
            {
                AddAction(new AddConstantAction(instruction.Op0Register, instruction.GetImmediate(1)));
            } else if (instruction.Mnemonic == Mnemonic.Inc && instruction.Op0Kind == OpKind.Register)
            {
                AddAction(new AddConstantAction(instruction.Op0Register, 1));
            } else if (instruction.Mnemonic == Mnemonic.Cmp)
            {
                if (m_runtimeInternalValues.TryGetValue(instruction.MemoryBase, out var lhsInternal))
                {
                    var lhsRead = ((IStructurePointer) lhsInternal).GetValueAtOffset(instruction.MemoryDisplacement32);
                    SetRuntimeInternalValue(Register.DontUse0, lhsRead);
                } else
                {
                    Debug.Assert(instruction.Op0Register != Register.None);
                    AddAction(new MoveAction(instruction.Op0Register, Register.DontUse0));
                }

                if (instruction.Op1Kind.IsImmediate())
                {
                    SetRuntimeInternalValue(Register.DontUseFA, new ConstantComparandValue(instruction.GetImmediate(1)));
                } else
                {
                    Debug.Assert(instruction.Op1Register != Register.None);
                    AddAction(new MoveAction(instruction.Op1Register, Register.DontUseFA));
                }
            } else if (instruction.Mnemonic == Mnemonic.Je)
            {
                AddAction(new BranchIfEqual());
            } else if (instruction.Mnemonic == Mnemonic.Jl)
            {
                AddAction(new BranchIfLess());
            } else if (instruction.Mnemonic == Mnemonic.Jmp)
            {
                AddAction(new BranchAlwaysAction());
            } else if (instruction.Mnemonic == Mnemonic.Test)
            {
                throw new NotImplementedException();
            } else if (instruction.Mnemonic == Mnemonic.Cmove)
            {
                // todo: do we want to lift conditional moves outside of here? currently always follow for debug
                
                AddAction(new MoveAction(instruction.Op1Register, instruction.Op0Register));
                //throw new NotImplementedException();
            } else if (instruction.Mnemonic == Mnemonic.Ret)
            {
                AddAction(new ReturnAction());
            }
        }

        private void AddManagedFunctionCall(ulong address)
        {
            var methodsAtAddress = SharedState.MethodsByAddress2[address];

            if (methodsAtAddress.Count != 1)
            {
                throw new NotImplementedException();
            }
            
            var methodToCall = methodsAtAddress[0];
            
            if (!methodToCall.IsStatic && m_runtimeInternalValues.TryGetValue(Register.RCX, out var instInRcx) && instInRcx is NewlyCreatedReferenceTypeValue newlyCreatedReferenceType)
            {
                newlyCreatedReferenceType.m_createdFromAction.SetConstructor(methodToCall);
                return;
            }
            
            var managedFunctionCall = new CallConcreteManagedFunction(methodToCall);
            
            AddAction(managedFunctionCall);

            if (!managedFunctionCall.GetMethod().ReturnType.IsValueType)
            {
                SetRuntimeInternalValue(Register.RAX, new ReferenceTypeValue(managedFunctionCall.GetMethod().ReturnType.Resolve()));
            }
        }
        
        private void SetRuntimeInternalValue(Register register, IInternalValue value, bool addAction=true)
        {
            var fullRegister = RegisterExtensions.GetFullRegister(register);
            m_runtimeInternalValues[fullRegister] = value;
            
            if (addAction) AddAction(new LoadInternalValueAction(fullRegister, value));
        }
        
        private void AddAction(LiftedAction instruction)
        {
            m_actions.Add(instruction);
        }
    }
}