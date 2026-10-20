namespace Jag.Pipeline.Core;

/// <summary>
/// A single registered pipeline step paired with its optional compensating action.
/// </summary>
/// <typeparam name="TModel">The shared model passed through the pipeline.</typeparam>
internal sealed record StepDefinition<TModel>(
    Func<TModel, CancellationToken, Task> executeAsync,
    Func<TModel, CancellationToken, Task>? compensateAsync)
    where TModel : class;
