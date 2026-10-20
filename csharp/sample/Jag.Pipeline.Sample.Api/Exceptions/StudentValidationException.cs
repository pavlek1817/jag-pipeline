namespace Jag.Pipeline.Sample.Api.Exceptions;

/// <summary>
/// Thrown by the sanity-check step (step 1) when a <see cref="Models.StudentAddModel"/> fails validation.
/// </summary>
public sealed class StudentValidationException(IReadOnlyList<string> errors)
    : Exception("Student registration request failed validation: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
