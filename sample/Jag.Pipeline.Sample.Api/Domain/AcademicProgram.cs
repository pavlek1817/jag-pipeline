namespace Jag.Pipeline.Sample.Api.Domain;

/// <summary>
/// An academic program a student can register into. Named "AcademicProgram" rather than
/// "Program" to avoid colliding with the top-level <c>Program</c> entry point class.
/// </summary>
public sealed class AcademicProgram
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
