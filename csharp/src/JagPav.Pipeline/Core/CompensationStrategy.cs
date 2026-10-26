namespace JagPav.Pipeline.Core;

/// <summary>
/// What the pipeline does when a compensating action fails while it rolls back a failed run.
/// </summary>
public enum CompensationStrategy
{
    /// <summary>
    /// Runs every remaining compensation and reports all of their failures. The default.
    /// </summary>
    ContinueOnFailure,

    /// <summary>
    /// Stops at the first compensation that fails; the steps whose compensation did not run are reported as not compensated.
    /// </summary>
    StopOnFirstFailure,
}
