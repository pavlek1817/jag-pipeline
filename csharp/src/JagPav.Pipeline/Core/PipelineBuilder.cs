using JagPav.Pipeline.Abstractions;
using JagPav.Pipeline.Exceptions;

namespace JagPav.Pipeline.Core;

/// <summary>
/// Collects the steps and the failure action of a compensable pipeline. <see cref="Build"/> creates the pipeline once.
/// </summary>
/// <typeparam name="TModel">Specified data model.</typeparam>
public sealed class PipelineBuilder<TModel> : IPipelineBuilder<TModel>
    where TModel : class
{
    private readonly List<StepDefinition<TModel>> steps = new List<StepDefinition<TModel>>();
    private readonly CompensationStrategy compensationStrategy;
    private Func<TModel, PipelineException, CancellationToken, Task>? onFailureAsync;
    private bool isBuilt;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBuilder{TModel}"/> class.
    /// </summary>
    /// <param name="compensationStrategy">What the pipeline does when a compensating action fails.</param>
    public PipelineBuilder(CompensationStrategy compensationStrategy = CompensationStrategy.ContinueOnFailure)
    {
        if (!Enum.IsDefined(compensationStrategy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(compensationStrategy),
                compensationStrategy,
                "Unknown compensation strategy.");
        }

        this.compensationStrategy = compensationStrategy;
    }

    public IPipelineBuilder<TModel> AddCompensableStep(
        Func<TModel, CancellationToken, Task<TModel>> executeAsync,
        Func<TModel, CancellationToken, Task<TModel>>? compensateAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        this.ensureNotBuilt();
        this.steps.Add(new StepDefinition<TModel>(this.steps.Count, executeAsync, compensateAsync));
        return this;
    }

    public IPipelineBuilder<TModel> AddStep(Func<TModel, CancellationToken, Task<TModel>> executeAsync)
    {
        return this.AddCompensableStep(executeAsync, null);
    }

    public IPipelineBuilder<TModel> OnFailure(Func<TModel, PipelineException, CancellationToken, Task> onFailureAsync)
    {
        ArgumentNullException.ThrowIfNull(onFailureAsync);
        this.ensureNotBuilt();
        if (this.onFailureAsync is not null)
        {
            throw new InvalidOperationException("OnFailure() can only be called once.");
        }

        this.onFailureAsync = onFailureAsync;
        return this;
    }

    public IPipeline<TModel> Build()
    {
        if (this.isBuilt)
        {
            throw new InvalidOperationException("Build() can only be called once.");
        }

        if (this.steps.Count == 0)
        {
            throw new InvalidOperationException("At least one step must be added before calling Build().");
        }

        var pipeline = new Pipeline<TModel>(this.steps.ToList(), this.onFailureAsync, this.compensationStrategy);
        this.isBuilt = true;
        return pipeline;
    }

    private void ensureNotBuilt()
    {
        if (this.isBuilt)
        {
            throw new InvalidOperationException("The pipeline cannot be changed after Build() has been called.");
        }
    }
}
