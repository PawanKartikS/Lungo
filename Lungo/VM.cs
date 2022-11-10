using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Lungo;

class VM : IDisposable
{
    /// <summary>
    /// The assembly that we'd be running.
    /// </summary>
    private readonly Assembly _assembly;

    /// <summary>
    /// PEReader that helps extract CIL.
    /// </summary>
    private readonly PEReader _pe;

    /// <summary>
    /// Each method gets its own local variables array and operand stack.
    /// </summary>
    private readonly Stack<Frame> _frames;

    /// <summary>
    /// Helps fetch stuff like user defined strings. This is easier than dealing
    /// with streams ourselves.
    /// </summary>
    private readonly MetadataReader _mr;

    /// <summary>
    /// Instantiate a new instance of VM.
    /// While the assembly and CIL is straight forward, we'd need to
    /// parse the Metadata using the `MetadataReader` to fetch elements
    /// such as user defined strings and so on.
    /// </summary>
    /// <param name="pe"></param>
    /// <param name="assembly"></param>
    public unsafe VM(PEReader pe, Assembly assembly)
    {
        _assembly = assembly;
        _pe = pe;
        _frames = new Stack<Frame>();

        var meta = pe.GetMetadata();
        _mr = new MetadataReader(meta.Pointer, meta.Length);
    }

