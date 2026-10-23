# JagPav.Pipeline

A small, dependency-free compensable pipeline for .NET. It exists to **simplify orchestration logic**: instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

## The idea

Every step a pipeline runs shares **one contract**:

```csharp
Func<TModel, CancellationToken, Task<TModel>>
```

Because every step — validation, a DB call, a payment charge, whatever — takes the same shared model and the same `CancellationToken` and hands back the same model, a pipeline definition stops being a bespoke block of glue code and becomes a flat, readable list of method references. No per-step lambdas, no manual threading of individual fields between steps, no bespoke rollback code.

For example, an eight-step student registration pipeline (see the full walkthrough in [`sample/JagPav.Pipeline.Sample.Api`](sample/JagPav.Pipeline.Sample.Api)) reads as:

```csharp
var pipeline = new Pipeline<StudentRegistrationContext>()
    .AddStep(studentService.ValidateRequestAsync)
    .AddStep(studentService.EnsureIdentificationNumberIsUniqueAsync)
    .AddStep(programCatalogService.EnsureProgramExistsAsync)
    .AddCompensableStep(studentService.AddPendingAsync, studentService.RemovePendingAsync)
    .AddCompensableStep(paymentService.ChargeAsync, paymentService.RevertChargeAsync)
    .AddStep(studentService.CompleteRegistrationAsync)
    .AddStep(studentService.SaveChangesAsync)
    .AddStep(studentService.ShapeResponseAsync)
    .Build();

var context = new StudentRegistrationContext(request);
var result = await pipeline.ExecuteAsync(context, ct);
return result.Response!;
```

Every method referenced above — across three unrelated services — has the exact same shape: `Task<StudentRegistrationContext> XAsync(StudentRegistrationContext model, CancellationToken ct)`. That's what keeps the definition above to nine lines instead of a wall of inline lambdas wiring narrower, mismatched method signatures together.

### Steps that only need part of the model

A step must resolve with the model it was given, so a step that should only see a slice of the model through an interface is written as a generic method constrained to that interface:

```csharp
public Task<TModel> ChargeAsync<TModel>(TModel model, CancellationToken ct)
    where TModel : IChargeModel
{
    model.ChargeId = gateway.Charge(model.Student!);
    return Task.FromResult(model);
}
```

It is still registered as `.AddCompensableStep(paymentService.ChargeAsync, paymentService.RevertChargeAsync)`: the compiler infers `TModel` from the pipeline. Inside the step only `IChargeModel` members are available, and a pipeline whose model does not implement `IChargeModel` does not compile. A non-generic `Task<IChargeModel> ChargeAsync(IChargeModel model, ...)` is rejected, because it would resolve with the interface rather than the pipeline's model.

## API surface

- **`AddStep(executeAsync)`** — registers a step with no compensation.
- **`AddCompensableStep(executeAsync, compensateAsync)`** — registers a step alongside the action that undoes it if a *later* step fails.
- **`OnFailure(onFailureAsync)`** — registers one final action that runs when the pipeline fails, after the compensations and before the exception reaches the caller. It receives the model, the exception and the token, e.g. to queue a retry or store a pending action. Can be called once, before `Build()`.
- **`Build()`** — finalizes the step list. Must be called once, before `ExecuteAsync`; a second call throws `InvalidOperationException`.
- **`ExecuteAsync(model, ct)`** — runs the model through every step in order. On failure, already-completed compensable steps are compensated in reverse order, the `OnFailure` action runs, then the original exception is re-thrown. If a compensation or the `OnFailure` action throws too, the original exception is still the one re-thrown; the others are kept in the returned task's `Exception.InnerExceptions`.

### `OnFailure` is not a compensation

Do not use `OnFailure` to undo the work of individual steps; pair each such step with its own compensating action through `AddCompensableStep`. A compensation belongs to its step and runs only if that step completed, in reverse order with the others. `OnFailure` runs once for the whole pipeline, whichever step failed, without knowing which steps completed, so rollback logic placed there has to guess what to undo.

Use `OnFailure` for what should happen after the rollback is done: logging or alerting, queueing a retry, or storing a pending action. It runs after every compensation, and its own failure does not affect them.

## Getting started

1. Define a single model type that every step reads from and writes to (e.g. `StudentRegistrationContext`).
2. Write each step as `Task<TModel> XAsync(TModel model, CancellationToken ct)`, mutating and returning the model.
3. Chain them with `AddStep` / `AddCompensableStep`, call `Build()`, then `ExecuteAsync(model, ct)`.

See [`sample/JagPav.Pipeline.Sample.Api`](sample/JagPav.Pipeline.Sample.Api) for a complete, runnable example, and [`test/JagPav.Pipeline.UnitTests`](test/JagPav.Pipeline.UnitTests) for the pipeline's own behavior (ordering, compensation, cancellation).
