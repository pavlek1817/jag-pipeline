namespace Jag.Pipeline.Sample.Api.Models;

/// <summary>
/// The request body for <c>POST /students/register</c>.
/// </summary>
public sealed class StudentAddModel
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string IdentificationNumber { get; set; } = string.Empty;

    public Guid ProgramId { get; set; }
}
