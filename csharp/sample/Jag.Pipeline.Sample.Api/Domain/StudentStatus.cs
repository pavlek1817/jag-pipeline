namespace Jag.Pipeline.Sample.Api.Domain;

/// <summary>
/// The lifecycle status of a <see cref="Student"/> registration.
/// </summary>
public enum StudentStatus
{
    /// <summary>The student row has been inserted but registration has not completed yet.</summary>
    Pending,

    /// <summary>Registration completed: the student has been charged and is active.</summary>
    Active,
}
