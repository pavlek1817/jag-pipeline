namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Step 3 of registration: confirms the requested academic program exists.
/// </summary>
public interface IProgramCatalogService
{
    /// <exception cref="Exceptions.ProgramNotFoundException">The program does not exist.</exception>
    Task EnsureProgramExistsAsync(Guid programId, CancellationToken ct);
}
