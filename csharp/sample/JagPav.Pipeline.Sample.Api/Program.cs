using JagPav.Pipeline.Exceptions;
using JagPav.Pipeline.Sample.Api.Data;
using JagPav.Pipeline.Sample.Api.Exceptions;
using JagPav.Pipeline.Sample.Api.Models;
using JagPav.Pipeline.Sample.Api.Pipeline.Services;
using JagPav.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<SampleDbContext>(options => options.UseInMemoryDatabase("JagPavPipelineSample"));

builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IProgramCatalogService, ProgramCatalogService>();
builder.Services.AddSingleton<IPaymentService, PaymentService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<StudentRegistrationHandler>();

var app = builder.Build();

using (var seedScope = app.Services.CreateScope())
{
    DbSeeder.Seed(seedScope.ServiceProvider.GetRequiredService<SampleDbContext>());
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/students/register", async (StudentAddModel request, StudentRegistrationHandler handler, CancellationToken ct) =>
{
    try
    {
        var response = await handler.RegisterAsync(request, ct);
        return Results.Created($"/students/{response.Id}", response);
    }
    catch (PipelineException ex) when (!ex.IsFullyCompensated)
    {
        // The rollback is incomplete, so the system may be left inconsistent whatever the original cause was.
        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (PipelineException ex) when (ex.OriginalException is StudentValidationException validation)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = validation.Errors.ToArray() });
    }
    catch (PipelineException ex) when (ex.OriginalException is DuplicateIdentificationNumberException duplicate)
    {
        return Results.Conflict(new { duplicate.Message });
    }
    catch (PipelineException ex) when (ex.OriginalException is ProgramNotFoundException notFound)
    {
        return Results.NotFound(new { notFound.Message });
    }
})
.WithName("RegisterStudent");

app.MapGet("/students", (IStudentService studentService, CancellationToken ct) => studentService.GetAllAsync(ct))
.WithName("GetStudents");

app.Run();

namespace JagPav.Pipeline.Sample.Api
{
    /// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can target it from tests.</summary>
    public partial class Program;
}
