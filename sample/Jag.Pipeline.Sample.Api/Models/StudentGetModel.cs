namespace Jag.Pipeline.Sample.Api.Models;

/// <summary>
/// The response shape returned by <c>POST /students/register</c> and <c>GET /students</c>.
/// </summary>
public sealed class StudentGetModel
{
    public string Id { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string IdentificationNumber { get; set; } = string.Empty;

    public string ProgramId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsCharged { get; set; }

    public DateTime? NextChargeDate { get; set; }
}
