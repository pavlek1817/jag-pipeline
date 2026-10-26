import {
  CompensablePipeline,
  CompensationStrategy,
  Pipeline,
  PipelineFailureHandler,
  PipelineStep,
  StepDefinition,
} from './pipeline'

/**
 * Collects the steps and the onFailure handler of a compensable pipeline. build() creates the pipeline once.
 *
 * Pass steps as arrow-function properties or bound functions: a plain method reference loses `this`.
 */
export class PipelineBuilder<TModel> {
  private readonly steps: StepDefinition<TModel>[] = []
  private failureHandler?: PipelineFailureHandler<TModel>
  private isBuilt = false

  /**
   * @param compensationStrategy What the pipeline does when a compensating action fails
   */
  constructor(private readonly compensationStrategy: CompensationStrategy = CompensationStrategy.ContinueOnFailure) {
    if (!Object.values(CompensationStrategy).includes(compensationStrategy)) {
      throw new RangeError(`Unknown compensation strategy: ${compensationStrategy}`)
    }
  }

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
    this.steps.push({ stepIndex: this.steps.length, execute: step, compensate })
    return this
  }

  /**
   * Registers a final action that runs once when the pipeline fails, after the compensations and before the
   * PipelineError is thrown to the caller. Optional; only one handler can be registered. It is not a replacement for a
   * compensating action: undoing a step belongs in addCompensableStep.
   * @param handler Receives the model, the same PipelineError the caller receives, and the signal
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
   * Creates the pipeline from the registered steps. Can be called only once; the builder cannot be changed afterwards.
   * @returns The pipeline, ready to execute
   */
  public build(): Pipeline<TModel> {
    if (this.isBuilt) {
      throw new Error('build() can only be called once')
    }

    if (this.steps.length === 0) {
      throw new Error('At least one step must be added before calling build()')
    }

    const pipeline = new CompensablePipeline([...this.steps], this.failureHandler, this.compensationStrategy)
    this.isBuilt = true
    return pipeline
  }

  private ensureNotBuilt() {
    if (this.isBuilt) {
      throw new Error('The pipeline cannot be changed after build() has been called')
    }
  }
}
