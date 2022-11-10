using Microsoft.CodeAnalysis;

namespace Lungo;

/// <summary>
/// Lungo; builds the specified resource and executes the CIL.
/// </summary>
class Program
{
    /// <summary>
    /// Should be invoked as:
    /// Lungo [csproj] [namespace] [class]
    /// </summary>
    /// <param name="args"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("Expecting args: <.csproj> <namespace> <class>");    
        }

        var (csproj, ns, c) = (args[0], args[1], args[2]);
        var compiler = new Project(csproj, OptimizationLevel.Debug);
        var assembly = compiler.m_Assembly;
        var pe = compiler.m_PeReader;

        if (assembly == null)
        {
            throw new ArgumentException(nameof(assembly));
        }

        if (pe == null)
        {
            throw new ArgumentException(nameof(pe));
        }

        using var vm = new VM(pe, assembly);
        vm.Run(ns, c);
    }
}
