using System.Diagnostics.CodeAnalysis;

namespace JagPav.Pipeline.UnitTests;

[ExcludeFromCodeCoverage]
internal abstract class TestBase
{
    protected Fixture Fixture => new Fixture();
}
