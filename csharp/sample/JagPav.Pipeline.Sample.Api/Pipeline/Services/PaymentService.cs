using JagPav.Pipeline.Sample.Api.Pipeline.Contracts;
using System.Collections.Concurrent;

namespace JagPav.Pipeline.Sample.Api.Pipeline.Services;

/// <summary>
/// Simulates a payment gateway with a simple in-memory charge ledger, so a charge made by
/// step 5 of the registration pipeline can be reverted by its compensating action.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const decimal RegistrationFee = 100m;

    private readonly ConcurrentDictionary<string, (string StudentId, decimal Amount)> ledger = new ();

    public Task<TModel> ChargeAsync<TModel>(TModel model, CancellationToken ct)
        where TModel : IChargeModel
    {
        var chargeId = Guid.NewGuid().ToString();
        this.ledger[chargeId] = (model.Student!.Id, RegistrationFee);

        model.ChargeId = chargeId;
        return Task.FromResult(model);
    }

    public Task<TModel> RevertChargeAsync<TModel>(TModel model, CancellationToken ct)
        where TModel : IRevertChargeModel
    {
        this.ledger.TryRemove(model.ChargeId!, out _);

        return Task.FromResult(model);
    }
}
