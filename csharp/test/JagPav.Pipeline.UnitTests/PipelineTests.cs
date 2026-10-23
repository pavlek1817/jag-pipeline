using JagPav.Pipeline.Core;

namespace JagPav.Pipeline.UnitTests;

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
        var pipeline = new Pipeline<StudentModel>()
            .AddCompensableStep(addYearToAnyAged, subtractYear)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(student);

        // Assert
        result.Age.Should().Be(21);
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
            .Build();

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
            .Build();

        // Act
        await pipeline
            .Invoking(p => p.ExecuteAsync(student))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — first step never completed, nothing compensated
        student.Age.Should().Be(20);
        student.NameLength.Should().Be(0);
    }

    // --- OnFailure ---
    [Test]
    public async Task ExecuteAsync_StepFailsWithOnFailureCase_ShouldRunOnFailureAfterCompensationAndRethrow()
    {
        // Arrange
        var student = new StudentModel { Name = "Alice", Age = 20 };
        var error = new InvalidOperationException("step 2 failed");
        var calls = new List<string>();
        Exception? receivedError = null;
        var pipeline = new Pipeline<StudentModel>()
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
        var pipeline = new Pipeline<StudentModel>()
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

    [Test]
    public async Task ExecuteAsync_OnFailureThrowsCase_ShouldRethrowOriginalAndKeepOnFailureError()
    {
        // Arrange
        var error = new InvalidOperationException("step failed");
        var onFailureError = new InvalidOperationException("onFailure failed");
        var pipeline = new Pipeline<StudentModel>()
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
        var pipeline = new Pipeline<StudentModel>()
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

    // --- Guard clauses ---
    [Test]
    public void Build_WithNoStepsCase_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        new Pipeline<StudentModel>()
            .Invoking(p => p.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Build_CalledTwiceCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .Build();

        // Act & Assert
        pipeline
            .Invoking(p => p.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteAsync_BeforeBuildCase_ShouldThrowInvalidOperationException()
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
    public void AddCompensableStep_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .Build();

        // Act & Assert
        pipeline
            .Invoking(p => p.AddCompensableStep(addYear, subtractYear))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void AddStep_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .Build();

        // Act & Assert
        pipeline
            .Invoking(p => p.AddStep(calculateNameLength))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void OnFailure_CalledTwiceCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .OnFailure((_, _, _) => Task.CompletedTask);

        // Act & Assert
        pipeline
            .Invoking(p => p.OnFailure((_, _, _) => Task.CompletedTask))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void OnFailure_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new Pipeline<StudentModel>()
            .AddStep(addYear)
            .Build();

        // Act & Assert
        pipeline
            .Invoking(p => p.OnFailure((_, _, _) => Task.CompletedTask))
            .Should().Throw<InvalidOperationException>();
    }

    private static Task<StudentModel> addYear(StudentModel model, CancellationToken ct)
    {
        model.Age++;
        return Task.FromResult(model);
    }

    private static Task<StudentModel> subtractYear(StudentModel model, CancellationToken ct)
    {
        model.Age--;
        return Task.FromResult(model);
    }

    private static Task<TModel> addYearToAnyAged<TModel>(TModel model, CancellationToken ct)
        where TModel : IAged
    {
        model.Age++;
        return Task.FromResult(model);
    }

    private static Task<StudentModel> calculateNameLength(StudentModel model, CancellationToken ct)
    {
        model.NameLength = model.Name.Length;
        return Task.FromResult(model);
    }

    private interface IAged
    {
        int Age { get; set; }
    }

    private sealed class StudentModel : IAged
    {
        public int Age { get; set; }

        internal string Name { get; init; } = string.Empty;

        internal int NameLength { get; set; }
    }
}
