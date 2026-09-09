using Jag.Pipeline.Sample.Api.Domain;

namespace Jag.Pipeline.Sample.Api.Data;

/// <summary>
/// Seeds the in-memory database with a starter catalog of academic programs and a couple of
/// already-registered students, so <c>GET /students</c> has data to show out of the box.
/// </summary>
public static class DbSeeder
{
    public static void Seed(SampleDbContext dbContext)
    {
        if (dbContext.Programs.Any())
        {
            return;
        }

        var computerScience = new AcademicProgram { Id = Guid.NewGuid(), Code = "CS101", Name = "Computer Science" };
        var businessAdministration = new AcademicProgram { Id = Guid.NewGuid(), Code = "BA101", Name = "Business Administration" };
        var mechanicalEngineering = new AcademicProgram { Id = Guid.NewGuid(), Code = "ME101", Name = "Mechanical Engineering" };

        dbContext.Programs.AddRange(computerScience, businessAdministration, mechanicalEngineering);

        dbContext.Students.AddRange(
            new Student
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Kovač",
                IdentificationNumber = "STU-0001",
                ProgramId = computerScience.Id,
                Status = StudentStatus.Active,
                IsCharged = true,
                NextChargeDate = DateTime.UtcNow.AddMonths(1),
            },
            new Student
            {
                Id = Guid.NewGuid(),
                FirstName = "Marko",
                LastName = "Horvat",
                IdentificationNumber = "STU-0002",
                ProgramId = businessAdministration.Id,
                Status = StudentStatus.Active,
                IsCharged = true,
                NextChargeDate = DateTime.UtcNow.AddMonths(1),
            });

        dbContext.SaveChanges();
    }
}
