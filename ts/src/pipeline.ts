/**
 * A step reads from and writes to the shared model. Declared as a function type (not a method signature)
 * so a step that needs fields the model does not have is a compile error.
 */
export type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<unknown>

/**
 * A step failed and so did at least one of the compensations run for it.
 */
export class PipelineCompensationError extends Error {
  constructor(public readonly error: unknown, public readonly compensationErrors: unknown[]) {
    super(`Pipeline step failed and ${compensationErrors.length} compensation(s) failed too`)
  }
}

type StepDefinition<TModel> = {
  execute: PipelineStep<TModel>
  compensate?: PipelineStep<TModel>
}

/**
 * Compensable pipeline, a TypeScript port of Jag.Pipeline (https://github.com/pavlek1817/jag-pipeline).
 * Runs the model through every step in order. If a step fails, the compensations of the steps that already
 * succeeded run in reverse order, then the original error is re-thrown.
 *
 * Pass steps as arrow-function properties or bound functions: a plain method reference loses `this`.
 */
export class Pipeline<TModel> {
  private readonly steps: StepDefinition<TModel>[] = []

  public addStep(execute: PipelineStep<TModel>) {
    return this.addCompensableStep(execute)
  }

  public addCompensableStep(execute: PipelineStep<TModel>, compensate?: PipelineStep<TModel>) {
    this.steps.push({ execute, compensate })
    return this
  }

  /**
   * @param model The shared model every step reads from and writes to
   * @param signal Aborts the pipeline before the next step starts
   * @returns The same model, after every step ran
   * @throws The original error, or a PipelineCompensationError when a compensation failed too
   */
  public async execute(model: TModel, signal: AbortSignal = new AbortController().signal): Promise<TModel> {
    const compensations: PipelineStep<TModel>[] = []

    try {
      for (const { execute, compensate } of this.steps) {
        signal.throwIfAborted()
        await execute(model, signal)

        if (compensate) {
          compensations.push(compensate)
        }
      }

      return model
    } catch (error) {
      const compensationErrors: unknown[] = []

      for (const compensate of compensations.reverse()) {
        try {
          await compensate(model, signal)
        } catch (compensationError) {
          compensationErrors.push(compensationError)
        }
      }

      if (compensationErrors.length > 0) {
        throw new PipelineCompensationError(error, compensationErrors)
      }

      throw error
    }
  }
}
