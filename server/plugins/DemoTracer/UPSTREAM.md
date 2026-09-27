# Source provenance

This component was extracted from
[`unicbm/demotracer`](https://github.com/unicbm/demotracer) commit
`012d978ecdce8a949306fdcec39e4dcd9bf7624e`:

- production source: `server/plugins/DemoTracer/`;
- complete managed regression suite: `server/plugins/DemoTracer.Tests/`;
- license: the original root `LICENSE` (AGPL-3.0-only).

`unicbm/demotracer` is the maintained source repository for this
module. Product builds consume this directory directly; there is one editable
source copy in the product working tree. The former component history is retained
under the product repository's `component-archive/css/` tags.
Runtime assembly, install directory, capability, and public API identifiers
are preserved by the extraction. Dependency path changes do not introduce a
new playback protocol or change native ABI requirements.

The component organizes that source under `src/DemoTracer/` by responsibility,
with tests under `tests/`, configuration inputs under `config/`, and maintenance
documentation under `docs/`. This layout change preserves the source contents,
namespaces, class names, and runtime identifiers.

ZstdSharp.Port/Zstandard attribution and license texts remain in
`THIRD_PARTY_NOTICES.md`. Econ data is generated and maintained in the pinned
common dependency, with source revisions embedded in that data. Public API and
native-provider test projects remain owned by their respective dependencies.