    /// <summary>
    /// Execute the specified CIL.
    /// </summary>
    /// <param name="cil"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    private void _Run(byte[] cil)
    {
        var frame = _frames.Peek();

        var i = 0;
        while (i < cil.Length)
        {
            Console.Write($"{i}: ");
            var opcode = cil[i];
            switch (opcode)
            {
                case 0x00: // NOP
                    Console.WriteLine("nop");
                    i++;
                    break;

                case 0x01: // break
                    Console.WriteLine("break");
                    i++;
                    break;

                case 0x06: // ldloc.x
                case 0x07:
                case 0x08:
                case 0x09:
                {
                    Console.WriteLine("ldloc.x");
                    frame.m_stack.Push(frame.m_locals[opcode - 0x06]);
                    i++;
                    break;
                }

                case 0x0A: // stloc.x
                case 0x0B:
                case 0x0C:
                case 0x0D:
                {
                    var pos = opcode - 0x0A;
                    Console.WriteLine($"stloc.{pos}");
                    frame.m_locals[opcode - 0x0A] = frame.m_stack.Pop();
                    i++;
                    break;
                }

                case 0x11: // ldloc.s
                {
                    Console.WriteLine("ldloc.s");
                    frame.m_stack.Push(frame.m_locals[cil[i + 1]]);

                    i += 2;
                    break;
                }

                case 0x13: // stloc.s
                {
                    Console.WriteLine("stloc.s");
                    frame.m_locals[cil[i + 1]] = frame.m_stack.Pop();

                    i += 2;
                    break;
                }

                case 0x14: // ldnull
                {
                    frame.m_stack.Push(null);
                    i++;
                    break;
                }

                case 0x15: // ldc.i4.m1
                case 0x16: // ldc.i4.0
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A:
                case 0x1B:
                case 0x1C:
                case 0x1D:
                case 0x1E: // ldc.i4.8
                {
                    var val = opcode - 0x16;
                    Console.WriteLine($"ldc.i4.{val}");
                    frame.m_stack.Push(val);
                    i++;
                    break;
                }

                case 0x1F: // ldc.i4.s
                {
                    Console.WriteLine("ldc.i4.s");
                    frame.m_stack.Push((int) cil[i + 1]);
                    i += 2;
                    break;
                }

                case 0x23: // ldc.r8
                {
                    Console.WriteLine("ldc.r8");
                    var float64 = BitConverter.ToDouble(cil.AsSpan()[(i + 1)..(i + 9)]);
                    frame.m_stack.Push(float64);
                    i += 9;
                    break;
                }

                case 0x25: // dup
                    Console.WriteLine("dup");
                    frame.m_stack.Push(frame.m_stack.Peek());
                    i++;
                    break;

                case 0x26: // pop
                    Console.WriteLine("pop");
                    frame.m_stack.Pop();
                    i++;
                    break;

                case 0x2A: // ret
                    Console.WriteLine("ret");
                    i++;
                    break;

                case 0x2B: // br.s
                {
                    Console.WriteLine("br.s");
                    i = i + 2 + (sbyte) cil[i + 1];
                    break;
                }

                case 0x2C: // brfalse.s
                {
                    Console.WriteLine("brfalse.s");
                    if ((int) frame.m_stack.Pop()! == 0)
                    {
                        i = i + 2 + (sbyte) cil[i + 1];
                    }
                    else
                    {
                        i += 2;
                    }

                    break;
                }

                case 0x2D: // brtrue.s
                {
                    Console.WriteLine("brtrue.s");
                    if ((int) frame.m_stack.Pop()! == 1)
                    {
                        i = i + 2 + (sbyte) cil[i + 1];
                    }
                    else
                    {
                        i += 2;
                    }

                    break;
                }

                case 0x58: // add
                case 0x59: // sub
                case 0x5A: // mul
                case 0x5B: // div
                {
                    Console.WriteLine("math");
                    if (frame.m_stack.Pop() is int i1 && frame.m_stack.Pop() is int i2)
                    {
                        var result = opcode switch
                        {
                            0x58 => i1 + i2,
                            0x59 => i2 - i1,
                            0x5A => i1 * i2,
                            0x5B => i2 / i1,
                            _ => throw new ArgumentException(nameof(opcode))
                        };

                        frame.m_stack.Push(result);
                    }
                    else
                    {
                        throw new NotImplementedException("`math` not implemented for type");
                    }

                    i++;
                    break;
                }

                case 0x5F: // and
                case 0x60: // or
                case 0x61: // xor
                {
                    Console.WriteLine("bitwise");
                    if (frame.m_stack.Pop() is int i1 && frame.m_stack.Pop() is int i2)
                    {
                        var result = opcode switch
                        {
                            0x5F => i1 & i2,
                            0x60 => i1 | i2,
                            0x61 => i1 ^ i2,
                            _ => throw new ArgumentException(nameof(opcode))
                        };

                        frame.m_stack.Push(result);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid operand types to `bitwise`");
                    }

                    i++;
                    break;
                }

                case 0x62: // shl
                case 0x63: // shr
                {
                    var amt = (int) frame.m_stack.Pop()!;
                    var val = (int) frame.m_stack.Pop()!;
                    var eval = opcode switch
                    {
                        0x62 => val << amt,
                        0x63 => val >> amt,
                        _ => throw new ArgumentException(nameof(opcode))
                    };

                    frame.m_stack.Push(eval);
                    i++;
                    break;
                }

                case 0x67: // conv.i1
                case 0x68: // conv.i2
                case 0x69: // conv.i4
                case 0x6A: // conv.i8
                {
                    Console.WriteLine("conv.ix");
                    var val = frame.m_stack.Pop();
                    var cast = opcode switch
                    {
                        0x67 => (sbyte) val!,
                        0x68 => (short) val!,
                        0x69 => (int) val!,
                        0x6A => (long) val!,
                        _ => throw new ArgumentException(nameof(opcode))
                    };

                    frame.m_stack.Push(cast);
                    i++;
                    break;
                }

                case 0x72: // ldstr
                {
                    Console.WriteLine("ldstr");
                    var offset = BitConverter.ToInt16(cil.AsSpan()[(i + 1)..(i + 5)]);
                    frame.m_stack.Push(_mr.GetUserString(MetadataTokens.UserStringHandle(offset)));

                    i += 5;
                    break;
                }

                case 0x8D: // newarr; TODO: Handle type
                {
                    Console.WriteLine("newarr");
                    if (frame.m_stack.Pop() is not int len)
                    {
                        throw new ArgumentException("Expecting `arr` length");
                    }

                    var arr = new object[len];
                    frame.m_stack.Push(arr);
                    i += 5;
                    break;
                }

                case 0x8E: // ldlen
                {
                    Console.WriteLine("ldlen");
                    if (frame.m_stack.Pop() is object[] arr)
                    {
                        frame.m_stack.Push(arr.Length);
                    }
                    else
                    {
                        throw new ArgumentException("Expected an array");
                    }

                    i++;
                    break;
                }

                case 0x94: // ldelem.i4
                {
                    Console.WriteLine("ldelem.i4");
                    var (idx, arr) = ((int) frame.m_stack.Pop()!, (object[]) frame.m_stack.Pop()!);
                    frame.m_stack.Push((int) arr[idx]);
                    i++;
                    break;
                }

                case 0x9A: // ldelem.ref
                {
                    Console.WriteLine("ldelem.ref");
                    var (idx, arr) = ((int) frame.m_stack.Pop()!, (object[]) frame.m_stack.Pop()!);
                    frame.m_stack.Push(arr[idx]);

                    i++;
                    break;
                }

                case 0x9B: // stelem.x
                case 0x9C:
                case 0x9D:
                case 0x9E:
                case 0x9F:
                case 0xA0:
                case 0xA1:
                {
                    Console.WriteLine("stelem.x");
                    var val = frame.m_stack.Pop();
                    if (frame.m_stack.Pop() is int pos && frame.m_stack.Pop() is object[] arr)
                    {
                        arr[pos] = (opcode switch
                        {
                            0x9B => val,
                            0x9C => (sbyte) val!,
                            0x9D => (short) val!,
                            0x9E => (int) val!,
                            0x9F => (long) val!,
                            0xA0 => (float) val!,
                            0xA1 => (double) val!,
                            _ => throw new ArgumentException(nameof(opcode))
                        })!;
                    }
                    else
                    {
                        throw new ArgumentException("Could not store `val` at `pos`");
                    }

                    i++;
                    break;
                }

                case 0xA2: // stelem.ref
                {
                    Console.WriteLine("stelem.ref");
                    var (a, b, c) = (frame.m_stack.Pop(), frame.m_stack.Pop(), frame.m_stack.Pop());
                    i++;
                    break;
                }

                case 0xFE when cil[i + 1] == 0x01: // ceq
                {
                    Console.WriteLine("ceq");
                    var v1 = frame.m_stack.Pop();
                    var v2 = frame.m_stack.Pop();

                    if (v1 is int i1 && v2 is int i2)
                    {
                        frame.m_stack.Push(i1 == i2 ? 1 : 0);
                    }
                    else
                    {
                        Console.WriteLine("ceq fallback");
                        frame.m_stack.Push(v1 == v2 ? 1 : 0);
                    }

                    i += 2;
                    break;
                }

                case 0xFE when cil[i + 1] is 0x02 or 0x04: // cgt or clt
                {
                    Console.WriteLine("c[g/l]t");
                    var v1 = frame.m_stack.Pop();
                    var v2 = frame.m_stack.Pop();

                    if (v1 is int i1 && v2 is int i2)
                    {
                        var b = cil[i + 1] switch
                        {
                            0x02 => i2 > i1,
                            0x04 => i1 > i2,
                            _ => false
                        };

                        frame.m_stack.Push(b ? 1 : 0);
                    }
                    else
                    {
                        throw new NotImplementedException("cgt not implemented for non integers");
                    }

                    i += 2;
                    break;
                }

                default:
                    Console.WriteLine($"Cannot decode 0x{BitConverter.ToString(new[] {opcode})}");
                    return;
            }
        }
    }

    /// <summary>
    /// Begins execution of CIL by looking up the `Main()` method.
    /// </summary>
    /// <param name="ns">Namespace that contains the class which contains `Main`.</param>
    /// <param name="c">Class containing `Main`.</param>
    /// <exception cref="ArgumentException"></exception>
    public void Run(string ns, string c)
    {
        var program = _assembly.GetType($"{ns}.{c}");
        var methodBody = program
            ?.GetMethod("Main")
            ?.GetMethodBody();

        var cil = methodBody?.GetILAsByteArray();

        if (methodBody == null || cil == null)
        {
            throw new ArgumentException($"Require that {nameof(methodBody)} and {nameof(cil)} be non-null");
        }

        _frames.Push(new Frame(methodBody.LocalVariables.Count, methodBody.MaxStackSize));
        _Run(cil);
        _frames.Pop();
    }

    /// <summary>
    /// Dispose of all the streams and release resources.
    /// </summary>
    public void Dispose()
    {
        _pe.Dispose();
    }
}
