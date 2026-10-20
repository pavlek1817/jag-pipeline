using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Pipeline.Contracts;
using Jag.Pipeline.Sample.Api.Registration;

namespace Jag.Pipeline.Sample.Api.Pipeline.Services;

/// <summary>
/// Backs steps 1, 2, 4, 6, 7 and 8 of the registration pipeline, plus the <c>GET /students</c> read.
/// Every pipeline step shares the same <see cref="StudentRegistrationPipelineModel"/> and
/// <see cref="CancellationToken"/> contract, so it can be plugged into the pipeline as a plain method reference.
/// </summary>
public interface IStudentService
{
    /// <summary>Step 1: sanity-checks the incoming request.</summary>
    /// <exception cref="Exceptions.StudentValidationException">The request failed validation.</exception>
    Task<StudentRegistrationPipelineModel> ValidateRequestAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    /// <summary>Step 2: verifies the identification number is not already registered.</summary>
    /// <exception cref="Exceptions.DuplicateIdentificationNumberException">Already registered.</exception>
    Task<IIdentificationNumber> EnsureIdentificationNumberIsUniqueAsync(IIdentificationNumber model, CancellationToken ct);

    /// <summary>Step 4: inserts a <see cref="Domain.StudentStatus.Pending"/> student and saves immediately.</summary>
    Task<StudentRegistrationPipelineModel> AddPendingAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    /// <summary>Compensates step 4: removes the pending student row that was inserted.</summary>
    Task<StudentRegistrationPipelineModel> RemovePendingAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    /// <summary>Step 6: activates the tracked student in memory, without saving yet.</summary>
    Task<StudentRegistrationPipelineModel> CompleteRegistrationAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    /// <summary>Step 7: persists whatever changes are currently tracked.</summary>
    Task<StudentRegistrationPipelineModel> SaveChangesAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    /// <summary>Step 8: shapes the response from the completed registration.</summary>
    Task<StudentRegistrationPipelineModel> ShapeResponseAsync(StudentRegistrationPipelineModel model, CancellationToken ct);

    Task<IReadOnlyList<StudentGetModel>> GetAllAsync(CancellationToken ct);
}
