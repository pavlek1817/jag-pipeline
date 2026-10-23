using JagPav.Pipeline.Core;

namespace JagPav.Pipeline.UnitTests.PipelineTests;

internal sealed class OrdinaryCaseTests : PipelineTestBase
{
    [Test]
    public async Task ExecuteAsync_AllStepsSucceed_ShouldApplyAllStepsToModel()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddStep(calculateNameLength)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(student);

        // Assert
        result.Age.Should().Be(21);
        result.NameLength.Should().Be(5);
    }

    [Test]
    public async Task ExecuteAsync_GenericNarrowInterfaceStep_ShouldRunAgainstModel()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYearToAnyAged, subtractYear)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(student);

        // Assert
        result.Age.Should().Be(21);
    }

    [Test]
    public async Task ExecuteAsync_SecondStepFailsCase_ShouldCompensateFirstStepAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddStep((_, _) => throw new InvalidOperationException("step 2 failed"))
            .Build();

        // Act
        await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — first step was compensated, Age reverted
        student.Age.Should().Be(20);
    }

    [Test]
    public async Task ExecuteAsync_StepFailsWithOnFailureCase_ShouldRunOnFailureAfterCompensationAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var error = new InvalidOperationException("step 2 failed");
        var calls = new List<string>();
        Exception? receivedError = null;
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, (m, ct) =>
            {
                calls.Add("compensate");
                return subtractYear(m, ct);
            })
            .AddStep((_, _) => throw error)
            .OnFailure((m, ex, _) =>
            {
                calls.Add($"onFailure age={m.Age}");
                receivedError = ex;
                return Task.CompletedTask;
            })
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — compensation ran first, then OnFailure got the original error, which still reached the caller
        thrown.Which.Should().BeSameAs(error);
        receivedError.Should().BeSameAs(error);
        calls.Should().Equal("compensate", "onFailure age=20");
    }

    [Test]
    public async Task ExecuteAsync_AllStepsSucceedWithOnFailure_ShouldNotRunOnFailure()
    {
        // Arrange
        var onFailureCalled = false;
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddStep(addYear)
            .OnFailure((_, _, _) =>
            {
                onFailureCalled = true;
                return Task.CompletedTask;
            })
            .Build();

        // Act
        await pipeline.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 });

        // Assert
        onFailureCalled.Should().BeFalse();
    }
}
