using Xunit;

///
/// The tests build and destroy the database frequently. We could randomly generate the
/// database name for each test, but simpler to disable parallelism. These tests are fast
/// so far. By default all tests in this assembly are in the same collection and will run
/// serially.
///
[assembly: CollectionBehavior(DisableTestParallelization = true)]
