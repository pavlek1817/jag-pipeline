import { CompensationFailure, PipelineError } from './pipeline.errors'

/**
 * A step reads from and writes to the shared model and resolves with it. Declared as a function type (not a method
 * signature) so a step that needs fields the model does not have, or resolves with anything but the model, is a
 * compile error.
 */
export type PipelineStep<TModel> = (model: TModel, signal: AbortSignal) => Promise<TModel>

/**
 * Runs once when the pipeline fails, after the compensations, with the same PipelineError the caller receives.
 */
export type PipelineFailureHandler<TModel> = (
  model: TModel,
  error: PipelineError,
  signal: AbortSignal,
) => Promise<unknown>

/**
 * What the pipeline does when a compensating action fails while it rolls back a failed run.
 */
export enum CompensationStrategy {
  /** Runs every remaining compensation and reports all of their failures. The default. */
  ContinueOnFailure = 'continueOnFailure',
  /** Stops at the first compensation that fails; the steps whose compensation did not run are reported as not compensated. */
  StopOnFirstFailure = 'stopOnFirstFailure',
}

/** @internal */
export type StepDefinition<TModel> = {
  stepIndex: number
  execute: PipelineStep<TModel>
  compensate?: PipelineStep<TModel>
}

/**
 * A compensable pipeline, created by PipelineBuilder.build(). A TypeScript port of JagPav.Pipeline.
 */
export interface Pipeline<TModel> {
  /**
   * Runs the model through every step in order. If a step fails, the compensations of the steps that already
   * succeeded run in reverse order (following the builder's compensation strategy), then the onFailure handler runs
   * (if registered) and a PipelineError is thrown.
   * @param model The shared model every step reads from and writes to
   * @param signal Aborts the pipeline before the next step starts
   * @returns The same model, after every step ran
   * @throws PipelineError when a step failed; its originalError is what the step threw, and failed compensations or
   * a failed onFailure handler are reported on it as well
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
    private readonly failureHandler: PipelineFailureHandler<TModel> | undefined,
    private readonly compensationStrategy: CompensationStrategy,
  ) {}

  public async execute(model: TModel, signal: AbortSignal = new AbortController().signal): Promise<TModel> {
    const compensations: { stepIndex: number; compensate: PipelineStep<TModel> }[] = []
    let failedStepIndex = 0

    try {
      for (const { stepIndex, execute, compensate } of this.steps) {
        failedStepIndex = stepIndex
        signal.throwIfAborted()
        await execute(model, signal)

        if (compensate) {
          compensations.push({ stepIndex, compensate })
        }
      }

      return model
    } catch (error) {
      let pipelineError = await this.compensateAll(model, error, failedStepIndex, compensations, signal)

      if (this.failureHandler) {
        try {
          await this.failureHandler(model, pipelineError, signal)
        } catch (failureHandlerError) {
          pipelineError = pipelineError.withFailureHandlerError(failureHandlerError)
        }
      }

      throw pipelineError
    }
  }

  private async compensateAll(
    model: TModel,
    error: unknown,
    failedStepIndex: number,
    compensations: { stepIndex: number; compensate: PipelineStep<TModel> }[],
    signal: AbortSignal,
  ) {
    const compensationFailures: CompensationFailure[] = []

    // Popping runs the compensations in reverse step order.
    let next = compensations.pop()
    while (next) {
      try {
        await next.compensate(model, signal)
      } catch (compensationError) {
        compensationFailures.push({ stepIndex: next.stepIndex, error: compensationError })
        if (this.compensationStrategy === CompensationStrategy.StopOnFirstFailure) {
          break
        }
      }

      next = compensations.pop()
    }

    // Whatever is left was skipped by StopOnFirstFailure.
    const notCompensatedStepIndexes = compensations.reverse().map((compensation) => compensation.stepIndex)

    return new PipelineError(error, failedStepIndex, compensationFailures, notCompensatedStepIndexes)
  }
}
