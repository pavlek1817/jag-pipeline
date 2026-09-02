namespace Jag.Pipeline.Core;

/// <summary>
/// Per-invocation state threaded through the pipeline's block network: the model being
/// processed, the stack of compensations accumulated so far, and the outcome handoff.
/// </summary>
/// <typeparam name="TModel">The shared model passed through the pipeline.</typeparam>
internal sealed class PipelineContext<TModel>(TModel model, CancellationToken ct)
    where TModel : class
{
    internal TModel Model { get; } = model;

    internal CancellationToken Ct { get; } = ct;

    internal Stack<Func<TModel, CancellationToken, Task>> CompensateStack { get; } = new ();

    internal Exception? Error { get; set; }

    internal TaskCompletionSource<TModel> Tcs { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
}
