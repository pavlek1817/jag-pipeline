namespace JagPav.Pipeline.Core;

/// <summary>
/// A single registered pipeline step paired with its optional compensating action.
/// </summary>
/// <typeparam name="TModel">The shared model passed through the pipeline.</typeparam>
internal sealed record StepDefinition<TModel>(
    Func<TModel, CancellationToken, Task<TModel>> executeAsync,
    Func<TModel, CancellationToken, Task<TModel>>? compensateAsync)
    where TModel : class;
