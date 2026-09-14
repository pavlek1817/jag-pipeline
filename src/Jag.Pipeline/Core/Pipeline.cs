using Jag.Pipeline.Abstractions;
using System.Threading.Tasks.Dataflow;

namespace Jag.Pipeline.Core;

/// <summary>
/// Dataflow-based compensable pipeline. <see cref="CreatePipeline"/> builds and links the block network once.
/// Each <see cref="ExecuteAsync"/> call posts a context through the live network and awaits its result.
/// </summary>
/// <typeparam name="TModel">Specified data model.</typeparam>
public sealed class Pipeline<TModel> : IPipeline<TModel>
    where TModel : class
{
    private readonly List<StepDefinition<TModel>> steps = new List<StepDefinition<TModel>>();
    private ITargetBlock<PipelineContext<TModel>>? headBlock;

    public IPipeline<TModel> AddCompensableStep(
        Func<TModel, CancellationToken, Task> executeAsync,
        Func<TModel, CancellationToken, Task>? compensateAsync)
    {
        if (this.headBlock is not null)
        {
            throw new InvalidOperationException("Steps cannot be added after CreatePipeline() has been called.");
        }

        this.steps.Add(new StepDefinition<TModel>(executeAsync, compensateAsync));
        return this;
    }

    public IPipeline<TModel> AddStep(Func<TModel, CancellationToken, Task> executeAsync)
    {
        return this.AddCompensableStep(executeAsync, null);
    }

    public IPipeline<TModel> CreatePipeline()
    {
        if (this.steps.Count == 0)
        {
            throw new InvalidOperationException("At least one step must be added before calling CreatePipeline().");
        }

        var blockOptions = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 };

        var blocks = this.steps
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
                    await compensateAllAsync(ctx.CompensateStack, ctx.Model, ctx.Ct);
                    ctx.Tcs.SetException(ctx.Error);
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
        return this;
    }

    public Task<TModel> ExecuteAsync(TModel model, CancellationToken ct = default)
    {
        if (this.headBlock is null)
        {
            throw new InvalidOperationException("Call CreatePipeline() before ExecuteAsync().");
        }

        var ctx = new PipelineContext<TModel>(model, ct);
        this.headBlock.Post(ctx);
        return ctx.Tcs.Task;
    }

    private static async Task compensateAllAsync(
        Stack<Func<TModel, CancellationToken, Task>> compensateStack,
        TModel model,
        CancellationToken ct)
    {
        while (compensateStack.Count > 0)
        {
            await compensateStack.Pop()(model, ct);
        }
    }
}
