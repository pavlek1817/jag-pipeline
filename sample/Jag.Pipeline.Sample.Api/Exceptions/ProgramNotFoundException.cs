namespace Jag.Pipeline.Sample.Api.Exceptions;

/// <summary>
/// Thrown by step 3 when the requested academic program does not exist.
/// </summary>
public sealed class ProgramNotFoundException(string programId)
    : Exception($"No academic program was found with id '{programId}'.")
{
    public string ProgramId { get; } = programId;
}
