using System.Linq;

namespace Lungo;

/// <summary>
/// Each method has its own frame that contains locals array and
/// operand stack.
/// </summary>
class Frame
{
    /// <summary>
    /// Holds local variables.
    /// </summary>
    public object?[] m_locals { get; }
    
    /// <summary>
    /// Holds operands required for opcodes.
    /// </summary>
    public Stack<object?> m_stack { get; }

    /// <summary>
    /// Instantiate a new frame.
    /// </summary>
    /// <param name="maxLocals">Max locals declared in the CIL.</param>
    /// <param name="maxStack">Max size to which the stack can extend.</param>
    public Frame(int maxLocals, int maxStack)
    {
        m_locals = new object?[maxLocals];
        m_stack = new Stack<object?>(maxStack);
    }
}
