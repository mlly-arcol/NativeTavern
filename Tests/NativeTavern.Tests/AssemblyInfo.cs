using Xunit;

// Microsoft.Data.Sqlite can only release a temp database file for deletion by clearing the connection
// pool process-wide, so a test that cleans up after itself would otherwise cut the file out from under a
// test running next door. The suite is small enough to run in one line.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
