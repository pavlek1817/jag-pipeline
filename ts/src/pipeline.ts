/**
 * A step reads from and writes to the shared model and resolves with it. Declared as a function type (not a method
 * signature) so a step that needs fields the model does not have, or resolves with anything but the model, is a
 * compile error.
 */
export type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<TModel>

/**
 * A step failed and so did at least one of the compensations run for it.
 */
export class PipelineCompensationError extends Error {
  constructor(public readonly error: unknown, public readonly compensationErrors: unknown[]) {
    super(`Pipeline step failed and ${compensationErrors.length} compensation(s) failed too`)
    this.name = 'PipelineCompensationError'
  }
}

/**
 * Runs once when the pipeline fails, after the compensations, with the error that failed the pipeline.
 */
export type PipelineFailureHandler<TModel> = (model: TModel, error: unknown, signal: AbortSignal) => Promise<unknown>

/**
 * The pipeline failed and so did its onFailure handler.
 */
export class PipelineFailureHandlerError extends Error {
  constructor(public readonly error: unknown, public readonly failureHandlerError: unknown) {
    super('Pipeline step failed and the onFailure handler failed too')
    this.name = 'PipelineFailureHandlerError'
  }
}

/** @internal */
export type StepDefinition<TModel> = {
  execute: PipelineStep<TModel>
  compensate?: PipelineStep<TModel>
}

/**
 * A compensable pipeline, created by PipelineBuilder.build(). A TypeScript port of JagPav.Pipeline.
 */
export interface Pipeline<TModel> {
  /**
   * Runs the model through every step in order. If a step fails, the compensations of the steps that already
   * succeeded run in reverse order, then the onFailure handler runs (if registered) and the original error is re-thrown.
   * @param model The shared model every step reads from and writes to
   * @param signal Aborts the pipeline before the next step starts
   * @returns The same model, after every step ran
   * @throws The original error, a PipelineCompensationError when a compensation failed too,
   * or a PipelineFailureHandlerError when the onFailure handler failed
   */
  execute(model: TModel, signal?: AbortSignal): Promise<TModel>
}

/**
 * Not part of the public API: a pipeline can only be created through PipelineBuilder.build().
 * @internal
 */
export class CompensablePipeline<TModel> implements Pipeline<TModel> {
  constructor(
    private readonly steps: readonly StepDefinition<TModel>[],
    private readonly failureHandler?: PipelineFailureHandler<TModel>,
  ) {}

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

      const pipelineError = compensationErrors.length > 0 ? new PipelineCompensationError(error, compensationErrors) : error

      if (this.failureHandler) {
        try {
          await this.failureHandler(model, error, signal)
        } catch (failureHandlerError) {
          throw new PipelineFailureHandlerError(pipelineError, failureHandlerError)
        }
      }

      throw pipelineError
    }
  }
}
