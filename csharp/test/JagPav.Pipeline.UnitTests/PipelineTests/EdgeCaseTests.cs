using JagPav.Pipeline.Core;

namespace JagPav.Pipeline.UnitTests.PipelineTests;

internal sealed class EdgeCaseTests : PipelineTestBase
{
    [Test]
    public async Task ExecuteAsync_FirstStepFails_ShouldNotCompensateAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(
                (_, _) => throw new InvalidOperationException("step 1 failed"),
                subtractYear)
            .AddStep(calculateNameLength)
            .Build();

        // Act
        await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — first step never completed, nothing compensated
        student.Age.Should().Be(20);
        student.NameLength.Should().Be(0);
    }

    [Test]
    public async Task ExecuteAsync_OnFailureThrowsCase_ShouldRethrowOriginalAndKeepOnFailureError()
    {
        // Arrange
        var error = new InvalidOperationException("step failed");
        var onFailureError = new InvalidOperationException("onFailure failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddStep((_, _) => throw error)
            .OnFailure((_, _, _) => throw onFailureError)
            .Build();

        // Act
        var task = pipeline.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 });
        var thrown = await task
            .Invoking(t => t)
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert
        thrown.Which.Should().BeSameAs(error);
        task.Exception!.InnerExceptions.Should().Equal(error, onFailureError);
    }

    [Test]
    public async Task ExecuteAsync_CompensationThrowsCase_ShouldStillRunOnFailureAndRethrowOriginal()
    {
        // Arrange
        var error = new InvalidOperationException("step 2 failed");
        var compensationError = new InvalidOperationException("compensation failed");
        var onFailureCalled = false;
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, (_, _) => throw compensationError)
            .AddStep((_, _) => throw error)
            .OnFailure((_, _, _) =>
            {
                onFailureCalled = true;
                return Task.CompletedTask;
            })
            .Build();

        // Act
        var task = pipeline.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 });
        var thrown = await task
            .Invoking(t => t)
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert
        thrown.Which.Should().BeSameAs(error);
        task.Exception!.InnerExceptions.Should().Equal(error, compensationError);
        onFailureCalled.Should().BeTrue();
    }
}
