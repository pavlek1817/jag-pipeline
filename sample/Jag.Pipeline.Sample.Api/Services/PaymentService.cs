using System.Collections.Concurrent;

namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Simulates a payment gateway with a simple in-memory charge ledger, so a charge made by
/// step 5 of the registration pipeline can be reverted by its compensating action.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly ConcurrentDictionary<Guid, (Guid StudentId, decimal Amount)> ledger = new ();

    public Task<Guid> ChargeAsync(Guid studentId, decimal amount, CancellationToken ct)
    {
        var chargeId = Guid.NewGuid();
        this.ledger[chargeId] = (studentId, amount);
        return Task.FromResult(chargeId);
    }

    public Task RevertChargeAsync(Guid chargeId, CancellationToken ct)
    {
        this.ledger.TryRemove(chargeId, out _);
        return Task.CompletedTask;
    }
}
