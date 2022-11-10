namespace Lungo;

/// <summary>
/// Represents the exception that is thrown when compilation is
/// not successful.
/// </summary>
public class CompileException : Exception
{
    public CompileException()
    {
    }

    public CompileException(string message) : base(message)
    {
    }

    public CompileException(string message, Exception innerException) :
        base(message, innerException)
    {
    }
}
