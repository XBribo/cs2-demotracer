# Shared DemoTracer infrastructure

This product module owns native hook/signature utilities, DemoTracerApi, source
field declarations, generated catalogs and their tools. Consumers reference
this directory directly. Changes and affected consumers land together.

Run `tools/check.ps1` with Node.js 22, .NET 10, CMake and the pinned Metamod
checkout. Product compatibility remains in
`shared/contracts/playback-contract.v1.json` at the repository root.

## Provenance and licenses

Originally extracted from DemoTracer commit
`012d978ecdce8a949306fdcec39e4dcd9bf7624e`; consolidated from
`c6b91341eeb0677dc7f84a7eba6d52180ab1ad8e`. Its original history is retained at
[component-archive/common/heads/main](https://github.com/unicbm/demotracer/tree/component-archive/common/heads/main).
First-party sources remain AGPL-3.0-only. Third-party headers and compatibility
sources retain their original notices and licenses.
