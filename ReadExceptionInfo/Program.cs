using System;
using System.Buffers.Binary;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ArcticFox.Codec.Binary;
using AsmResolver.PE;
using AsmResolver.PE.Exceptions.X64;
using AsmResolver.PE.File;
using Cpp2IL.Core;
using Iced.Intel;

namespace ReadExceptionInfo
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var cpp2IlArgs = Cpp2IL.Program.GetRuntimeOptionsFromCommandLine(new[]
            {
                @"--game-path",
                @"D:\re\il2cpp\Il2CppTests\TestBinaries\GameAssembly-MyAttribute-x64",
                @"--force-binary-path",
                @"D:\re\il2cpp\Il2CppTests\Il2CppInspector\Il2CppTests\TestBinaries\GameAssembly-MyAttribute-x64\GameAssembly-MyAttribute-x64.dll",
                @"--force-metadata-path",
                @"D:\re\il2cpp\Il2CppTests\Il2CppInspector\Il2CppTests\TestBinaries\GameAssembly-MyAttribute-x64\global-metadata.dat",
                @"--force-unity-version",
                @"2019.4.15"
            });

            var peFile = PEFile.FromFile(cpp2IlArgs.PathToAssembly);
            var peImage = PEImage.FromFile(peFile);
            
            Cpp2IlApi.InitializeLibCpp2Il(cpp2IlArgs.PathToAssembly, cpp2IlArgs.PathToMetadata, cpp2IlArgs.UnityVersion, cpp2IlArgs.EnableVerboseLogging, cpp2IlArgs.EnableRegistrationPrompts);
            Cpp2IlApi.MakeDummyDLLs();
            var keyFunctions = Cpp2IlApi.ScanForKeyFunctionAddresses();

            foreach (var exceptionEntry in peImage.Exceptions.GetEntries())
            {
                var funcBaseVA = peImage.ImageBase + exceptionEntry.Begin.Rva;
                if (funcBaseVA != 0x180794910)
                {
                    continue;
                }
                
                var funcEndVA = peImage.ImageBase + exceptionEntry.End.Rva;
                var funcReader = peFile.CreateReaderAtRva(exceptionEntry.Begin.Rva, 500000);
                var funcCodeBytes = funcReader.ReadToEnd();
                var disassembler = Decoder.Create(64, funcCodeBytes, funcBaseVA);

                ControlFlowTest.DoCFG(funcCodeBytes, funcBaseVA, keyFunctions);

                var x64ExceptionEntry = (X64RuntimeFunction) exceptionEntry;

                var rvaReader = x64ExceptionEntry.UnwindInfo.ExceptionHandlerData.CreateReader();
                while (rvaReader.Rva % 4 != 0)
                {
                    // why does it not align by default
                    rvaReader.ReadByte();
                }
                var rva = rvaReader.ReadUInt32();

                var funcInfoReader = CreateEHReaderAtRva(peFile, rva);
                
                var isCatch = funcInfoReader.ReadBit();
                var isSeparated = funcInfoReader.ReadBit();
                var BBT = funcInfoReader.ReadBit();
                var UnwindMap = funcInfoReader.ReadBit();
                var TryBlockMap = funcInfoReader.ReadBit();
                var EHs = funcInfoReader.ReadBit();
                var NoExcept = funcInfoReader.ReadBit();
                var reserved = funcInfoReader.ReadBit();

                if (BBT)
                {
                    throw new Exception();
                }
                
                if (UnwindMap)
                {
                    var dispUnwindMap = funcInfoReader.ReadUInt32LittleEndian();
                }

                TryBlock4[]? tryBlocks = null;
                
                if (TryBlockMap)
                {
                    var dispTryBlockMap = funcInfoReader.ReadUInt32LittleEndian();
                    tryBlocks = ReadTryBlock4(peFile, dispTryBlockMap, funcBaseVA);
                }
                
                IPToStateData[]? ipToStateMap = null;

                if (isSeparated)
                {
                    var SepIPtoStateMap = funcInfoReader.ReadUInt32LittleEndian();
                    throw new Exception();
                } else
                {
                    var IPtoStateMapRva = funcInfoReader.ReadUInt32LittleEndian();
                    ipToStateMap = ReadIPToStateMap4(peFile, IPtoStateMapRva, funcBaseVA);
                }

                if (isCatch)
                {
                    throw new Exception();
                    //var dispFrame = ReadCompressedUnsigned(ref reader);
                }

                Debug.Assert(tryBlocks != null);
                Debug.Assert(ipToStateMap != null);
                
                var tryLowsMap = tryBlocks.ToDictionary(x => x.tryLow, x => x);

                var currentStateIndex = 0;
                var currentState = -1;

                Dictionary<int, List<Instruction>> delayedInstructions = new Dictionary<int, List<Instruction>>();
                var continuationStack = new Stack<int>();
                var continuationRestorePointStack = new Stack<int>();
                var tryStack = new Stack<TryBlock4>();
                
                var writer = new IndentedTextWriter(new StringWriter());

                void ChangeState(int newState)
                {
                    var previousState = currentState;
                    currentState = newState;
                    
                    if (continuationStack.Count > 0)
                    {
                        continuationStack.Pop();
                        previousState = continuationRestorePointStack.Pop();
                    }

                    if (tryStack.Count > 0 && currentState > tryStack.Peek().tryHigh)
                    {
                        var blockToExit = tryStack.Pop();
                        FinishTry(blockToExit);
                    }

                    if (currentState > previousState)
                    {
                        for (var stateAdded = previousState+1; stateAdded <= currentState; stateAdded++)
                        {
                            Console.Out.WriteLine($"state up {stateAdded}");

                            if (!tryLowsMap.TryGetValue((uint)stateAdded, out var tryBlock)) continue;

                            if (tryBlock.tryLow <= currentState && tryBlock.tryHigh >= currentState)
                            {
                                tryStack.Push(tryBlock);
                            
                                writer.WriteLine("try {");
                                writer.Indent++;
                            }
                        }
                    } else if (previousState > currentState)
                    {
                        Console.Out.WriteLine("state down");
                        
                        for (var stateRemoved = previousState; stateRemoved > currentState; stateRemoved--)
                        {
                            Console.Out.WriteLine($"state down {stateRemoved}");
                        }

                        if (tryLowsMap.TryGetValue((uint)previousState, out var tryBlock) && tryStack.Count > 0 && tryStack.Peek().tryLow == previousState)
                        {
                            tryStack.Pop();
                            FinishTry(tryBlock);
                        }
                        if (previousState - 1 != currentState && currentState == -1)
                        {
                            // todo: should be checking -1 only?
                            Console.Out.WriteLine("dropped too many, we have fallen through to continuation code");
                            continuationRestorePointStack.Push(previousState);
                            continuationStack.Push(currentState);
                        } 
                    }

                    if (delayedInstructions.Remove(currentState, out var tooDown))
                    {
                        foreach (var delayedIns in tooDown)
                        {
                            writer.WriteLine($"{currentState} {delayedIns.IP:X} {delayedIns}");
                        }
                    }
                }

                void FinishTry(TryBlock4 tryBlock)
                {
                    writer.Indent--;
                    writer.WriteLine("}");
                            
                    foreach (var handler in tryBlock.handlers)
                    {
                        var continuation1 = handler.m_continuation1;
                        var continuation2 = handler.m_continuation2;
                            
                        writer.WriteLine("catch {");
                        writer.Indent++;
                                
                        writer.WriteLine($"call catch funclet at {peImage.ImageBase + handler.m_handlerRva:X}");

                        if (continuation1 != 0 && continuation2 == 0)
                        {
                            writer.WriteLine($"will jump to {continuation1:X}");
                        } else if (continuation1 != 0 && continuation2 != 0)
                        {
                            writer.WriteLine($"could jump to {continuation1:X} or {continuation2:X} based off funclet return value");
                        } else
                        {
                            writer.WriteLine("continuation code pointer is returned by funclet");
                        }
                                
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                }

                while (disassembler.IP < funcEndVA)
                {
                    var ins = disassembler.Decode();
                    Debug.Assert(!ins.IsInvalid);

                    if (currentStateIndex >= ipToStateMap.Length)
                    {
                        currentState = -1;
                    } else if (disassembler.IP >= ipToStateMap[currentStateIndex].m_ip)
                    {
                        ChangeState(ipToStateMap[currentStateIndex].m_state);
                        currentStateIndex++;
                    }

                    if (continuationStack.Count > 0)
                    {
                        var currentContinuationState = continuationStack.Peek();
                        if (!delayedInstructions.TryGetValue(currentContinuationState, out var tooDown))
                        {
                            tooDown = new List<Instruction>();
                            delayedInstructions[currentContinuationState] = tooDown;
                        }
                        tooDown.Add(ins);
                        continue;
                    }
                    writer.WriteLine($"{currentState} {ins.IP:X} {ins}");
                }
                while (continuationRestorePointStack.Count > 0)
                {
                    var peek = continuationRestorePointStack.Peek();
                    
                    // todo: what happens when > 1 in stack
                    // is it even possible

                    while (peek > -1)
                    {
                        ChangeState(peek-1);
                        peek -= 1;
                    }
                }
                
                Console.Out.WriteLine(writer.InnerWriter.ToString());
            }
        }

        private static BitReader CreateEHReaderAtRva(PEFile peFile, uint rva)
        {
            var dataReader = peFile.CreateReaderAtRva(rva);

            var buffer = new byte[27]; // maxBufferSize = 27
            dataReader.ReadBytes(buffer, 0, buffer.Length);
            
            return new BitReader(buffer);
        }

        private record TryBlock4(uint tryLow, uint tryHigh, uint catchHigh, Handler4[] handlers);
        private record Handler4(uint m_handlerRva, ulong m_continuation1, ulong m_continuation2);
        private record IPToStateData(ulong m_ip, int m_state);


        private enum ContType : byte
        {
            NONE = 0,
            ONE = 1, 
            TWO = 2,
            RESERVED = 3
        }
        
        
        private static TryBlock4[] ReadTryBlock4(PEFile file, uint rva, ulong funcBase)
        {
            var reader = CreateEHReaderAtRva(file, rva);

            var numTryBlocks = ReadCompressedUnsigned(ref reader);
            var array = new TryBlock4[numTryBlocks];

            for (var i = 0; i < numTryBlocks; i++)
            {
                var tryLow = ReadCompressedUnsigned(ref reader);
                var tryHigh = ReadCompressedUnsigned(ref reader);
                var catchHigh = ReadCompressedUnsigned(ref reader);
                var dispHandlerArray = reader.ReadUInt32LittleEndian();
                
                Console.Out.WriteLine($"try {i}: tryLow:{tryLow} tryHigh:{tryHigh} catchHigh:{catchHigh} dispHandlerArray:{dispHandlerArray:X}");

                var handlers = ReadHandlerMap4(file, dispHandlerArray, funcBase);
                
                array[i] = new TryBlock4(tryLow, tryHigh, catchHigh, handlers);
            }

            return array;
        }

        private static Handler4[] ReadHandlerMap4(PEFile file, uint rva, ulong funcBase)
        {
            var reader = CreateEHReaderAtRva(file, rva);
            
            var numHandlers = ReadCompressedUnsigned(ref reader);
            var array = new Handler4[numHandlers];

            for (var i = 0; i < numHandlers; i++)
            {
                var hasAdjectives = reader.ReadBit();
                var hasDispType = reader.ReadBit();
                var hasDispCatchObj = reader.ReadBit();
                var contIsRVA = reader.ReadBit();
                var contAddrType = reader.ReadBits<ContType>(2);
                var unused = reader.ReadBits<byte>(2);

                uint adjectives = 0;
                var dispType = 0;
                uint dispCatchObj = 0;
                
                if (hasAdjectives)
                {
                    adjectives = ReadCompressedUnsigned(ref reader);
                }
                if (hasDispType)
                {
                    dispType = reader.ReadInt32LittleEndian();
                }
                if (hasDispCatchObj)
                {
                    dispCatchObj = ReadCompressedUnsigned(ref reader);
                }
                
                var dispOfHandler = reader.ReadUInt32LittleEndian();
                
                Console.Out.WriteLine($"    handler {i}: adjectives:{adjectives} dispType:{dispType:X} dispCatchObj:{dispCatchObj:X} dispOfHandler:{dispOfHandler:X}");

                ulong continuation1 = 0;
                ulong continuation2 = 0;
                
                if (contIsRVA)
                {
                    throw new Exception();
                } else
                {
                    if (contAddrType == ContType.ONE)
                    {
                        continuation1 = funcBase + ReadCompressedUnsigned(ref reader);
                        
                        Console.Out.WriteLine($"       continuation: {continuation1:X}");
                    } else if (contAddrType == ContType.TWO)
                    {
                        continuation1 = funcBase + ReadCompressedUnsigned(ref reader);
                        continuation2 = funcBase + ReadCompressedUnsigned(ref reader);

                        Console.Out.WriteLine($"       continuation1: {continuation1:X}");
                        Console.Out.WriteLine($"       continuation2: {continuation2:X}");
                    }
                }
                
                array[i] = new Handler4(dispOfHandler, continuation1, continuation2);
            }
            return array;
        }

        private static IPToStateData[] ReadIPToStateMap4(PEFile file, uint rva, ulong funcBase)
        {
            var reader = CreateEHReaderAtRva(file, rva);
            
            var numEntries = ReadCompressedUnsigned(ref reader);
            var array = new IPToStateData[numEntries];
            
            var prevIP = 0u;
            for (var i = 0; i < numEntries; i++)
            {
                var ip = prevIP + ReadCompressedUnsigned(ref reader);
                var state = (int)(ReadCompressedUnsigned(ref reader) - 1);

                var vIP = funcBase + ip;
                Console.Out.WriteLine($"{vIP:X}: state {state}");
                array[i] = new IPToStateData(vIP, state);

                prevIP = ip;
            }

            return array;
        }

        private static ReadOnlySpan<sbyte> s_negLengthTab => new ReadOnlySpan<sbyte>(new sbyte[]
        {
            -1, // 0
            -2, // 1
            -1, // 2
            -3, // 3

            -1, // 4
            -2, // 5
            -1, // 6
            -4, // 7

            -1, // 8
            -2, // 9
            -1, // 10
            -3, // 11

            -1, // 12
            -2, // 13
            -1, // 14
            -5, // 15
        });
        
        private static ReadOnlySpan<byte> s_shiftTab => new ReadOnlySpan<byte>(new byte[]
        {
            32 - 7 * 1,    // 0
            32 - 7 * 2,    // 1
            32 - 7 * 1,    // 2
            32 - 7 * 3,    // 3

            32 - 7 * 1,    // 4
            32 - 7 * 2,    // 5
            32 - 7 * 1,    // 6
            32 - 7 * 4,    // 7

            32 - 7 * 1,    // 8
            32 - 7 * 2,    // 9
            32 - 7 * 1,    // 10
            32 - 7 * 3,    // 11

            32 - 7 * 1,    // 12
            32 - 7 * 2,    // 13
            32 - 7 * 1,    // 14
            0,             // 15
        });
        
        private static uint ReadCompressedUnsigned(ref BitReader reader)
        {
            var lengthBits = reader.ReadByte() & 0x0F;
            reader.SkipBytes(-1);

            var length = -s_negLengthTab[lengthBits];
            var shift = s_shiftTab[lengthBits];

            Span<byte> data = new byte[4];
            
            // todo: cpp version reads invalid memory, hard to translate
            
            var readStart = length - 4;
            if (readStart > 0)
            {
                // on 5, skip first byte
                reader.SkipBytes(readStart);
            }
            reader.ReadBytesTo(data.Slice(data.Length - length), length);
            
            var asInt = BinaryPrimitives.ReadUInt32LittleEndian(data);
            asInt >>= shift;

            return asInt;
        }
    }
}