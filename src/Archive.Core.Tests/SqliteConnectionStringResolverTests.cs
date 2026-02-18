using Archive.Infrastructure.Configuration;

namespace Archive.Core.Tests;

public class SqliteConnectionStringResolverTests
{
    [Fact]
    public void Resolve_RelativeDataSource_IsAnchoredToBaseDirectory()
    {
        var resolver = new SqliteConnectionStringResolver("C:\\TestBase");

        var resolved = resolver.Resolve("Data Source=archive.db");

        Assert.Contains("Data Source=C:\\TestBase\\archive.db", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_AbsoluteDataSource_IsPreserved()
    {
        var resolver = new SqliteConnectionStringResolver("C:\\IgnoredBase");

        var resolved = resolver.Resolve("Data Source=C:\\Data\\archive.db");

        Assert.Contains("Data Source=C:\\Data\\archive.db", resolved, StringComparison.OrdinalIgnoreCase);
    }
}