namespace Jag.Pipeline.Sample.Api.Exceptions;

/// <summary>
/// Thrown by step 2 when a student with the same identification number is already registered.
/// </summary>
public sealed class DuplicateIdentificationNumberException(string identificationNumber)
    : Exception($"A student with identification number '{identificationNumber}' is already registered.")
{
    public string IdentificationNumber { get; } = identificationNumber;
}
