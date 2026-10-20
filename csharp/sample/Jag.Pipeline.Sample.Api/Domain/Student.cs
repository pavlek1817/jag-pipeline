namespace Jag.Pipeline.Sample.Api.Domain;

/// <summary>
/// A student, as persisted in the sample database.
/// </summary>
public sealed class Student
{
    public string Id { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string IdentificationNumber { get; set; } = string.Empty;

    public string ProgramId { get; set; } = string.Empty;

    public StudentStatus Status { get; set; }

    public bool IsCharged { get; set; }

    public DateTime? NextChargeDate { get; set; }
}
