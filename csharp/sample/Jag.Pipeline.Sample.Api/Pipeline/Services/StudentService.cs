using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Mapping;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Pipeline.Contracts;
using Jag.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Pipeline.Services;

public sealed class StudentService(SampleDbContext dbContext, TimeProvider timeProvider) : IStudentService
{
    public Task<StudentRegistrationPipelineModel> ValidateRequestAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.FirstName))
        {
            errors.Add("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.LastName))
        {
            errors.Add("Last name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.IdentificationNumber))
        {
            errors.Add("Identification number is required.");
        }

        if (string.IsNullOrWhiteSpace(model.ProgramId))
        {
            errors.Add("Program id is required.");
        }

        if (errors.Count > 0)
        {
            throw new StudentValidationException(errors);
        }

        return Task.FromResult(model);
    }

    public async Task<IIdentificationNumber> EnsureIdentificationNumberIsUniqueAsync(IIdentificationNumber model, CancellationToken ct)
    {
        var identificationNumber = model.IdentificationNumber;
        var exists = await dbContext.Students.AnyAsync(s => s.IdentificationNumber == identificationNumber, ct);
        if (exists)
        {
            throw new DuplicateIdentificationNumberException(identificationNumber);
        }

        return model;
    }

    public async Task<StudentRegistrationPipelineModel> AddPendingAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        var student = new Student
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = model.FirstName,
            LastName = model.LastName,
            IdentificationNumber = model.IdentificationNumber,
            ProgramId = model.ProgramId,
            Status = StudentStatus.Pending,
            IsCharged = false,
            NextChargeDate = null,
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(ct);

        model.Student = student;
        return model;
    }

    public async Task<StudentRegistrationPipelineModel> RemovePendingAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        dbContext.Students.Remove(model.Student!);
        await dbContext.SaveChangesAsync(ct);

        return model;
    }

    public Task<StudentRegistrationPipelineModel> CompleteRegistrationAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        var student = model.Student!;
        student.Status = StudentStatus.Active;
        student.IsCharged = true;
        student.NextChargeDate = timeProvider.GetUtcNow().UtcDateTime.AddMonths(1);

        return Task.FromResult(model);
    }

    public async Task<StudentRegistrationPipelineModel> SaveChangesAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);

        return model;
    }

    public Task<StudentRegistrationPipelineModel> ShapeResponseAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
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
