using JagPav.Pipeline.Sample.Api.Registration;

namespace JagPav.Pipeline.Sample.Api.Pipeline.Services;

/// <summary>
/// Step 3 of registration: confirms the requested academic program exists.
/// </summary>
public interface IProgramCatalogService
{
    /// <exception cref="Exceptions.ProgramNotFoundException">The program does not exist.</exception>
    Task<StudentRegistrationPipelineModel> EnsureProgramExistsAsync(StudentRegistrationPipelineModel model, CancellationToken ct);
}
