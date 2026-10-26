using JagPav.Pipeline.Abstractions;
using JagPav.Pipeline.Exceptions;
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
        Func<TModel, PipelineException, CancellationToken, Task>? onFailureAsync,
        CompensationStrategy compensationStrategy)
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
                            ctx.CompensateStack.Push((step.stepIndex, step.compensateAsync));
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Error = ex;
                        ctx.FailedStepIndex = step.stepIndex;
                    }

                    return ctx;
                }, blockOptions))
            .ToList();

        var finalBlock = new ActionBlock<PipelineContext<TModel>>(
            async ctx =>
            {
                if (ctx.Error is not null)
                {
                    var exception = await compensateAllAsync(ctx, compensationStrategy);

                    if (onFailureAsync is not null)
                    {
                        try
                        {
                            await onFailureAsync(ctx.Model, exception, ctx.Ct);
                        }
                        catch (Exception ex)
                        {
                            exception = exception.WithFailureHandlerException(ex);
                        }
                    }

                    ctx.Tcs.SetException(exception);
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

    private static async Task<PipelineException> compensateAllAsync(
        PipelineContext<TModel> ctx,
        CompensationStrategy compensationStrategy)
    {
        var compensationFailures = new List<CompensationFailure>();
        while (ctx.CompensateStack.Count > 0)
        {
            var (stepIndex, compensateAsync) = ctx.CompensateStack.Pop();
            try
            {
                await compensateAsync(ctx.Model, ctx.Ct);
            }
            catch (Exception ex)
            {
                compensationFailures.Add(new CompensationFailure(stepIndex, ex));
                if (compensationStrategy == CompensationStrategy.StopOnFirstFailure)
                {
                    break;
                }
            }
        }

        // Whatever is left on the stack was skipped by StopOnFirstFailure.
        var notCompensatedStepIndexes = ctx.CompensateStack.Select(compensation => compensation.StepIndex).ToList();

        return new PipelineException(ctx.Error!, ctx.FailedStepIndex, compensationFailures, notCompensatedStepIndexes);
    }
}
