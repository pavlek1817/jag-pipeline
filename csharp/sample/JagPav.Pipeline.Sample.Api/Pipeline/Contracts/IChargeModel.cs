using JagPav.Pipeline.Sample.Api.Domain;

namespace JagPav.Pipeline.Sample.Api.Pipeline.Contracts;

public interface IChargeModel
{
    Student? Student { get; }

    string? ChargeId { get; set; }
}
