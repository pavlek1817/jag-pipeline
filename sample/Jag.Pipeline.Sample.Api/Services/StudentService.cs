using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Mapping;
using Jag.Pipeline.Sample.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Services;

public sealed class StudentService(SampleDbContext dbContext) : IStudentService
{
    public async Task EnsureIdentificationNumberIsUniqueAsync(string identificationNumber, CancellationToken ct)
    {
        var exists = await dbContext.Students.AnyAsync(s => s.IdentificationNumber == identificationNumber, ct);
        if (exists)
        {
            throw new DuplicateIdentificationNumberException(identificationNumber);
        }
    }

    public async Task<Student> AddPendingAsync(StudentAddModel request, CancellationToken ct)
    {
        var student = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IdentificationNumber = request.IdentificationNumber,
            ProgramId = request.ProgramId,
            Status = StudentStatus.Pending,
            IsCharged = false,
            NextChargeDate = null,
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(ct);
        return student;
    }

    public async Task RemovePendingAsync(Student student, CancellationToken ct)
    {
        dbContext.Students.Remove(student);
        await dbContext.SaveChangesAsync(ct);
    }

    public void CompleteRegistration(Student student, DateTime nextChargeDate)
    {
        student.Status = StudentStatus.Active;
        student.IsCharged = true;
        student.NextChargeDate = nextChargeDate;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StudentGetModel>> GetAllAsync(CancellationToken ct)
    {
        var students = await dbContext.Students.AsNoTracking().ToListAsync(ct);
        return students.Select(StudentMapper.ToGetModel).ToList();
    }
}
