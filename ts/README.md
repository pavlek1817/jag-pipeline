# @jagpav/pipeline

A small, dependency-free compensable pipeline for TypeScript — the TypeScript port of [JagPav.Pipeline](https://github.com/pavlek1817/jag-pipeline/tree/main/csharp). Instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

## The idea

Every step shares **one contract**:

```ts
type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<TModel>
```

Because every step takes the same shared model and the same `AbortSignal`, and resolves with that model, a pipeline definition becomes a flat, readable list of step references:

```ts
import { PipelineBuilder } from '@jagpav/pipeline'

const pipeline = new PipelineBuilder<StudentRegistrationModel>()
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

A pipeline is configured with **`PipelineBuilder<TModel>`**:

- **`new PipelineBuilder<TModel>(compensationStrategy?)`** — creates a builder. The strategy defaults to `CompensationStrategy.ContinueOnFailure`; see [Compensation strategy](#compensation-strategy).
- **`addStep(step)`** — registers a step with no compensation.
- **`addCompensableStep(step, compensate?)`** — registers a step alongside the action that undoes it if a *later* step fails.
- **`onFailure(handler)`** — registers one final action that runs when the pipeline fails, after the compensations and before the error reaches the caller. It receives the model, the same `PipelineError` the caller gets and the signal, e.g. to queue a retry or store a pending action. Optional; can be called once, before `build()`.
- **`build()`** — creates the pipeline from the registered steps and returns it as a `Pipeline<TModel>`. Can be called once; a second call throws, and no steps or `onFailure` handler can be added afterwards.

The built **`Pipeline<TModel>`** only runs; it is an interface, so a pipeline can only be created through `build()`:

- **`execute(model, signal?)`** — runs the model through every step in order and resolves with the same model. The signal is checked before each step. On failure, already-completed compensable steps are compensated in reverse order, the `onFailure` handler runs, then a `PipelineError` is thrown; see [Errors](#errors).

### Compensation strategy

The strategy passed to the builder decides what happens when a compensating action itself fails:

- **`CompensationStrategy.ContinueOnFailure`** (default) — the remaining compensations still run, so as much as possible is undone. Every failure is reported.
- **`CompensationStrategy.StopOnFirstFailure`** — the rollback stops at the first failing compensation. The steps whose compensation did not run are reported in `notCompensatedStepIndexes`.

```ts
import { CompensationStrategy, PipelineBuilder } from '@jagpav/pipeline'

const pipeline = new PipelineBuilder<StudentRegistrationModel>(CompensationStrategy.StopOnFirstFailure)
  .addCompensableStep(studentService.addPending, studentService.removePending)
  .addCompensableStep(paymentService.charge, paymentService.revertCharge)
  .build()
```

### Errors

When a step fails, `execute` always rejects with a **`PipelineError`**. Its `cause` is what the step threw, so the caller can still tell what went wrong in the first place; the rest describes the rollback:

| Member | Meaning |
| --- | --- |
| `originalError` | What the step that failed threw; the same as `cause` |
| `failedStepIndex` | 0-based index of the step that failed, in the order the steps were added |
| `compensationFailures` | Each compensation that failed: `{ stepIndex, error }`, in the order they ran |
| `notCompensatedStepIndexes` | Steps whose compensation did not run (only with `StopOnFirstFailure`) |
| `failureHandlerError` | What the `onFailure` handler threw, or `undefined` if it did not fail |
| `isFullyCompensated` | `true` when no compensation failed or was skipped |
| `errors` | Every error in order: the original one, the failed compensations, then the `onFailure` handler's |

Map the original error to a response, and treat an incomplete rollback separately, since the system may then be left inconsistent:

```ts
try {
  return { status: 200, body: await pipeline.execute(model, signal) }
} catch (error) {
  if (!(error instanceof PipelineError)) throw error
  if (!error.isFullyCompensated) return { status: 500 }
  if (error.originalError instanceof DuplicateIdentificationNumberError) return { status: 409 }
  throw error
}
```

An aborted run is reported the same way, with the signal's abort reason as the `originalError`.

### `onFailure` is not a compensation

Do not use `onFailure` to undo the work of individual steps; pair each such step with its own compensating action through `addCompensableStep`. A compensation belongs to its step and runs only if that step completed, in reverse order with the others. `onFailure` runs once for the whole pipeline, whichever step failed, without knowing which steps completed, so rollback logic placed there has to guess what to undo.

Use `onFailure` for what should happen after the rollback is done: logging or alerting, queueing a retry, or storing a pending action for what could not be undone (`notCompensatedStepIndexes`, `compensationFailures`). It runs after every compensation, and its own failure does not affect them; it is reported as `failureHandlerError`.

## Development

```sh
npm install
npm test
npm run build
```
