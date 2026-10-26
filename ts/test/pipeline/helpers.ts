import { Pipeline, PipelineError } from '../../src'

export type StudentModel = {
  name: string
  age: number
  nameLength: number
}

export const createStudent = (): StudentModel => ({ name: 'Alice', age: 20, nameLength: 0 })

export const addYear = async (model: StudentModel) => {
  model.age++
  return model
}

export const subtractYear = async (model: StudentModel) => {
  model.age--
  return model
}

export const calculateNameLength = async (model: StudentModel) => {
  model.nameLength = model.name.length
  return model
}

export type Aged = { age: number }

export const addYearToAnyAged = async <T extends Aged>(model: T) => {
  model.age++
  return model
}

export const doNothing = async (model: StudentModel) => model

export const record = (calls: string[], call: string) => async (model: StudentModel) => {
  calls.push(call)
  return model
}

export const executeAndCatch = async (
  pipeline: Pipeline<StudentModel>,
  model: StudentModel = createStudent(),
  signal?: AbortSignal,
): Promise<PipelineError> => {
  try {
    await pipeline.execute(model, signal)
  } catch (error) {
    expect(error).toBeInstanceOf(PipelineError)
    return error as PipelineError
  }

  throw new Error('Expected the pipeline to fail')
}
