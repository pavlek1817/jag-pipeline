using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Models;

namespace Jag.Pipeline.Sample.Api.Registration;

/// <summary>
/// The model threaded through <see cref="Jag.Pipeline.Core.Pipeline{TModel}"/> for a single
/// registration: the incoming request, the student entity as it is built up across steps,
/// the charge id raised in step 5, and the response once step 8 has shaped it.
/// </summary>
public sealed class StudentRegistrationContext(StudentAddModel request)
{
    public StudentAddModel Request { get; } = request;

    public Student? Student { get; set; }

    public Guid? ChargeId { get; set; }

    public StudentGetModel? Response { get; set; }
}
