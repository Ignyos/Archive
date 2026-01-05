namespace Archive.Core;

/// <summary>
/// Exception thrown when a synchronization operation is stopped due to schedule boundary constraints.
/// </summary>
public class ScheduleBoundaryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleBoundaryException"/> class.
    /// </summary>
    public ScheduleBoundaryException()
        : base("Operation stopped due to schedule boundary.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleBoundaryException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ScheduleBoundaryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleBoundaryException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ScheduleBoundaryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
