using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Services;

public sealed class ProgramCatalogService(SampleDbContext dbContext) : IProgramCatalogService
{
    public async Task<StudentRegistrationContext> EnsureProgramExistsAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var programId = model.Request.ProgramId;
        var exists = await dbContext.Programs.AnyAsync(p => p.Id == programId, ct);
        if (!exists)
        {
            throw new ProgramNotFoundException(programId);
        }

        return model;
    }
}
