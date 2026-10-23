namespace JagPav.Pipeline.Abstractions;

/// <summary>
/// A compensable pipeline that threads a model through a sequence of steps,
/// compensating completed steps in reverse order when any step fails.
/// Created by <see cref="IPipelineBuilder{TModel}.Build"/>.
/// </summary>
/// <typeparam name="TModel">The shared model passed through and returned by the pipeline.</typeparam>
public interface IPipeline<TModel>
    where TModel : class
{
    /// <summary>
    /// Runs the model through all registered steps in order.
    /// On failure, compensates all completed steps in reverse order, runs the failure action registered with
    /// <see cref="IPipelineBuilder{TModel}.OnFailure"/>, and re-throws the original exception. If a compensation or
    /// the failure action throws as well, the original exception is still the one re-thrown; the others are added to
    /// the returned task's <see cref="Task.Exception"/>.
    /// </summary>
    /// <param name="model">The model to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The processed model.</returns>
    Task<TModel> ExecuteAsync(TModel model, CancellationToken ct = default);
}
