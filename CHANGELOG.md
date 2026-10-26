# Changelog

All notable changes to `JagPav.Pipeline` (NuGet) and `@jagpav/pipeline` (npm). Both packages share version numbers.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/).

## [2.0.0] - 2026-10-26

### Breaking changes

- **Pipelines are created through a builder.** The builder collects the steps, and `Build()` / `build()` returns the
  pipeline, which can only be executed.
  - .NET: `new Pipeline<TModel>()…CreatePipeline()` is now `new PipelineBuilder<TModel>()…Build()`. `AddStep` and
    `AddCompensableStep` moved to `IPipelineBuilder<TModel>`, `IPipeline<TModel>` only has `ExecuteAsync`, and
    `Pipeline<TModel>` is no longer public.
  - TypeScript: `new Pipeline<TModel>()` is now `new PipelineBuilder<TModel>()…build()`. `Pipeline<TModel>` is now
    an interface with only `execute`.
- **Steps and compensations return the model.**
  - .NET: `Func<TModel, CancellationToken, Task<TModel>>` (was `Task`).
  - TypeScript: `(model, signal) => Promise<TModel>` (was `Promise<unknown>`).
- **A failed run always throws the pipeline's own error**, with the step's error inside it.
  - .NET: `PipelineException` (namespace `JagPav.Pipeline.Exceptions`) instead of rethrowing the step's exception. The
    step's exception is `OriginalException`, also available as `InnerException`.
  - TypeScript: `PipelineError` instead of the step's error, or `PipelineCompensationError` when a compensation failed
    too. The step's error is `originalError`, also available as `cause`. `PipelineCompensationError` is removed.
- **TypeScript: only the public API is exported.** The entry point no longer re-exports everything from `pipeline.ts`.

### Added

- **`OnFailure` / `onFailure`**: an optional action that runs once when the pipeline fails, after the compensations.
  It receives the model and the same error the caller receives. If it throws, that error is reported on the pipeline
  error instead of replacing it.
- **`CompensationStrategy`**, passed to the builder's constructor:
  - `ContinueOnFailure` (default): runs every remaining compensation and reports all of their failures.
  - `StopOnFirstFailure`: stops at the first compensation that fails and reports the steps that were not compensated.
- **Details on the pipeline error**: the index of the failed step, each failed compensation with its step index, the
  steps that were not compensated, the failure action's error, whether every step was fully compensated, and a list of
  every error in the order it occurred.
  - .NET: `FailedStepIndex`, `CompensationFailures`, `NotCompensatedStepIndexes`, `FailureHandlerException`,
    `IsFullyCompensated`, `Exceptions`.
  - TypeScript: `failedStepIndex`, `compensationFailures`, `notCompensatedStepIndexes`, `failureHandlerError`,
    `isFullyCompensated`, `errors`.
- **Safer builder**: `Build()` / `build()` and `OnFailure` / `onFailure` can each be called only once, and the builder
  cannot be changed after it has built the pipeline.
- **Argument validation**: an unknown compensation strategy is rejected (`ArgumentOutOfRangeException` in .NET,
  `RangeError` in TypeScript). In .NET, a `null` step or failure action throws `ArgumentNullException`.

### Fixed

- **.NET: a compensation that threw made the pipeline hang.** In 1.0.0 the exception stopped the rollback, the call to
  `ExecuteAsync` never completed, and every later run on the same pipeline hung too. Compensation failures are now
  caught and reported on `PipelineException`.

### Upgrading from 1.x

.NET:

```csharp
// 1.x
var pipeline = new Pipeline<Order>()
    .AddCompensableStep(reserveAsync, releaseAsync)
    .AddStep(chargeAsync)
    .CreatePipeline();

// 2.0
var pipeline = new PipelineBuilder<Order>()
    .AddCompensableStep(reserveAsync, releaseAsync)
    .AddStep(chargeAsync)
    .Build();

try
{
    await pipeline.ExecuteAsync(order, ct);
}
catch (PipelineException ex) when (ex.OriginalException is PaymentDeclinedException)
{
    // Previously: catch (PaymentDeclinedException)
}
```

Make every step and compensation return the model, for example `return order;` at the end of each method.

TypeScript:

```ts
// 1.x
const pipeline = new Pipeline<Order>().addCompensableStep(reserve, release).addStep(charge)

// 2.0
const pipeline = new PipelineBuilder<Order>().addCompensableStep(reserve, release).addStep(charge).build()

try {
  await pipeline.execute(order)
} catch (error) {
  if (error instanceof PipelineError && error.originalError instanceof PaymentDeclinedError) {
    // Previously: error instanceof PaymentDeclinedError
  }
}
```

Make every step and compensation resolve with the model, for example `return order` at the end of each function.

## [1.0.0] - 2026-10-21

- First release: a compensable pipeline for .NET (built on TPL Dataflow) and TypeScript. Steps run in order with a
  shared model, and the compensations of completed steps run in reverse order when a step fails.

[2.0.0]: https://github.com/pavlek1817/jag-pipeline/releases/tag/v2.0.0
[1.0.0]: https://github.com/pavlek1817/jag-pipeline/tree/9cd6093
