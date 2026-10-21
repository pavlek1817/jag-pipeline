using JagPav.Pipeline.Sample.Api.Domain;
using JagPav.Pipeline.Sample.Api.Models;

namespace JagPav.Pipeline.Sample.Api.Mapping;

/// <summary>
/// Shapes a persisted <see cref="Student"/> into the API's <see cref="StudentGetModel"/> response.
/// </summary>
public static class StudentMapper
{
    public static StudentGetModel ToGetModel(Student student)
    {
        return new StudentGetModel
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            IdentificationNumber = student.IdentificationNumber,
            ProgramId = student.ProgramId,
            Status = student.Status.ToString(),
            IsCharged = student.IsCharged,
            NextChargeDate = student.NextChargeDate,
        };
    }
}
