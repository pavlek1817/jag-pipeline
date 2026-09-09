using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Services;

public sealed class ProgramCatalogService(SampleDbContext dbContext) : IProgramCatalogService
{
    public async Task EnsureProgramExistsAsync(Guid programId, CancellationToken ct)
    {
        var exists = await dbContext.Programs.AnyAsync(p => p.Id == programId, ct);
        if (!exists)
        {
            throw new ProgramNotFoundException(programId);
        }
    }
}
