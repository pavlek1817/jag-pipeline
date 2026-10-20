using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Pipeline.Services;

public sealed class ProgramCatalogService(SampleDbContext dbContext) : IProgramCatalogService
{
    public async Task<StudentRegistrationPipelineModel> EnsureProgramExistsAsync(StudentRegistrationPipelineModel model, CancellationToken ct)
    {
        var programId = model.ProgramId;
        var exists = await dbContext.Programs.AnyAsync(p => p.Id == programId, ct);
        if (!exists)
        {
            throw new ProgramNotFoundException(programId);
        }

        return model;
    }
}
