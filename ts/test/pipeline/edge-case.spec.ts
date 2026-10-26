import { CompensationStrategy, PipelineBuilder, PipelineError } from '../../src'
import {
  StudentModel,
  createStudent,
  addYear,
  subtractYear,
  calculateNameLength,
  doNothing,
  executeAndCatch,
} from './helpers'

const failWith = (error: unknown) => async (): Promise<StudentModel> => {
  throw error
}

describe('Pipeline: edge cases', () => {
  it('execute_FirstStepFails_ShouldNotCompensateAndThrowPipelineError', async () => {
    // Arrange
    const student = createStudent()
    const error = new Error('step 1 failed')
    const compensate = jest.fn(subtractYear)
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(failWith(error), compensate)
      .addStep(calculateNameLength)
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student)

    // Assert — first step never completed, nothing compensated
    expect(compensate).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
    expect(student.nameLength).toBe(0)
    expect(thrown.originalError).toBe(error)
    expect(thrown.failedStepIndex).toBe(0)
    expect(thrown.isFullyCompensated).toBe(true)
  })

  it('execute_OnFailureThrowsCase_ShouldKeepOriginalAndOnFailureError', async () => {
    // Arrange
    const error = new Error('step failed')
    const failureHandlerError = new Error('onFailure failed')
    const pipeline = new PipelineBuilder<StudentModel>()
      .addStep(failWith(error))
      .onFailure(failWith(failureHandlerError))
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline)

    // Assert
    expect(thrown.originalError).toBe(error)
    expect(thrown.failureHandlerError).toBe(failureHandlerError)
    expect(thrown.errors).toEqual([error, failureHandlerError])
  })

  it('execute_CompensationThrowsWithOnFailureCase_ShouldRunOnFailureAndReportCompensationFailure', async () => {
    // Arrange
    const error = new Error('step 2 failed')
    const compensationError = new Error('compensation failed')
    const onFailure = jest.fn(async () => {})
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(doNothing, failWith(compensationError))
      .addStep(failWith(error))
      .onFailure(onFailure)
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline)

    // Assert — onFailure already sees the failed compensation
    expect(thrown.originalError).toBe(error)
    expect(thrown.isFullyCompensated).toBe(false)
    expect(thrown.compensationFailures).toEqual([{ stepIndex: 0, error: compensationError }])
    expect(thrown.errors).toEqual([error, compensationError])
    expect(onFailure).toHaveBeenCalledWith(expect.anything(), thrown, expect.any(AbortSignal))
  })

  it('execute_DefaultStrategyMiddleCompensationFailsCase_ShouldStillCompensateEarlierSteps', async () => {
    // Arrange
    const student = createStudent()
    const compensationError = new Error('compensation 1 failed')
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addCompensableStep(doNothing, failWith(compensationError))
      .addStep(failWith(new Error('step 3 failed')))
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student)

    // Assert — continueOnFailure is the default: the first step is compensated despite the failure before it
    expect(student.age).toBe(20)
    expect(thrown.failedStepIndex).toBe(2)
    expect(thrown.compensationFailures).toEqual([{ stepIndex: 1, error: compensationError }])
    expect(thrown.notCompensatedStepIndexes).toEqual([])
    expect(thrown.isFullyCompensated).toBe(false)
  })

  it('execute_StopOnFirstFailureMiddleCompensationFailsCase_ShouldStopAndReportNotCompensatedSteps', async () => {
    // Arrange
    const student = createStudent()
    const compensationError = new Error('compensation 1 failed')
    const pipeline = new PipelineBuilder<StudentModel>(CompensationStrategy.StopOnFirstFailure)
      .addCompensableStep(addYear, subtractYear)
      .addCompensableStep(doNothing, failWith(compensationError))
      .addStep(failWith(new Error('step 3 failed')))
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student)

    // Assert — the first step's compensation never ran
    expect(student.age).toBe(21)
    expect(thrown.compensationFailures).toEqual([{ stepIndex: 1, error: compensationError }])
    expect(thrown.notCompensatedStepIndexes).toEqual([0])
    expect(thrown.isFullyCompensated).toBe(false)
  })

  it('execute_TwoCompensationsFailCase_ShouldReportBothInCompensationOrder', async () => {
    // Arrange
    const error = new Error('step 3 failed')
    const firstCompensationError = new Error('compensation 0 failed')
    const secondCompensationError = new Error('compensation 1 failed')
    const pipeline = new PipelineBuilder<StudentModel>(CompensationStrategy.ContinueOnFailure)
      .addCompensableStep(doNothing, failWith(firstCompensationError))
      .addCompensableStep(doNothing, failWith(secondCompensationError))
      .addStep(failWith(error))
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline)

    // Assert — compensations run in reverse, so step 1's failure comes first
    expect(thrown.compensationFailures.map((failure) => failure.stepIndex)).toEqual([1, 0])
    expect(thrown.errors).toEqual([error, secondCompensationError, firstCompensationError])
  })

  it('execute_CompensationAndOnFailureThrowCase_ShouldKeepEveryErrorInOrder', async () => {
    // Arrange
    const error = new Error('step 2 failed')
    const compensationError = new Error('compensation failed')
    const failureHandlerError = new Error('onFailure failed')
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(doNothing, failWith(compensationError))
      .addStep(failWith(error))
      .onFailure(failWith(failureHandlerError))
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline)

    // Assert
    expect(thrown.failureHandlerError).toBe(failureHandlerError)
    expect(thrown.compensationFailures).toHaveLength(1)
    expect(thrown.errors).toEqual([error, compensationError, failureHandlerError])
  })

  it('execute_SignalAlreadyAbortedCase_ShouldNotRunAnyStep', async () => {
    // Arrange
    const controller = new AbortController()
    controller.abort()
    const step = jest.fn(addYear)
    const pipeline = new PipelineBuilder<StudentModel>().addStep(step).build()

    // Act
    const thrown = await executeAndCatch(pipeline, createStudent(), controller.signal)

    // Assert — the abort reason is the original error, reported against the step that did not run
    expect(step).not.toHaveBeenCalled()
    expect(thrown.originalError).toBe(controller.signal.reason)
    expect(thrown.failedStepIndex).toBe(0)
  })

  it('execute_SignalAbortedMidwayCase_ShouldStopAndCompensateCompletedSteps', async () => {
    // Arrange
    const student = createStudent()
    const controller = new AbortController()
    const lastStep = jest.fn(calculateNameLength)
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(async (model) => {
        controller.abort()
        return model
      })
      .addStep(lastStep)
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student, controller.signal)

    // Assert
    expect(lastStep).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
    expect(thrown.failedStepIndex).toBe(2)
    expect(thrown.isFullyCompensated).toBe(true)
  })
})
