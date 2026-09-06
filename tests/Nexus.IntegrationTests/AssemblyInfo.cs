using Xunit;

// Testcontainers + TestServer are resource-heavy integration fixtures. Keep
// the integration classes sequential in CI as well as locally so Docker,
// PostgreSQL and host teardown cannot race across parallel test collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
