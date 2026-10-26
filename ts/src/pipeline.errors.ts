/**
 * A compensating action that failed, and the step it belongs to.
 */
export type CompensationFailure = {
  /** The 0-based index of the step whose compensation failed, in the order the steps were added */
  readonly stepIndex: number
  /** What the compensation threw */
  readonly error: unknown
}

/**
 * Thrown when a pipeline step fails. `cause` (also `originalError`) is what the step threw; the other members
 * describe the rollback that followed.
 */
export class PipelineError extends Error {
  /** Every error that occurred, in order: the original one, then the failed compensations, then the onFailure handler's */
  public readonly errors: readonly unknown[]

  /**
   * @param originalError What the step that failed threw
   * @param failedStepIndex The 0-based index of the step that failed
   * @param compensationFailures The compensations that failed, in the order they ran
   * @param notCompensatedStepIndexes The steps whose compensation did not run, in the order they would have run
   * @param failureHandlerError What the onFailure handler threw, if it failed
   */
  constructor(
    originalError: unknown,
    public readonly failedStepIndex: number,
    public readonly compensationFailures: readonly CompensationFailure[] = [],
    public readonly notCompensatedStepIndexes: readonly number[] = [],
    public readonly failureHandlerError?: unknown,
  ) {
    super(
      createMessage(originalError, failedStepIndex, compensationFailures, notCompensatedStepIndexes, failureHandlerError),
      { cause: originalError },
    )
    this.name = 'PipelineError'
    this.errors = [
      originalError,
      ...compensationFailures.map((failure) => failure.error),
      ...(failureHandlerError === undefined ? [] : [failureHandlerError]),
    ]
  }

  /** What the step that failed threw; the same as `cause` */
  public get originalError(): unknown {
    return this.cause
  }

  /** Whether every completed step was compensated: no compensation failed or was skipped */
  public get isFullyCompensated(): boolean {
    return this.compensationFailures.length === 0 && this.notCompensatedStepIndexes.length === 0
  }

  /** @internal */
  public withFailureHandlerError(failureHandlerError: unknown): PipelineError {
    return new PipelineError(
      this.originalError,
      this.failedStepIndex,
      this.compensationFailures,
      this.notCompensatedStepIndexes,
      failureHandlerError,
    )
  }
}

const createMessage = (
  originalError: unknown,
  failedStepIndex: number,
  compensationFailures: readonly CompensationFailure[],
  notCompensatedStepIndexes: readonly number[],
  failureHandlerError: unknown,
) => {
  const originalMessage = originalError instanceof Error ? originalError.message : String(originalError)
  let message = `Pipeline step ${failedStepIndex} failed: ${originalMessage.replace(/\.+$/, '')}.`

  if (compensationFailures.length > 0) {
    message += ` ${compensationFailures.length} compensation(s) failed.`
  }

  if (notCompensatedStepIndexes.length > 0) {
    message += ` ${notCompensatedStepIndexes.length} step(s) were not compensated.`
  }

  if (failureHandlerError !== undefined) {
    message += ' The onFailure handler failed too.'
  }

  return message
}
