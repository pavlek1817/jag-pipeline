namespace JagPav.Pipeline.Abstractions;

/// <summary>
/// A compensable pipeline that threads a model through a sequence of steps,
/// compensating completed steps in reverse order when any step fails.
/// </summary>
/// <typeparam name="TModel">The shared model passed through and returned by the pipeline.</typeparam>
public interface IPipeline<TModel>
    where TModel : class
{
    /// <summary>
    /// Registers a step paired with a compensating action that runs if a later step fails.
    /// </summary>
    /// <param name="executeAsync">The step to execute.</param>
    /// <param name="compensateAsync">The compensating action to run on failure. Pass <c>null</c> to skip compensation.</param>
    IPipeline<TModel> AddCompensableStep(
        Func<TModel, CancellationToken, Task> executeAsync,
        Func<TModel, CancellationToken, Task>? compensateAsync);

    /// <summary>
    /// Registers a step with no compensation. Calls <see cref="AddCompensableStep"/> with a null compensating action.
    /// </summary>
    /// <param name="executeAsync">The step to execute.</param>
    IPipeline<TModel> AddStep(Func<TModel, CancellationToken, Task> executeAsync);

    /// <summary>
    /// Registers a final action that runs once when the pipeline fails, after the compensations and before
    /// the original exception is re-thrown to the caller. Only one failure action can be registered.
    /// </summary>
    /// <param name="onFailureAsync">The action to run, given the model and the exception that failed the pipeline.</param>
    IPipeline<TModel> OnFailure(Func<TModel, Exception, CancellationToken, Task> onFailureAsync);

    /// <summary>
    /// Finalizes the pipeline configuration. Must be called before <see cref="ExecuteAsync"/>.
    /// No further steps may be added after this call.
    /// </summary>
    IPipeline<TModel> Create();

    /// <summary>
    /// Runs the model through all registered steps in order.
    /// On failure, compensates all completed steps in reverse order, runs the failure action registered with
    /// <see cref="OnFailure"/>, and re-throws the original exception. If a compensation or the failure action
    /// throws as well, the original exception is still the one re-thrown; the others are added to the returned task's
    /// <see cref="Task.Exception"/>.
    /// </summary>
    /// <param name="model">The model to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The processed model.</returns>
    Task<TModel> ExecuteAsync(TModel model, CancellationToken ct = default);
}
