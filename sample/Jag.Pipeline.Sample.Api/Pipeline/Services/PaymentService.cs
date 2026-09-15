using Jag.Pipeline.Sample.Api.Pipeline.Contracts;
using System.Collections.Concurrent;

namespace Jag.Pipeline.Sample.Api.Pipeline.Services;

/// <summary>
/// Simulates a payment gateway with a simple in-memory charge ledger, so a charge made by
/// step 5 of the registration pipeline can be reverted by its compensating action.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const decimal RegistrationFee = 100m;

    private readonly ConcurrentDictionary<string, (string StudentId, decimal Amount)> ledger = new ();

    public Task<IChargeModel> ChargeAsync(IChargeModel model, CancellationToken ct)
    {
        var chargeId = Guid.NewGuid().ToString();
        this.ledger[chargeId] = (model.Student!.Id, RegistrationFee);

        model.ChargeId = chargeId;
        return Task.FromResult(model);
    }

    public Task<IRevertChargeModel> RevertChargeAsync(IRevertChargeModel model, CancellationToken ct)
    {
        this.ledger.TryRemove(model.ChargeId!, out _);

        return Task.FromResult(model);
    }
}
