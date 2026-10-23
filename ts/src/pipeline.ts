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

type StepDefinition<TModel> = {
  execute: PipelineStep<TModel>
  compensate?: PipelineStep<TModel>
}

/**
 * Compensable pipeline, a TypeScript port of JagPav.Pipeline
 * Runs the model through every step in order. If a step fails, the compensations of the steps that already
 * succeeded run in reverse order, then the onFailure handler runs (if registered) and the original error is re-thrown.
 *
 * Pass steps as arrow-function properties or bound functions: a plain method reference loses `this`.
 */
export class Pipeline<TModel> {
  private readonly steps: StepDefinition<TModel>[] = []
  private failureHandler?: PipelineFailureHandler<TModel>
  private isBuilt = false

  /**
   * Registers a step with no compensation.
   * @param step The step to run
   */
  public addStep(step: PipelineStep<TModel>) {
    return this.addCompensableStep(step)
  }

  /**
   * Registers a step paired with a compensating action that runs if a later step fails.
   * @param step The step to run
   * @param compensate Undoes the step; omit it to skip compensation
   */
  public addCompensableStep(step: PipelineStep<TModel>, compensate?: PipelineStep<TModel>) {
    this.ensureNotBuilt()
    this.steps.push({ execute: step, compensate })
    return this
  }

  /**
   * Registers a final action that runs once when the pipeline fails, after the compensations and before the error
   * is re-thrown to the caller. Optional; only one handler can be registered.
   * @param handler Receives the model, the error that failed the pipeline and the signal
   */
  public onFailure(handler: PipelineFailureHandler<TModel>) {
    this.ensureNotBuilt()
    if (this.failureHandler) {
      throw new Error('onFailure() can only be called once')
    }

    this.failureHandler = handler
    return this
  }

  /**
   * Finalizes the pipeline. Must be called before execute; no steps or onFailure handler can be added afterwards.
   */
  public build() {
    if (this.steps.length === 0) {
      throw new Error('At least one step must be added before calling build()')
    }

    this.isBuilt = true
    return this
  }

  /**
   * Runs the model through every step in order. Call build() first.
   * @param model The shared model every step reads from and writes to
   * @param signal Aborts the pipeline before the next step starts
   * @returns The same model, after every step ran
   * @throws The original error, a PipelineCompensationError when a compensation failed too,
   * or a PipelineFailureHandlerError when the onFailure handler failed
   */
  public async execute(model: TModel, signal: AbortSignal = new AbortController().signal): Promise<TModel> {
    if (!this.isBuilt) {
      throw new Error('Call build() before execute()')
    }

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

  private ensureNotBuilt() {
    if (this.isBuilt) {
      throw new Error('The pipeline cannot be changed after build() has been called')
    }
  }
}
