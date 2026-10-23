using JagPav.Pipeline.Abstractions;
using System.Threading.Tasks.Dataflow;

namespace JagPav.Pipeline.Core;

/// <summary>
/// Dataflow-based compensable pipeline, created by <see cref="PipelineBuilder{TModel}.Build"/>.
/// The constructor builds and links the block network once.
/// Each <see cref="ExecuteAsync"/> call posts a context through the live network and awaits its result.
/// </summary>
/// <typeparam name="TModel">Specified data model.</typeparam>
internal sealed class Pipeline<TModel> : IPipeline<TModel>
    where TModel : class
{
    private readonly ITargetBlock<PipelineContext<TModel>> headBlock;

    internal Pipeline(
        IReadOnlyList<StepDefinition<TModel>> steps,
        Func<TModel, Exception, CancellationToken, Task>? onFailureAsync)
    {
        var blockOptions = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 };

        var blocks = steps
            .Select(step => new TransformBlock<PipelineContext<TModel>, PipelineContext<TModel>>(
                async ctx =>
                {
                    if (ctx.Error is not null)
                    {
                        return ctx;
                    }

                    try
                    {
                        await step.executeAsync(ctx.Model, ctx.Ct);
                        if (step.compensateAsync is not null)
                        {
                            ctx.CompensateStack.Push(step.compensateAsync);
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Error = ex;
                    }

                    return ctx;
                }, blockOptions))
            .ToList();

        var finalBlock = new ActionBlock<PipelineContext<TModel>>(
            async ctx =>
            {
                if (ctx.Error is not null)
                {
                    // The original exception goes first so it is the one awaiting callers see.
                    var errors = new List<Exception> { ctx.Error };

                    try
                    {
                        await compensateAllAsync(ctx.CompensateStack, ctx.Model, ctx.Ct);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }

                    if (onFailureAsync is not null)
                    {
                        try
                        {
                            await onFailureAsync(ctx.Model, ctx.Error, ctx.Ct);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }

                    ctx.Tcs.SetException(errors);
                }
                else
                {
                    ctx.Tcs.SetResult(ctx.Model);
                }
            }, blockOptions);

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = false };
        for (var i = 0; i < blocks.Count - 1; i++)
        {
            blocks[i].LinkTo(blocks[i + 1], linkOptions);
        }

        blocks[^1].LinkTo(finalBlock, linkOptions);

        this.headBlock = blocks[0];
    }

    public Task<TModel> ExecuteAsync(TModel model, CancellationToken ct = default)
    {
        var ctx = new PipelineContext<TModel>(model, ct);
        this.headBlock.Post(ctx);
        return ctx.Tcs.Task;
    }

    private static async Task compensateAllAsync(
        Stack<Func<TModel, CancellationToken, Task<TModel>>> compensateStack,
        TModel model,
        CancellationToken ct)
    {
        while (compensateStack.Count > 0)
        {
            await compensateStack.Pop()(model, ct);
        }
    }
}
