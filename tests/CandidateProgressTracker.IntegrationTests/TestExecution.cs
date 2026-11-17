using Xunit;

// Each collection provisions real SQL Express databases. Running collections
// serially avoids competing migrations/database teardown on the local instance.
// Coordinated concurrent requests inside individual tests remain concurrent.
[assembly: CollectionBehavior(MaxParallelThreads = 1)]
