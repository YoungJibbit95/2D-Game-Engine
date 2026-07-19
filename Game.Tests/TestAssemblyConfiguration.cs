using Xunit;

// This suite contains exact current-thread allocation and wall-clock micro-gates.
// Running those gates beside unrelated tests makes their evidence scheduler-dependent.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
