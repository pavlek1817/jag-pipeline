using JagPav.Pipeline.Sample.Api.Pipeline.Contracts;

namespace JagPav.Pipeline.Sample.Api.Pipeline.Services;

/// <summary>
/// Step 5 of registration: charges the registration fee, with a matching revert for compensation.
/// </summary>
public interface IPaymentService
{
    /// <summary>Charges the student, recording the raised charge id on the context for <see cref="RevertChargeAsync"/> to use on compensation.</summary>
    /// <typeparam name="TModel">The pipeline model; only its <see cref="IChargeModel"/> members are used.</typeparam>
    Task<TModel> ChargeAsync<TModel>(TModel model, CancellationToken ct)
        where TModel : IChargeModel;

    /// <summary>
    /// Reverts a previously charged registration fee, using the charge id recorded on the context.
    /// </summary>
    /// <typeparam name="TModel">The pipeline model; only its <see cref="IRevertChargeModel"/> members are used.</typeparam>
    /// <param name="model">The model containing the charge id to revert.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    Task<TModel> RevertChargeAsync<TModel>(TModel model, CancellationToken ct)
        where TModel : IRevertChargeModel;
}
