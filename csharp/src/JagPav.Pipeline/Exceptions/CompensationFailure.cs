namespace JagPav.Pipeline.Exceptions;

/// <summary>
/// A compensating action that failed, and the step it belongs to.
/// </summary>
public sealed class CompensationFailure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationFailure"/> class.
    /// </summary>
    /// <param name="stepIndex">The 0-based index of the step whose compensation failed.</param>
    /// <param name="exception">The exception the compensation threw.</param>
    public CompensationFailure(int stepIndex, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        this.StepIndex = stepIndex;
        this.Exception = exception;
    }

    /// <summary>
    /// Gets the 0-based index of the step whose compensation failed, in the order the steps were added.
    /// </summary>
    public int StepIndex { get; }

    /// <summary>
    /// Gets the exception the compensation threw.
    /// </summary>
    public Exception Exception { get; }
}
