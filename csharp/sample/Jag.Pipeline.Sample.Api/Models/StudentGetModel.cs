namespace Jag.Pipeline.Sample.Api.Models;

/// <summary>
/// The response shape returned by <c>POST /students/register</c> and <c>GET /students</c>.
/// </summary>
public sealed class StudentGetModel
{
    public string Id { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string IdentificationNumber { get; init; } = string.Empty;

    public string ProgramId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool IsCharged { get; init; }

    public DateTime? NextChargeDate { get; init; }
}
