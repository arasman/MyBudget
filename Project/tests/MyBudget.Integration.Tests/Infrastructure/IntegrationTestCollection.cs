namespace MyBudget.Integration.Tests.Infrastructure;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
    // Shared factory for all integration tests
}
