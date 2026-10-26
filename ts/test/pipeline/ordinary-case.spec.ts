import { PipelineBuilder, PipelineError } from '../../src'
import {
  StudentModel,
  createStudent,
  addYear,
  subtractYear,
  calculateNameLength,
  addYearToAnyAged,
  doNothing,
  executeAndCatch,
  record,
} from './helpers'

describe('Pipeline: ordinary cases', () => {
  it('execute_AllStepsSucceed_ShouldApplyAllStepsToModel', async () => {
    // Arrange
    const student = createStudent()
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(calculateNameLength)
      .build()

    // Act
    const result = await pipeline.execute(student)

    // Assert
    expect(result).toBe(student)
    expect(result.age).toBe(21)
    expect(result.nameLength).toBe(5)
  })

  it('execute_MultipleSteps_ShouldRunStepsInOrder', async () => {
    // Arrange
    const calls: string[] = []
    const pipeline = new PipelineBuilder<StudentModel>()
      .addStep(record(calls, 'first'))
      .addStep(record(calls, 'second'))
      .addStep(record(calls, 'third'))
      .build()

    // Act
    await pipeline.execute(createStudent())

    // Assert
    expect(calls).toEqual(['first', 'second', 'third'])
  })

  it('execute_GenericNarrowTypeStep_ShouldRunAgainstModel', async () => {
    // Arrange
    const pipeline = new PipelineBuilder<StudentModel>().addCompensableStep(addYearToAnyAged, subtractYear).build()

    // Act
    const result = await pipeline.execute(createStudent())

    // Assert
    expect(result.age).toBe(21)
  })

  it('execute_SecondStepFailsCase_ShouldCompensateFirstStepAndThrowPipelineError', async () => {
    // Arrange
    const student = createStudent()
    const error = new Error('step 2 failed')
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(async () => {
        throw error
      })
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student)

    // Assert — first step was compensated, age reverted, and the step's error is the cause
    expect(student.age).toBe(20)
    expect(thrown.name).toBe('PipelineError')
    expect(thrown.originalError).toBe(error)
    expect(thrown.cause).toBe(error)
    expect(thrown.failedStepIndex).toBe(1)
    expect(thrown.isFullyCompensated).toBe(true)
    expect(thrown.errors).toEqual([error])
  })

  it('execute_LastStepFails_ShouldCompensateInReverseOrder', async () => {
    // Arrange
    const calls: string[] = []
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(doNothing, record(calls, 'compensate first'))
      .addCompensableStep(doNothing, record(calls, 'compensate second'))
      .addStep(async () => {
        throw new Error('step 3 failed')
      })
      .build()

    // Act & Assert
    await expect(pipeline.execute(createStudent())).rejects.toBeInstanceOf(PipelineError)
    expect(calls).toEqual(['compensate second', 'compensate first'])
  })

  it('execute_StepFailsWithOnFailureCase_ShouldRunOnFailureAfterCompensationWithSameError', async () => {
    // Arrange
    const student = createStudent()
    const error = new Error('step 2 failed')
    const calls: string[] = []
    const onFailure = jest.fn(async (model: StudentModel) => void calls.push(`onFailure age=${model.age}`))
    const pipeline = new PipelineBuilder<StudentModel>()
      .addCompensableStep(addYear, async (model) => {
        calls.push('compensate')
        return subtractYear(model)
      })
      .addStep(async () => {
        throw error
      })
      .onFailure(onFailure)
      .build()

    // Act
    const thrown = await executeAndCatch(pipeline, student)

    // Assert — compensation ran first, then onFailure got the same error the caller receives
    expect(calls).toEqual(['compensate', 'onFailure age=20'])
    expect(onFailure).toHaveBeenCalledWith(student, thrown, expect.any(AbortSignal))
    expect(thrown.originalError).toBe(error)
  })

  it('execute_AllStepsSucceedWithOnFailure_ShouldNotRunOnFailure', async () => {
    // Arrange
    const onFailure = jest.fn(async () => {})
    const pipeline = new PipelineBuilder<StudentModel>().addStep(addYear).onFailure(onFailure).build()

    // Act
    await pipeline.execute(createStudent())

    // Assert
    expect(onFailure).not.toHaveBeenCalled()
  })

  it('execute_StepReceivesSignal_ShouldPassSameSignalToSteps', async () => {
    // Arrange
    const controller = new AbortController()
    const step = jest.fn(doNothing)
    const pipeline = new PipelineBuilder<StudentModel>().addStep(step).build()
    const student = createStudent()

    // Act
    await pipeline.execute(student, controller.signal)

    // Assert
    expect(step).toHaveBeenCalledWith(student, controller.signal)
  })
})
