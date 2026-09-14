# Jag.Pipeline

A small, dependency-free compensable pipeline for .NET. It exists to **simplify orchestration logic**: instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

## The idea

Every step a pipeline runs shares **one contract**:

```csharp
Func<TModel, CancellationToken, Task>
```

Because every step — validation, a DB call, a payment charge, whatever — takes the same shared model and the same `CancellationToken` and hands back the same model, a pipeline definition stops being a bespoke block of glue code and becomes a flat, readable list of method references. No per-step lambdas, no manual threading of individual fields between steps, no bespoke rollback code.

For example, an eight-step student registration pipeline (see the full walkthrough in [`sample/Jag.Pipeline.Sample.Api`](sample/Jag.Pipeline.Sample.Api)) reads as:

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
    .CreatePipeline();

var context = new StudentRegistrationContext(request);
var result = await pipeline.ExecuteAsync(context, ct);
return result.Response!;
```

Every method referenced above — across three unrelated services — has the exact same shape: `Task<StudentRegistrationContext> XAsync(StudentRegistrationContext model, CancellationToken ct)`. That's what keeps the definition above to nine lines instead of a wall of inline lambdas wiring narrower, mismatched method signatures together.

## API surface

- **`AddStep(executeAsync)`** — registers a step with no compensation.
- **`AddCompensableStep(executeAsync, compensateAsync)`** — registers a step alongside the action that undoes it if a *later* step fails.
- **`CreatePipeline()`** — finalizes the step list. Must be called once, before `ExecuteAsync`.
- **`ExecuteAsync(model, ct)`** — runs the model through every step in order. On failure, already-completed compensable steps are compensated in reverse order, then the original exception is re-thrown.

## Getting started

1. Define a single model type that every step reads from and writes to (e.g. `StudentRegistrationContext`).
2. Write each step as `Task<TModel> XAsync(TModel model, CancellationToken ct)`, mutating and returning the model.
3. Chain them with `AddStep` / `AddCompensableStep`, call `CreatePipeline()`, then `ExecuteAsync(model, ct)`.

See [`sample/Jag.Pipeline.Sample.Api`](sample/Jag.Pipeline.Sample.Api) for a complete, runnable example, and [`test/Jag.Pipeline.UnitTests`](test/Jag.Pipeline.UnitTests) for the pipeline's own behavior (ordering, compensation, cancellation).
