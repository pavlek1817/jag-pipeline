namespace JagPav.Pipeline.Exceptions;

/// <summary>
/// Thrown when a pipeline step fails. <see cref="Exception.InnerException"/> (also <see cref="OriginalException"/>) is
/// the exception of the step that failed; the other members describe the rollback that followed.
/// </summary>
public sealed class PipelineException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineException"/> class.
    /// </summary>
    /// <param name="originalException">The exception of the step that failed.</param>
    /// <param name="failedStepIndex">The 0-based index of the step that failed.</param>
    /// <param name="compensationFailures">The compensations that failed, in the order they ran.</param>
    /// <param name="notCompensatedStepIndexes">The steps whose compensation did not run, in the order they would have run.</param>
    /// <param name="failureHandlerException">The exception of the <c>OnFailure</c> action, if it failed.</param>
    public PipelineException(
        Exception originalException,
        int failedStepIndex,
        IReadOnlyList<CompensationFailure>? compensationFailures = null,
        IReadOnlyList<int>? notCompensatedStepIndexes = null,
        Exception? failureHandlerException = null)
        : base(
            createMessage(
                originalException ?? throw new ArgumentNullException(nameof(originalException)),
                failedStepIndex,
                compensationFailures?.Count ?? 0,
                notCompensatedStepIndexes?.Count ?? 0,
                failureHandlerException),
            originalException)
    {
        this.FailedStepIndex = failedStepIndex;
        this.CompensationFailures = compensationFailures ?? Array.Empty<CompensationFailure>();
        this.NotCompensatedStepIndexes = notCompensatedStepIndexes ?? Array.Empty<int>();
        this.FailureHandlerException = failureHandlerException;

        var exceptions = new List<Exception> { originalException };
        exceptions.AddRange(this.CompensationFailures.Select(failure => failure.Exception));
        if (failureHandlerException is not null)
        {
            exceptions.Add(failureHandlerException);
        }

        this.Exceptions = exceptions;
    }

    /// <summary>
    /// Gets the exception of the step that failed. The same as <see cref="Exception.InnerException"/>.
    /// </summary>
    public Exception OriginalException => this.InnerException!;

    /// <summary>
    /// Gets the 0-based index of the step that failed, in the order the steps were added.
    /// </summary>
    public int FailedStepIndex { get; }

    /// <summary>
    /// Gets the compensations that failed, in the order they ran.
    /// </summary>
    public IReadOnlyList<CompensationFailure> CompensationFailures { get; }

    /// <summary>
    /// Gets the indexes of the steps whose compensation did not run because an earlier one failed under
    /// <see cref="Core.CompensationStrategy.StopOnFirstFailure"/>, in the order they would have run.
    /// </summary>
    public IReadOnlyList<int> NotCompensatedStepIndexes { get; }

    /// <summary>
    /// Gets the exception of the <c>OnFailure</c> action, or <c>null</c> if it did not fail.
    /// </summary>
    public Exception? FailureHandlerException { get; }

    /// <summary>
    /// Gets a value indicating whether every completed step was compensated: no compensation failed or was skipped.
    /// </summary>
    public bool IsFullyCompensated => this.CompensationFailures.Count == 0 && this.NotCompensatedStepIndexes.Count == 0;

    /// <summary>
    /// Gets every exception that occurred, in order: the original one, then the failed compensations, then the
    /// <c>OnFailure</c> action's.
    /// </summary>
    public IReadOnlyList<Exception> Exceptions { get; }

    internal PipelineException WithFailureHandlerException(Exception failureHandlerException)
        => new PipelineException(
            this.OriginalException,
            this.FailedStepIndex,
            this.CompensationFailures,
            this.NotCompensatedStepIndexes,
            failureHandlerException);

    private static string createMessage(
        Exception originalException,
        int failedStepIndex,
        int compensationFailureCount,
        int notCompensatedCount,
        Exception? failureHandlerException)
    {
        var message = $"Pipeline step {failedStepIndex} failed: {originalException.Message.TrimEnd('.')}.";
        if (compensationFailureCount > 0)
        {
            message += $" {compensationFailureCount} compensation(s) failed.";
        }

        if (notCompensatedCount > 0)
        {
            message += $" {notCompensatedCount} step(s) were not compensated.";
        }

        if (failureHandlerException is not null)
        {
            message += " The OnFailure action failed too.";
        }

        return message;
    }
}
