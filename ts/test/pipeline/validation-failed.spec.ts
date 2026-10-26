import { CompensationStrategy, PipelineBuilder } from '../../src'
import { StudentModel, addYear, subtractYear, calculateNameLength, Aged } from './helpers'

describe('Pipeline: validation failures', () => {
  it('constructor_UnknownCompensationStrategyCase_ShouldThrow', () => {
    // Act & Assert — a plain JavaScript caller can pass anything
    expect(() => new PipelineBuilder<StudentModel>('unknown' as CompensationStrategy)).toThrow(RangeError)
  })

  it('addStep_StepNotResolvingWithModelCase_ShouldNotCompile', () => {
    // Arrange
    const calls: string[] = []

    // Act & Assert — the @ts-expect-error fails the build if a step resolving with nothing is ever accepted
    // @ts-expect-error a step must resolve with the model
    new PipelineBuilder<StudentModel>().addStep(async () => void calls.push('first'))
  })

  it('addStep_NonGenericNarrowTypeStepCase_ShouldNotCompile', () => {
    // Arrange
    const addYearToAged = async (model: Aged): Promise<Aged> => model

    // Act & Assert — a narrow-type step must be generic so it resolves with the pipeline's own model
    // @ts-expect-error a step resolving with a narrower type is rejected
    new PipelineBuilder<StudentModel>().addStep(addYearToAged)
  })

  it('onFailure_CalledTwiceCase_ShouldThrow', () => {
    // Arrange
    const builder = new PipelineBuilder<StudentModel>().addStep(addYear).onFailure(async () => {})

    // Act & Assert
    expect(() => builder.onFailure(async () => {})).toThrow('onFailure() can only be called once')
  })

  it('build_WithNoStepsCase_ShouldThrow', () => {
    // Act & Assert
    expect(() => new PipelineBuilder<StudentModel>().build()).toThrow(
      'At least one step must be added before calling build()',
    )
  })

  it('build_CalledTwiceCase_ShouldThrow', () => {
    // Arrange
    const builder = new PipelineBuilder<StudentModel>().addStep(addYear)
    builder.build()

    // Act & Assert
    expect(() => builder.build()).toThrow('build() can only be called once')
  })

  it('addStep_AfterBuildCase_ShouldThrow', () => {
    // Arrange
    const builder = new PipelineBuilder<StudentModel>().addStep(addYear)
    builder.build()

    // Act & Assert
    expect(() => builder.addStep(calculateNameLength)).toThrow(
      'The pipeline cannot be changed after build() has been called',
    )
  })

  it('addCompensableStep_AfterBuildCase_ShouldThrow', () => {
    // Arrange
    const builder = new PipelineBuilder<StudentModel>().addStep(addYear)
    builder.build()

    // Act & Assert
    expect(() => builder.addCompensableStep(addYear, subtractYear)).toThrow(
      'The pipeline cannot be changed after build() has been called',
    )
  })

  it('onFailure_AfterBuildCase_ShouldThrow', () => {
    // Arrange
    const builder = new PipelineBuilder<StudentModel>().addStep(addYear)
    builder.build()

    // Act & Assert
    expect(() => builder.onFailure(async () => {})).toThrow(
      'The pipeline cannot be changed after build() has been called',
    )
  })
})
