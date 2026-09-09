namespace Jag.Pipeline.Sample.Api.Services;

/// <summary>
/// Step 5 of registration: charges the registration fee, with a matching revert for compensation.
/// </summary>
public interface IPaymentService
{
    /// <returns>An id identifying the charge, to pass back into <see cref="RevertChargeAsync"/> on compensation.</returns>
    Task<Guid> ChargeAsync(Guid studentId, decimal amount, CancellationToken ct);

    Task RevertChargeAsync(Guid chargeId, CancellationToken ct);
}
