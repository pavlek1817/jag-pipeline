# @jag-pipeline/core

A small, dependency-free compensable pipeline for TypeScript — the TypeScript port of [Jag.Pipeline](https://github.com/pavlek1817/jag-pipeline/tree/main/csharp). Instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

## The idea

Every step shares **one contract**:

```ts
type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<unknown>
```

Because every step takes the same shared model and the same `AbortSignal`, a pipeline definition becomes a flat, readable list of step references:

```ts
import { Pipeline } from '@jag-pipeline/core'

const pipeline = new Pipeline<StudentRegistrationModel>()
  .addStep(studentService.validateRequest)
  .addStep(studentService.ensureIdentificationNumberIsUnique)
  .addStep(programCatalogService.ensureProgramExists)
  .addCompensableStep(studentService.addPending, studentService.removePending)
  .addCompensableStep(paymentService.charge, paymentService.revertCharge)
  .addStep(studentService.completeRegistration)

const result = await pipeline.execute(new StudentRegistrationModel(request), signal)
```

> Pass steps as arrow-function properties or bound functions: a plain method reference loses `this`.

## API surface

- **`addStep(execute)`** — registers a step with no compensation.
- **`addCompensableStep(execute, compensate?)`** — registers a step alongside the action that undoes it if a *later* step fails.
- **`execute(model, signal?)`** — runs the model through every step in order and resolves with the same model. The signal is checked before each step. On failure, already-completed compensable steps are compensated in reverse order, then the original error is re-thrown.
- **`PipelineCompensationError`** — thrown instead of the original error when one or more compensations also failed. Exposes `error` (the original) and `compensationErrors`. Every compensation runs even if an earlier one fails.

## Development

```sh
npm install
npm test
npm run build
```
