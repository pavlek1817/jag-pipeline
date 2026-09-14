using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Mapping;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Services;

public sealed class StudentService(SampleDbContext dbContext, TimeProvider timeProvider) : IStudentService
{
    public Task<StudentRegistrationContext> ValidateRequestAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var errors = new List<string>();
        var request = model.Request;

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors.Add("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors.Add("Last name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdentificationNumber))
        {
            errors.Add("Identification number is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            errors.Add("Program id is required.");
        }

        if (errors.Count > 0)
        {
            throw new StudentValidationException(errors);
        }

        return Task.FromResult(model);
    }

    public async Task<StudentRegistrationContext> EnsureIdentificationNumberIsUniqueAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var identificationNumber = model.Request.IdentificationNumber;
        var exists = await dbContext.Students.AnyAsync(s => s.IdentificationNumber == identificationNumber, ct);
        if (exists)
        {
            throw new DuplicateIdentificationNumberException(identificationNumber);
        }

        return model;
    }

    public async Task<StudentRegistrationContext> AddPendingAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var request = model.Request;
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

        model.Student = student;
        return model;
    }

    public async Task<StudentRegistrationContext> RemovePendingAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        dbContext.Students.Remove(model.Student!);
        await dbContext.SaveChangesAsync(ct);

        return model;
    }

    public Task<StudentRegistrationContext> CompleteRegistrationAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var student = model.Student!;
        student.Status = StudentStatus.Active;
        student.IsCharged = true;
        student.NextChargeDate = timeProvider.GetUtcNow().UtcDateTime.AddMonths(1);

        return Task.FromResult(model);
    }

    public async Task<StudentRegistrationContext> SaveChangesAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);

        return model;
    }

    public Task<StudentRegistrationContext> ShapeResponseAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        model.Response = StudentMapper.ToGetModel(model.Student!);

        return Task.FromResult(model);
    }

    public async Task<IReadOnlyList<StudentGetModel>> GetAllAsync(CancellationToken ct)
    {
        var students = await dbContext.Students.AsNoTracking().ToListAsync(ct);
        return students.Select(StudentMapper.ToGetModel).ToList();
    }
}
