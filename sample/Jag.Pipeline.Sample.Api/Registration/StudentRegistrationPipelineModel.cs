using Jag.Pipeline.Sample.Api.Domain;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Pipeline.Contracts;

namespace Jag.Pipeline.Sample.Api.Registration;

/// <summary>
/// The model threaded through <see cref="Jag.Pipeline.Core.Pipeline{TModel}"/> for a single
/// registration: the incoming request, the student entity as it is built up across steps,
/// the charge id raised in step 5, and the response once step 8 has shaped it.
/// </summary>
public sealed class StudentRegistrationPipelineModel : IIdentificationNumber, IChargeModel, IRevertChargeModel
{
    public StudentRegistrationPipelineModel(string firstName, string lastName, string identificationNumber, string programId)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.IdentificationNumber = identificationNumber;
        this.ProgramId = programId;
    }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public string IdentificationNumber { get; init; }

    public string ProgramId { get; init; }

    public Student? Student { get; set; }

    public string? ChargeId { get; set; }

    public StudentGetModel? Response { get; set; }
}
