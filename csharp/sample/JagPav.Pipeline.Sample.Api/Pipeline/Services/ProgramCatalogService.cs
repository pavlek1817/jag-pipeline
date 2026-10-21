using JagPav.Pipeline.Sample.Api.Data;
using JagPav.Pipeline.Sample.Api.Exceptions;
using JagPav.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

namespace JagPav.Pipeline.Sample.Api.Pipeline.Services;

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
