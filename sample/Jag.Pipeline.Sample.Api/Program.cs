using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Pipeline.Services;
using Jag.Pipeline.Sample.Api.Registration;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<SampleDbContext>(options => options.UseInMemoryDatabase("JagPipelineSample"));

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
    catch (StudentValidationException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ex.Errors.ToArray() });
    }
    catch (DuplicateIdentificationNumberException ex)
    {
        return Results.Conflict(new { ex.Message });
    }
    catch (ProgramNotFoundException ex)
    {
        return Results.NotFound(new { ex.Message });
    }
})
.WithName("RegisterStudent");

app.MapGet("/students", (IStudentService studentService, CancellationToken ct) => studentService.GetAllAsync(ct))
.WithName("GetStudents");

app.Run();

namespace Jag.Pipeline.Sample.Api
{
    /// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can target it from tests.</summary>
    public partial class Program;
}
