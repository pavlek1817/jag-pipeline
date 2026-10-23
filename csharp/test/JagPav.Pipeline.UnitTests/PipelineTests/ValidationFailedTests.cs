using JagPav.Pipeline.Core;

namespace JagPav.Pipeline.UnitTests.PipelineTests;

internal sealed class ValidationFailedTests : PipelineTestBase
{
    [Test]
    public void AddCompensableStep_NullExecuteCase_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        new PipelineBuilder<StudentModel>()
            .Invoking(b => b.AddCompensableStep(null!, subtractYear))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("executeAsync");
    }

    [Test]
    public void AddStep_NullExecuteCase_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        new PipelineBuilder<StudentModel>()
            .Invoking(b => b.AddStep(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("executeAsync");
    }

    [Test]
    public void OnFailure_NullHandlerCase_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        new PipelineBuilder<StudentModel>()
            .Invoking(b => b.OnFailure(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("onFailureAsync");
    }

    [Test]
    public void Build_WithNoStepsCase_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        new PipelineBuilder<StudentModel>()
            .Invoking(b => b.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Build_CalledTwiceCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder<StudentModel>()
            .AddStep(addYear);
        builder.Build();

        // Act & Assert
        builder
            .Invoking(b => b.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void AddCompensableStep_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder<StudentModel>()
            .AddStep(addYear);
        builder.Build();

        // Act & Assert
        builder
            .Invoking(b => b.AddCompensableStep(addYear, subtractYear))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void AddStep_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder<StudentModel>()
            .AddStep(addYear);
        builder.Build();

        // Act & Assert
        builder
            .Invoking(b => b.AddStep(calculateNameLength))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void OnFailure_CalledTwiceCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder<StudentModel>()
            .AddStep(addYear)
            .OnFailure((_, _, _) => Task.CompletedTask);

        // Act & Assert
        builder
            .Invoking(b => b.OnFailure((_, _, _) => Task.CompletedTask))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void OnFailure_AfterBuildCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder<StudentModel>()
            .AddStep(addYear);
        builder.Build();

        // Act & Assert
        builder
            .Invoking(b => b.OnFailure((_, _, _) => Task.CompletedTask))
            .Should().Throw<InvalidOperationException>();
    }
}
