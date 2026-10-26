using JagPav.Pipeline.Core;
using JagPav.Pipeline.Exceptions;

namespace JagPav.Pipeline.UnitTests.PipelineTests;

internal sealed class EdgeCaseTests : PipelineTestBase
{
    [Test]
    public async Task ExecuteAsync_FirstStepFails_ShouldNotCompensateAndThrowPipelineException()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var error = new InvalidOperationException("step 1 failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep((_, _) => throw error, subtractYear)
            .AddStep(calculateNameLength)
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — first step never completed, nothing compensated
        student.Age.Should().Be(20);
        student.NameLength.Should().Be(0);
        thrown.Which.OriginalException.Should().BeEquivalentTo(error);
        thrown.Which.FailedStepIndex.Should().Be(0);
        thrown.Which.IsFullyCompensated.Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_OnFailureThrowsCase_ShouldKeepOriginalAndOnFailureException()
    {
        // Arrange
        var error = new InvalidOperationException("step failed");
        var onFailureError = new InvalidOperationException("onFailure failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddStep((_, _) => throw error)
            .OnFailure((_, _, _) => throw onFailureError)
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 }))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert
        thrown.Which.OriginalException.Should().BeEquivalentTo(error);
        thrown.Which.FailureHandlerException.Should().BeEquivalentTo(onFailureError);
        thrown.Which.Exceptions.Should().Equal(error, onFailureError);
    }

    [Test]
    public async Task ExecuteAsync_CompensationThrowsCase_ShouldStillRunOnFailureAndReportCompensationFailure()
    {
        // Arrange
        var error = new InvalidOperationException("step 2 failed");
        var compensationError = new InvalidOperationException("compensation failed");
        PipelineException? receivedException = null;
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, (_, _) => throw compensationError)
            .AddStep((_, _) => throw error)
            .OnFailure((_, ex, _) =>
            {
                receivedException = ex;
                return Task.CompletedTask;
            })
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 }))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — OnFailure already sees the failed compensation
        thrown.Which.OriginalException.Should().BeEquivalentTo(error);
        thrown.Which.IsFullyCompensated.Should().BeFalse();
        thrown.Which.CompensationFailures.Should().ContainSingle()
            .Which.Should().Match<CompensationFailure>(f => f.StepIndex == 0 && f.Exception == compensationError);
        thrown.Which.Exceptions.Should().Equal(error, compensationError);
        receivedException.Should().BeEquivalentTo(thrown.Which);
    }

    [Test]
    public async Task ExecuteAsync_DefaultStrategyMiddleCompensationFailsCase_ShouldStillCompensateEarlierSteps()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var compensationError = new InvalidOperationException("compensation 1 failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep(addYear, subtractYear)
            .AddCompensableStep((m, _) => Task.FromResult(m), (_, _) => throw compensationError)
            .AddStep((_, _) => throw new InvalidOperationException("step 3 failed"))
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — ContinueOnFailure is the default: the first step is compensated despite the failure before it
        student.Age.Should().Be(20);
        thrown.Which.FailedStepIndex.Should().Be(2);
        thrown.Which.CompensationFailures.Select(f => f.StepIndex).Should().Equal(1);
        thrown.Which.NotCompensatedStepIndexes.Should().BeEmpty();
        thrown.Which.IsFullyCompensated.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteAsync_StopOnFirstFailureMiddleCompensationFailsCase_ShouldStopAndReportNotCompensatedSteps()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var compensationError = new InvalidOperationException("compensation 1 failed");
        var pipeline = new PipelineBuilder<StudentModel>(CompensationStrategy.StopOnFirstFailure)
            .AddCompensableStep(addYear, subtractYear)
            .AddCompensableStep((m, _) => Task.FromResult(m), (_, _) => throw compensationError)
            .AddStep((_, _) => throw new InvalidOperationException("step 3 failed"))
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — the first step's compensation never ran
        student.Age.Should().Be(21);
        thrown.Which.CompensationFailures.Select(f => f.StepIndex).Should().Equal(1);
        thrown.Which.NotCompensatedStepIndexes.Should().Equal(0);
        thrown.Which.IsFullyCompensated.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteAsync_TwoCompensationsFailCase_ShouldReportBothInCompensationOrder()
    {
        // Arrange
        var error = new InvalidOperationException("step 3 failed");
        var firstCompensationError = new InvalidOperationException("compensation 0 failed");
        var secondCompensationError = new InvalidOperationException("compensation 1 failed");
        var pipeline = new PipelineBuilder<StudentModel>(CompensationStrategy.ContinueOnFailure)
            .AddCompensableStep((m, _) => Task.FromResult(m), (_, _) => throw firstCompensationError)
            .AddCompensableStep((m, _) => Task.FromResult(m), (_, _) => throw secondCompensationError)
            .AddStep((_, _) => throw error)
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 }))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert — compensations run in reverse, so step 1's failure comes first
        thrown.Which.CompensationFailures.Select(f => f.StepIndex).Should().Equal(1, 0);
        thrown.Which.Exceptions.Should().Equal(error, secondCompensationError, firstCompensationError);
    }

    [Test]
    public async Task ExecuteAsync_CompensationAndOnFailureThrowCase_ShouldKeepEveryExceptionInOrder()
    {
        // Arrange
        var error = new InvalidOperationException("step 2 failed");
        var compensationError = new InvalidOperationException("compensation failed");
        var onFailureError = new InvalidOperationException("onFailure failed");
        var pipeline = new PipelineBuilder<StudentModel>()
            .AddCompensableStep((m, _) => Task.FromResult(m), (_, _) => throw compensationError)
            .AddStep((_, _) => throw error)
            .OnFailure((_, _, _) => throw onFailureError)
            .Build();

        // Act
        var thrown = await pipeline
            .Invoking(p => p.ExecuteAsync(new StudentModel { Name = "Alice", Age = 20 }))
            .Should().ThrowExactlyAsync<PipelineException>();

        // Assert
        thrown.Which.FailureHandlerException.Should().BeEquivalentTo(onFailureError);
        thrown.Which.CompensationFailures.Should().ContainSingle();
        thrown.Which.Exceptions.Should().Equal(error, compensationError, onFailureError);
    }
}
