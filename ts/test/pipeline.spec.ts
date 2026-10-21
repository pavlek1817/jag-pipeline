import { Pipeline, PipelineCompensationError, PipelineFailureHandlerError } from '../src'

type StudentModel = {
  name: string
  age: number
  nameLength: number
}

const createStudent = (): StudentModel => ({ name: 'Alice', age: 20, nameLength: 0 })

const addYear = async (model: StudentModel) => {
  model.age++
}

const subtractYear = async (model: StudentModel) => {
  model.age--
}

const calculateNameLength = async (model: StudentModel) => {
  model.nameLength = model.name.length
}

describe('Pipeline', () => {
  // --- Happy path ---
  it('execute_AllStepsSucceed_ShouldApplyAllStepsToModel', async () => {
    // Arrange
    const student = createStudent()
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(calculateNameLength)
      .create()

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
    const pipeline = new Pipeline<StudentModel>()
      .addStep(async () => void calls.push('first'))
      .addStep(async () => void calls.push('second'))
      .addStep(async () => void calls.push('third'))
      .create()

    // Act
    await pipeline.execute(createStudent())

    // Assert
    expect(calls).toEqual(['first', 'second', 'third'])
  })

  // --- Compensation ---
  it('execute_SecondStepFailsCase_ShouldCompensateFirstStepAndRethrow', async () => {
    // Arrange
    const student = createStudent()
    const error = new Error('step 2 failed')
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(async () => {
        throw error
      })
      .create()

    // Act & Assert
    await expect(pipeline.execute(student)).rejects.toBe(error)

    // Assert — first step was compensated, age reverted
    expect(student.age).toBe(20)
  })

  it('execute_FirstStepFails_ShouldNotCompensateAndRethrow', async () => {
    // Arrange
    const student = createStudent()
    const compensate = jest.fn(subtractYear)
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(async () => {
        throw new Error('step 1 failed')
      }, compensate)
      .addStep(calculateNameLength)
      .create()

    // Act & Assert
    await expect(pipeline.execute(student)).rejects.toThrow('step 1 failed')

    // Assert — first step never completed, nothing compensated
    expect(compensate).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
    expect(student.nameLength).toBe(0)
  })

  it('execute_LastStepFails_ShouldCompensateInReverseOrder', async () => {
    // Arrange
    const calls: string[] = []
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(async () => {}, async () => void calls.push('compensate first'))
      .addCompensableStep(async () => {}, async () => void calls.push('compensate second'))
      .addStep(async () => {
        throw new Error('step 3 failed')
      })
      .create()

    // Act & Assert
    await expect(pipeline.execute(createStudent())).rejects.toThrow('step 3 failed')
    expect(calls).toEqual(['compensate second', 'compensate first'])
  })

  it('execute_CompensationFailsCase_ShouldThrowPipelineCompensationError', async () => {
    // Arrange
    const error = new Error('step 3 failed')
    const compensationError = new Error('compensation failed')
    const compensateFirst = jest.fn(async () => {})
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(async () => {}, compensateFirst)
      .addCompensableStep(async () => {}, async () => {
        throw compensationError
      })
      .addStep(async () => {
        throw error
      })
      .create()

    // Act
    const thrown = await pipeline.execute(createStudent()).catch((e: unknown) => e)

    // Assert — remaining compensations still run, both errors are reported
    expect(thrown).toBeInstanceOf(PipelineCompensationError)
    expect((thrown as PipelineCompensationError).name).toBe('PipelineCompensationError')
    expect((thrown as PipelineCompensationError).error).toBe(error)
    expect((thrown as PipelineCompensationError).compensationErrors).toEqual([compensationError])
    expect(compensateFirst).toHaveBeenCalledTimes(1)
  })

  // --- OnFailure ---
  it('execute_StepFailsWithOnFailureCase_ShouldRunOnFailureAfterCompensationAndRethrow', async () => {
    // Arrange
    const student = createStudent()
    const error = new Error('step 2 failed')
    const calls: string[] = []
    const onFailure = jest.fn(async (model: StudentModel) => void calls.push(`onFailure age=${model.age}`))
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(addYear, async (model) => {
        calls.push('compensate')
        await subtractYear(model)
      })
      .addStep(async () => {
        throw error
      })
      .onFailure(onFailure)
      .create()

    // Act & Assert
    await expect(pipeline.execute(student)).rejects.toBe(error)
    expect(calls).toEqual(['compensate', 'onFailure age=20'])
    expect(onFailure).toHaveBeenCalledWith(student, error, expect.any(AbortSignal))
  })

  it('execute_AllStepsSucceedWithOnFailure_ShouldNotRunOnFailure', async () => {
    // Arrange
    const onFailure = jest.fn(async () => {})
    const pipeline = new Pipeline<StudentModel>().addStep(addYear).onFailure(onFailure).create()

    // Act
    await pipeline.execute(createStudent())

    // Assert
    expect(onFailure).not.toHaveBeenCalled()
  })

  it('execute_OnFailureThrowsCase_ShouldThrowPipelineFailureHandlerError', async () => {
    // Arrange
    const error = new Error('step failed')
    const failureHandlerError = new Error('onFailure failed')
    const pipeline = new Pipeline<StudentModel>()
      .addStep(async () => {
        throw error
      })
      .onFailure(async () => {
        throw failureHandlerError
      })
      .create()

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
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(async () => {}, async () => {
        throw new Error('compensation failed')
      })
      .addStep(async () => {
        throw error
      })
      .onFailure(onFailure)
      .create()

    // Act & Assert
    await expect(pipeline.execute(createStudent())).rejects.toBeInstanceOf(PipelineCompensationError)
    expect(onFailure).toHaveBeenCalledWith(expect.anything(), error, expect.any(AbortSignal))
  })

  it('onFailure_CalledTwiceCase_ShouldThrow', () => {
    // Arrange
    const pipeline = new Pipeline<StudentModel>().addStep(addYear).onFailure(async () => {})

    // Act & Assert
    expect(() => pipeline.onFailure(async () => {})).toThrow('onFailure() can only be called once')
  })

  // --- Guard clauses ---
  it('create_WithNoStepsCase_ShouldThrow', () => {
    // Act & Assert
    expect(() => new Pipeline<StudentModel>().create()).toThrow(
      'At least one step must be added before calling create()',
    )
  })

  it('execute_BeforeCreateCase_ShouldThrow', async () => {
    // Arrange
    const pipeline = new Pipeline<StudentModel>().addStep(addYear)

    // Act & Assert
    await expect(pipeline.execute(createStudent())).rejects.toThrow('Call create() before execute()')
  })

  it('addStep_AfterCreateCase_ShouldThrow', () => {
    // Arrange
    const pipeline = new Pipeline<StudentModel>().addStep(addYear).create()

    // Act & Assert
    expect(() => pipeline.addStep(calculateNameLength)).toThrow(
      'The pipeline cannot be changed after create() has been called',
    )
  })

  it('addCompensableStep_AfterCreateCase_ShouldThrow', () => {
    // Arrange
    const pipeline = new Pipeline<StudentModel>().addStep(addYear).create()

    // Act & Assert
    expect(() => pipeline.addCompensableStep(addYear, subtractYear)).toThrow(
      'The pipeline cannot be changed after create() has been called',
    )
  })

  it('onFailure_AfterCreateCase_ShouldThrow', () => {
    // Arrange
    const pipeline = new Pipeline<StudentModel>().addStep(addYear).create()

    // Act & Assert
    expect(() => pipeline.onFailure(async () => {})).toThrow(
      'The pipeline cannot be changed after create() has been called',
    )
  })

  // --- Cancellation ---
  it('execute_SignalAlreadyAbortedCase_ShouldNotRunAnyStep', async () => {
    // Arrange
    const controller = new AbortController()
    controller.abort()
    const step = jest.fn(addYear)
    const pipeline = new Pipeline<StudentModel>().addStep(step).create()

    // Act & Assert
    await expect(pipeline.execute(createStudent(), controller.signal)).rejects.toThrow()
    expect(step).not.toHaveBeenCalled()
  })

  it('execute_SignalAbortedMidwayCase_ShouldStopAndCompensateCompletedSteps', async () => {
    // Arrange
    const student = createStudent()
    const controller = new AbortController()
    const lastStep = jest.fn(calculateNameLength)
    const pipeline = new Pipeline<StudentModel>()
      .addCompensableStep(addYear, subtractYear)
      .addStep(async () => controller.abort())
      .addStep(lastStep)
      .create()

    // Act & Assert
    await expect(pipeline.execute(student, controller.signal)).rejects.toThrow()
    expect(lastStep).not.toHaveBeenCalled()
    expect(student.age).toBe(20)
  })

  it('execute_StepReceivesSignal_ShouldPassSameSignalToSteps', async () => {
    // Arrange
    const controller = new AbortController()
    const step = jest.fn(async () => {})
    const pipeline = new Pipeline<StudentModel>().addStep(step).create()
    const student = createStudent()

    // Act
    await pipeline.execute(student, controller.signal)

    // Assert
    expect(step).toHaveBeenCalledWith(student, controller.signal)
  })
})
