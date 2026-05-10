namespace Cms.Tests.Infrastructure;

using Xunit;

[CollectionDefinition(Name)]
public sealed class MySqlCollection : ICollectionFixture<MySqlContainerFixture>
{
    public const string Name = "MySqlCollection";
}
