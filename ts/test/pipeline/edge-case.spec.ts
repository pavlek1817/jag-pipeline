import { PipelineBuilder, PipelineCompensationError, PipelineFailureHandlerError } from '../../src'
import { StudentModel, createStudent, addYear, subtractYear, calculateNameLength, doNothing } from './helpers'

describe('Pipeline: edge cases', () => {
  it('execute_FirstStepFails_ShouldNotCompensateAndRethrow', async () => {
    // Arrange
    const student = createStudent()
    const compensate = jest.fn(subtractYear)
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(async () => {
        throw new Error('step 1 failed')
      }, compensate)
      .addStep(calculateNameLength)
      .build()

    // Act & Assert
    await expect(pipeline.execute(student)).rejects.toThrow('step 1 failed')

    // Assert — first step never completed, nothing compensated
    expect(compensate).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
    expect(student.nameLength).toBe(0)
  })

  it('execute_CompensationFailsCase_ShouldThrowPipelineCompensationError', async () => {
    // Arrange
    const error = new Error('step 3 failed')
    const compensationError = new Error('compensation failed')
    const compensateFirst = jest.fn(doNothing)
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(doNothing, compensateFirst)
      .addCompensableStep(doNothing, async () => {
        throw compensationError
      })
      .addStep(async () => {
        throw error
      })
      .build()

    // Act
    const thrown = await pipeline.execute(createStudent()).catch((e: unknown) => e)

    // Assert — remaining compensations still run, both errors are reported
    expect(thrown).toBeInstanceOf(PipelineCompensationError)
    expect((thrown as PipelineCompensationError).name).toBe('PipelineCompensationError')
    expect((thrown as PipelineCompensationError).error).toBe(error)
    expect((thrown as PipelineCompensationError).compensationErrors).toEqual([compensationError])
    expect(compensateFirst).toHaveBeenCalledTimes(1)
  })

  it('execute_OnFailureThrowsCase_ShouldThrowPipelineFailureHandlerError', async () => {
    // Arrange
    const error = new Error('step failed')
    const failureHandlerError = new Error('onFailure failed')
    const pipeline = new PipelineBuilder<StudentModel>()
      .addStep(async () => {
        throw error
      })
      .onFailure(async () => {
        throw failureHandlerError
      })
      .build()

    // Act
    const thrown = await pipeline.execute(createStudent()).catch((e: unknown) => e)

    // Assert
    expect(thrown).toBeInstanceOf(PipelineFailureHandlerError)
    expect((thrown as PipelineFailureHandlerError).name).toBe('PipelineFailureHandlerError')
    expect((thrown as PipelineFailureHandlerError).error).toBe(error)
    expect((thrown as PipelineFailureHandlerError).failureHandlerError).toBe(failureHandlerError)
  })

  it('execute_CompensationThrowsWithOnFailureCase_ShouldRunOnFailureAndThrowPipelineCompensationError', async () => {
    // Arrange
    const error = new Error('step 2 failed')
    const onFailure = jest.fn(async () => {})
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(doNothing, async () => {
        throw new Error('compensation failed')
      })
      .addStep(async () => {
        throw error
      })
      .onFailure(onFailure)
      .build()

    // Act & Assert
    await expect(pipeline.execute(createStudent())).rejects.toBeInstanceOf(PipelineCompensationError)
    expect(onFailure).toHaveBeenCalledWith(expect.anything(), error, expect.any(AbortSignal))
  })

  it('execute_SignalAlreadyAbortedCase_ShouldNotRunAnyStep', async () => {
    // Arrange
    const controller = new AbortController()
    controller.abort()
    const step = jest.fn(addYear)
    const pipeline = new PipelineBuilder<StudentModel>().addStep(step).build()

    // Act & Assert
    await expect(pipeline.execute(createStudent(), controller.signal)).rejects.toThrow()
    expect(step).not.toHaveBeenCalled()
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

    // Act & Assert
    await expect(pipeline.execute(student, controller.signal)).rejects.toThrow()
    expect(lastStep).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
  })
})
