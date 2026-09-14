using Jag.Pipeline.Sample.Api.Domain;

namespace Jag.Pipeline.Sample.Api.Pipeline.Contracts;

public interface IChargeModel
{
    Student? Student { get; }

    string? ChargeId { get; set; }
}
