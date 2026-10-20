using System.Diagnostics.CodeAnalysis;

namespace Jag.Pipeline.UnitTests;

[ExcludeFromCodeCoverage]
internal abstract class TestBase
{
    protected Fixture Fixture => new Fixture();
}
