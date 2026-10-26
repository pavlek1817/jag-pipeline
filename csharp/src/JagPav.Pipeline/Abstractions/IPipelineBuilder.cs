using JagPav.Pipeline.Exceptions;

namespace JagPav.Pipeline.Abstractions;

/// <summary>
/// Configures a compensable pipeline: the steps it runs, their compensating actions and the action that runs
/// when it fails. <see cref="Build"/> turns the configuration into an <see cref="IPipeline{TModel}"/>.
/// </summary>
/// <typeparam name="TModel">The shared model passed through and returned by the pipeline.</typeparam>
public interface IPipelineBuilder<TModel>
    where TModel : class
{
    /// <summary>
    /// Registers a step paired with a compensating action that runs if a later step fails.
    /// </summary>
    /// <param name="executeAsync">The step to execute.</param>
    /// <param name="compensateAsync">The compensating action to run on failure. Pass <c>null</c> to skip compensation.</param>
    IPipelineBuilder<TModel> AddCompensableStep(
        Func<TModel, CancellationToken, Task<TModel>> executeAsync,
        Func<TModel, CancellationToken, Task<TModel>>? compensateAsync);

    /// <summary>
    /// Registers a step with no compensation. Calls <see cref="AddCompensableStep"/> with a null compensating action.
    /// </summary>
    /// <param name="executeAsync">The step to execute.</param>
    IPipelineBuilder<TModel> AddStep(Func<TModel, CancellationToken, Task<TModel>> executeAsync);

    /// <summary>
    /// Registers a final action that runs once when the pipeline fails, after the compensations and before
    /// the <see cref="PipelineException"/> is thrown to the caller. Only one failure action can be registered.
    /// It is not a replacement for a compensating action: undoing a step belongs in <see cref="AddCompensableStep"/>.
    /// </summary>
    /// <param name="onFailureAsync">
    /// The action to run, given the model and the same <see cref="PipelineException"/> the caller receives: the
    /// original exception and the outcome of the compensations.
    /// </param>
    IPipelineBuilder<TModel> OnFailure(Func<TModel, PipelineException, CancellationToken, Task> onFailureAsync);

    /// <summary>
    /// Builds the pipeline from the registered steps. Can be called only once; a second call throws
    /// <see cref="InvalidOperationException"/>, and the builder cannot be changed afterwards.
    /// </summary>
    /// <returns>The pipeline, ready to execute.</returns>
    IPipeline<TModel> Build();
}
