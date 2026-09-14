using Jag.Pipeline.Sample.Api.Registration;

namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Step 5 of registration: charges the registration fee, with a matching revert for compensation.
/// </summary>
public interface IPaymentService
{
    /// <summary>Charges the student, recording the raised charge id on the context for <see cref="RevertChargeAsync"/> to use on compensation.</summary>
    Task<StudentRegistrationContext> ChargeAsync(StudentRegistrationContext model, CancellationToken ct);

    Task<StudentRegistrationContext> RevertChargeAsync(StudentRegistrationContext model, CancellationToken ct);
}
