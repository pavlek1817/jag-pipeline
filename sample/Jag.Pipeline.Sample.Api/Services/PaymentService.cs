using System.Collections.Concurrent;
using Jag.Pipeline.Sample.Api.Registration;

namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Simulates a payment gateway with a simple in-memory charge ledger, so a charge made by
/// step 5 of the registration pipeline can be reverted by its compensating action.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const decimal RegistrationFee = 100m;

    private readonly ConcurrentDictionary<Guid, (Guid StudentId, decimal Amount)> ledger = new ();

    public Task<StudentRegistrationContext> ChargeAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        var chargeId = Guid.NewGuid();
        this.ledger[chargeId] = (model.Student!.Id, RegistrationFee);

        model.ChargeId = chargeId;
        return Task.FromResult(model);
    }

    public Task<StudentRegistrationContext> RevertChargeAsync(StudentRegistrationContext model, CancellationToken ct)
    {
        this.ledger.TryRemove(model.ChargeId!.Value, out _);

        return Task.FromResult(model);
    }
}
