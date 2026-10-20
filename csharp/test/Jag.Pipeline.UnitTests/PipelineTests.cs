using Jag.Pipeline.Core;

namespace Jag.Pipeline.UnitTests;

internal sealed class PipelineTests : TestBase
{
    // --- Happy path ---
    [Test]
    public async Task ExecuteAsync_AllStepsSucceed_ShouldApplyAllStepsToModel()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new Pipeline<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddStep(calculateNameLength)
            .CreatePipeline();

        // Act
        var result = await pipeline.ExecuteAsync(student);

        // Assert
        result.Age.Should().Be(21);
        result.NameLength.Should().Be(5);
    }

    // --- Compensation ---
    [Test]
    public async Task ExecuteAsync_SecondStepFailsCase_ShouldCompensateFirstStepAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new Pipeline<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddStep((_, _) => throw new InvalidOperationException("step 2 failed"))
            .CreatePipeline();

        // Act
        await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — first step was compensated, Age reverted
        student.Age.Should().Be(20);
    }

    [Test]
    public async Task ExecuteAsync_FirstStepFails_ShouldNotCompensateAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new Pipeline<StudentModel>()
            .AddCompensableStep(
                (_, _) => throw new InvalidOperationException("step 1 failed"),
                subtractYear)
            .AddStep(calculateNameLength)
            .CreatePipeline();

        // Act
        await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — first step never completed, nothing compensated
        student.Age.Should().Be(20);
        student.NameLength.Should().Be(0);
    }

    // --- Guard clauses ---
    [Test]
    public void CreatePipeline_WithNoStepsCase_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        new Pipeline<StudentModel>()
            .Invoking(p => p.CreatePipeline())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteAsync_BeforeCreatePipelineCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear);

        // Act & Assert
        await pipeline
            .Invoking(p => p.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public void AddCompensableStep_AfterCreatePipelineCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .CreatePipeline();

        // Act & Assert
        pipeline
            .Invoking(p => p.AddCompensableStep(addYear, subtractYear))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void AddStep_AfterCreatePipelineCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .CreatePipeline();

        // Act & Assert
        pipeline
            .Invoking(p => p.AddStep(calculateNameLength))
            .Should().Throw<InvalidOperationException>();
    }

    private static Task addYear(StudentModel model, CancellationToken ct)
    {
        model.Age++;
        return Task.CompletedTask;
    }

    private static Task subtractYear(StudentModel model, CancellationToken ct)
    {
        model.Age--;
        return Task.CompletedTask;
    }

    private static Task calculateNameLength(StudentModel model, CancellationToken ct)
    {
        model.NameLength = model.Name.Length;
        return Task.CompletedTask;
    }

    private sealed class StudentModel
    {
        internal string Name { get; init; } = string.Empty;

        internal int Age { get; set; }

        internal int NameLength { get; set; }
    }
}
