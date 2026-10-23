# @jagpav/pipeline

A small, dependency-free compensable pipeline for TypeScript — the TypeScript port of [JagPav.Pipeline](https://github.com/pavlek1817/jag-pipeline/tree/main/csharp). Instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

## The idea

Every step shares **one contract**:

```ts
type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<TModel>
```

Because every step takes the same shared model and the same `AbortSignal`, and resolves with that model, a pipeline definition becomes a flat, readable list of step references:

```ts
import { Pipeline } from '@jagpav/pipeline'

const pipeline = new Pipeline<StudentRegistrationModel>()
  .addStep(studentService.validateRequest)
  .addStep(studentService.ensureIdentificationNumberIsUnique)
  .addStep(programCatalogService.ensureProgramExists)
  .addCompensableStep(studentService.addPending, studentService.removePending)
  .addCompensableStep(paymentService.charge, paymentService.revertCharge)
  .addStep(studentService.completeRegistration)
  .build()

const result = await pipeline.execute(new StudentRegistrationModel(request), signal)
```

> Pass steps as arrow-function properties or bound functions: a plain method reference loses `this`.

### Steps that only need part of the model

A step must resolve with the model it was given, so a step that should only see a slice of the model is written as a generic function constrained to that type:

```ts
type ChargeModel = { student: Student; chargeId?: string }

charge = async <T extends ChargeModel>(model: T): Promise<T> => {
  model.chargeId = await this.gateway.charge(model.student)
  return model
}
```

It is still registered as `.addCompensableStep(paymentService.charge, paymentService.revertCharge)`: TypeScript infers `T` from the pipeline. Inside the step only `ChargeModel` fields are available, and a pipeline whose model lacks them does not compile. A non-generic `(model: ChargeModel) => Promise<ChargeModel>` is rejected, because it would resolve with the narrower type rather than the pipeline's model.

## API surface

- **`addStep(step)`** — registers a step with no compensation.
- **`addCompensableStep(step, compensate?)`** — registers a step alongside the action that undoes it if a *later* step fails.
- **`onFailure(handler)`** — registers one final action that runs when the pipeline fails, after the compensations and before the error reaches the caller. It receives the model, the error and the signal, e.g. to queue a retry or store a pending action. Optional; can be called once, before `build()`.
- **`build()`** — finalizes the pipeline. Must be called once, before `execute`; a second call throws. No steps or `onFailure` handler can be added afterwards.
- **`execute(model, signal?)`** — runs the model through every step in order and resolves with the same model. The signal is checked before each step. On failure, already-completed compensable steps are compensated in reverse order, the `onFailure` handler runs, then the original error is re-thrown.
- **`PipelineCompensationError`** — thrown instead of the original error when one or more compensations also failed. Exposes `error` (the original) and `compensationErrors`. Every compensation runs even if an earlier one fails.
- **`PipelineFailureHandlerError`** — thrown when the `onFailure` handler itself fails. Exposes `error` (what the pipeline would otherwise have thrown) and `failureHandlerError`.

### `onFailure` is not a compensation

Do not use `onFailure` to undo the work of individual steps; pair each such step with its own compensating action through `addCompensableStep`. A compensation belongs to its step and runs only if that step completed, in reverse order with the others. `onFailure` runs once for the whole pipeline, whichever step failed, without knowing which steps completed, so rollback logic placed there has to guess what to undo.

Use `onFailure` for what should happen after the rollback is done: logging or alerting, queueing a retry, or storing a pending action. It runs after every compensation, and its own failure does not affect them.

## Development

```sh
npm install
npm test
npm run build
```
