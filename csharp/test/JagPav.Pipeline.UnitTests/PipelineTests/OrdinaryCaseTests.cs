using JagPav.Pipeline.Core;
using JagPav.Pipeline.Exceptions;

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
    public async Task ExecuteAsync_SecondStepFailsCase_ShouldCompensateFirstStepAndThrowPipelineException()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var error = new InvalidOperationException("step 2 failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddStep((_, _) => throw error)
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — first step was compensated, Age reverted, and the step's exception is the cause
        student.Age.Should().Be(20);
        thrown.Which.OriginalException.Should().BeEquivalentTo(error);
        thrown.Which.InnerException.Should().BeEquivalentTo(error);
        thrown.Which.FailedStepIndex.Should().Be(1);
        thrown.Which.IsFullyCompensated.Should().BeTrue();
        thrown.Which.Exceptions.Should().Equal(error);
    }

    [Test]
    public async Task ExecuteAsync_StepFailsWithOnFailureCase_ShouldRunOnFailureAfterCompensationWithSameException()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var error = new InvalidOperationException("step 2 failed");
        var calls = new List<string>();
        PipelineException? receivedException = null;
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
                receivedException = ex;
                return Task.CompletedTask;
            })
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — compensation ran first, then OnFailure got the same exception the caller receives
        calls.Should().Equal("compensate", "onFailure age=20");
        receivedException.Should().BeEquivalentTo(thrown.Which);
        receivedException!.OriginalException.Should().BeEquivalentTo(error);
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
