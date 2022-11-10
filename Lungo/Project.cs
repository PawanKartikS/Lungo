using System.Reflection;
using System.Reflection.PortableExecutable;

using Microsoft.Build.Locator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace Lungo;

class Project
{
    /// <summary>
    /// The assembly file that is built.
    /// </summary>
    public Assembly? m_Assembly { get; }
    
    /// <summary>
    /// PEReader for reading the assembly file.
    /// </summary>
    public PEReader? m_PeReader { get; }

    /// <summary>
    /// Instantiate a new instance of the compiler and build the specified
    /// csproj.
    /// </summary>
    /// <param name="proj"></param>
    /// <param name="optimizationLevel"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="CompileException"></exception>
    public Project(string proj, OptimizationLevel optimizationLevel)
    {
        Compilation? compilation = null;

        MSBuildLocator.RegisterDefaults();
        using var workspace = MSBuildWorkspace.Create();

        if (proj.EndsWith(".csproj"))
        {
            var project = workspace.OpenProjectAsync(proj).Result;
            compilation = project.GetCompilationAsync().Result;
        }
        else if (proj.EndsWith(".cs"))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(proj));
            compilation = CSharpCompilation.Create("source")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object)
                    .Assembly
                    .Location))
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: optimizationLevel))
                .AddSyntaxTrees(tree);
        }
        else if (proj.EndsWith(".dll"))
        {
            m_Assembly = Assembly.LoadFile(proj);
            return;
        }

        if (compilation == null)
        {
            throw new ArgumentException(nameof(compilation));
        }

        var @out = Path.Join(Path.GetTempPath(), "lungo_out.dll");
        var result = compilation.Emit(@out);

        if (result.Success)
        {
            var fstream = File.OpenRead(@out);
            m_PeReader = new PEReader(fstream);
            m_Assembly = Assembly.LoadFile(@out);
        }
        else
        {
            // TODO: Process `result.Diagnostics`?
            throw new CompileException("Could not compile and load assembly");
        }
    }
}
