# gRPC client parity with the WCF client

The goal: the gRPC Viking client (`d:\src\git\Viking\Clients\WebAnnotationModel.gRPC`, branch `dev`) matches the WCF client (`d:\src\git\VikingLegacy\Clients\WebAnnotationModel`, branch `Legacy`, read-only) in features and in speed, so Viking can switch transports without a user noticing. Read by the scout and implementer on Parity and gRPC-client wakes.

## Parity matrix

`Clients/WebAnnotationModel.gRPC/PARITY.md`, created on first need and kept short. One row per WCF operation or store behavior:

| WCF member | gRPC equivalent | Status | Test | Benchmark |
|------------|-----------------|--------|------|-----------|

Status is `done`, `partial` (say what is missing), `missing`, or `n/a` (say why). Build the first version by walking the WCF stores (`Structure`, `StructureType`, `Location`, `LocationLink`, `StructureLink`, `PermittedStructureLink`, section-indexed and region loading, change notification and refresh, update and delete semantics, error and retry behavior) against the gRPC stores and the `AnnotationInterfaces` contracts. Behavior counts, not just method names: ordering, null and empty results, paging, timestamps for incremental refresh, auth and token handling, and cancellation.

A `missing` or `partial` row is a candidate. Implement it in the gRPC client (and in the server service if the RPC is absent), with the test below.

## Behavior parity tests

Run the same test body against both clients through the shared interfaces, with the same data in the throwaway database. A parity test fails if the two clients return different objects, order, or errors. Prefer property tests (see Test coverage in [gates.md](gates.md)); keep generators with the tests.

## Performance parity benchmarks

`Benchmarks/AnnotationParity`, created on first need: BenchmarkDotNet, one benchmark class per matrix area, the WCF operation as `Baseline = true` and the gRPC operation beside it, same database, same inputs, `[GlobalSetup]` seeds the data.

- Use realistic sizes: thousands of locations per section, full-section region loads, a large structure tree, bulk create/update/delete.
- Measure first-call and warm latency, bulk throughput, and allocated bytes, on loopback and, when available, over the real network.
- **gRPC is at parity when it is at most 5% slower than WCF on time and at most 20% higher on allocated bytes**, per matrix row. A row over the line is a candidate (batching, streaming, message size, serialization, channel reuse, compression, connection pooling, server query cost).
- A gRPC change that makes any benchmarked row worse than before is rejected.
- If the WCF baseline cannot run (it needs the old service host), record `baseline: unavailable` on the row, compare against the last recorded WCF number, and add a proposal to make it runnable.

## Reporting

Each report shows the matrix tally (`done / partial / missing`), rows over the performance line with their ratios, and rows that changed since the last report. See [reports.md](reports.md).
