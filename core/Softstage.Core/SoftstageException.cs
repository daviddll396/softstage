namespace Softstage.Core;

/// <summary>An error with a message that can be shown to the user.</summary>
public sealed class SoftstageException : Exception
{
    public SoftstageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
