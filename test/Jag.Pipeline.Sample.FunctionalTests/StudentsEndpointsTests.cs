using System.Net;
using System.Net.Http.Json;
using Jag.Pipeline.Sample.Api.Data;
using Jag.Pipeline.Sample.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Jag.Pipeline.Sample.FunctionalTests;

internal sealed class StudentsEndpointsTests
{
    private SampleApiFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        this.factory = new SampleApiFactory();
        this.client = this.factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        this.client.Dispose();
        this.factory.Dispose();
    }

    // --- POST /students/register ---
    [Test]
    public async Task Register_ValidStudent_ReturnsCreatedWithActiveStatus()
    {
        // Arrange
        var request = new StudentAddModel
        {
            FirstName = "Ivana",
            LastName = "Novak",
            IdentificationNumber = $"STU-{Guid.NewGuid():N}",
            ProgramId = await this.getSeededProgramIdAsync(),
        };

        // Act
        var httpResponse = await this.client.PostAsJsonAsync("/students/register", request);

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var student = await httpResponse.Content.ReadFromJsonAsync<StudentGetModel>();
        student.Should().NotBeNull();
        student!.Status.Should().Be("Active");
        student.IsCharged.Should().BeTrue();
        student.NextChargeDate.Should().NotBeNull();
        student.NextChargeDate!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Register_DuplicateIdentificationNumberCase_ReturnsConflictAndDoesNotDuplicate()
    {
        // Arrange
        var request = new StudentAddModel
        {
            FirstName = "Ivana",
            LastName = "Novak",
            IdentificationNumber = $"STU-{Guid.NewGuid():N}",
            ProgramId = await this.getSeededProgramIdAsync(),
        };
        await this.client.PostAsJsonAsync("/students/register", request);

        // Act — register the same identification number again
        var httpResponse = await this.client.PostAsJsonAsync("/students/register", request);

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Register_UnknownProgramCase_ReturnsNotFound()
    {
        // Arrange
        var request = new StudentAddModel
        {
            FirstName = "Ivana",
            LastName = "Novak",
            IdentificationNumber = $"STU-{Guid.NewGuid():N}",
            ProgramId = Guid.NewGuid(),
        };

        // Act
        var httpResponse = await this.client.PostAsJsonAsync("/students/register", request);

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Register_InvalidRequestCase_ReturnsBadRequest()
    {
        // Arrange
        var request = new StudentAddModel();

        // Act
        var httpResponse = await this.client.PostAsJsonAsync("/students/register", request);

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- GET /students ---
    [Test]
    public async Task GetAll_ReturnsSeededAndNewlyRegisteredStudents()
    {
        // Arrange — 2 students are seeded at startup; register one more
        var request = new StudentAddModel
        {
            FirstName = "Ivana",
            LastName = "Novak",
            IdentificationNumber = $"STU-{Guid.NewGuid():N}",
            ProgramId = await this.getSeededProgramIdAsync(),
        };
        await this.client.PostAsJsonAsync("/students/register", request);

        // Act
        var students = await this.client.GetFromJsonAsync<List<StudentGetModel>>("/students");

        // Assert
        students.Should().NotBeNull();
        students!.Should().HaveCount(3);
        students.Should().Contain(s => s.IdentificationNumber == request.IdentificationNumber);
    }

    private async Task<Guid> getSeededProgramIdAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        return await Task.FromResult(dbContext.Programs.Select(p => p.Id).First());
    }
}
