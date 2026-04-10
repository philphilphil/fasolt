using Microsoft.AspNetCore.Mvc.Testing;

namespace Fasolt.Tests.Helpers;

/// <summary>
/// Shared xUnit collection for all integration tests that use WebApplicationFactory.
/// Tests in this collection run sequentially, which prevents concurrent MigrateAsync()
/// calls from racing on the shared Postgres database.
/// </summary>
[CollectionDefinition(Name)]
public class WebAppCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
    public const string Name = "WebApp";
}
