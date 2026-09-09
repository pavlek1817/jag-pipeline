using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Models;

namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Backs steps 2, 4, 6 and 7 of the registration pipeline, plus the <c>GET /students</c> read.
/// </summary>
public interface IStudentService
{
    /// <exception cref="Exceptions.DuplicateIdentificationNumberException">Already registered.</exception>
    Task EnsureIdentificationNumberIsUniqueAsync(string identificationNumber, CancellationToken ct);

    /// <summary>Step 4: inserts a <see cref="StudentStatus.Pending"/> student and saves immediately.</summary>
    Task<Student> AddPendingAsync(StudentAddModel request, CancellationToken ct);

    /// <summary>Compensates step 4: removes the pending student row that was inserted.</summary>
    Task RemovePendingAsync(Student student, CancellationToken ct);

    /// <summary>Step 6: activates the tracked student in memory, without saving yet.</summary>
    void CompleteRegistration(Student student, DateTime nextChargeDate);

    /// <summary>Step 7: persists whatever changes are currently tracked.</summary>
    Task SaveChangesAsync(CancellationToken ct);

    Task<IReadOnlyList<StudentGetModel>> GetAllAsync(CancellationToken ct);
}
