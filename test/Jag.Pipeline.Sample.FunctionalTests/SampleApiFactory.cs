using Jag.Pipeline.Sample.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jag.Pipeline.Sample.FunctionalTests;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the sample API's EF Core
/// InMemory database with a uniquely-named one per instance, so each test gets its own
/// isolated, freshly-seeded database instead of sharing state with other tests.
/// </summary>
internal sealed class SampleApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SampleDbContext>>();
            services.AddDbContext<SampleDbContext>(options => options.UseInMemoryDatabase(this.databaseName));
        });
    }
}
