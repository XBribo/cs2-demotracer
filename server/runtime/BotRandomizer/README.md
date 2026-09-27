# CS2 Bot Randomizer

Canonical source: [`unicbm/demotracer`](https://github.com/unicbm/demotracer/tree/main/server/runtime/BotRandomizer).

One provider supplies stable per-bot weapon skins, knives, gloves, stickers,
charms, agents and music kits, with optional externally owned cosmetic plans.
The same binaries support ordinary bot matches, Bot Improver Panel controls,
and DemoTracer playback. There is no separate replay Randomizer plugin.

## Install

Use the unified `BotRandomizer-v1.7.0.zip`. Extract its `addons` folder into the
server's `game/csgo` directory. Install **both** the plugin and the shared API:

```text
addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.dll
addons/counterstrikesharp/plugins/BotRandomizer/{three catalog JSON files}
addons/counterstrikesharp/shared/BotRandomizerApi/BotRandomizerApi.dll
```

Replace the existing standard BotRandomizer directory during an offline update;
do not run a renamed copy alongside it. Keep user configuration. The shared API
is required even when no consumer is installed. No DemoTracer assembly, demo
file, or native Randomizer shim is needed for ordinary randomization.

Requires .NET 10, CounterStrikeSharp managed API 1.0.371+, and the **KHook-enabled
CSS host** matched to Metamod 2.0 build 1469+ (plugin API 18). The exact host pins
are in `botrandomizer-package.v1.json`. The NuGet version alone does not prove
the host uses KHook. Configure the host for cosmetic plugins as required by
CounterStrikeSharp (`FollowCS2ServerGuidelines: false`). Windows x64 is the
maintained runtime target; Linux builds require independent signature and live
server validation before distribution as supported builds.

All native hooks use Metamod's shared KHook through CSS. KHook shares detours;
the provider's ownership plans prevent competing cosmetic writes. Two separate
cosmetic writers are still unsupported.

## Controls and coexistence

Server console / Bot Improver Panel:

```text
bot_randomizer github.com/ed0ard/CS2-Bot-Randomizer weapons 1
```

Categories: `weapons`, `knives`, `gloves`, `agents`, `music`, `stickers`, `charms`.
Values accept `1/0`, `on/off`, `yes/no`, `true/false`. Put desired defaults in
server configuration and execute them after plugin reload. These settings
affect random defaults, never valid external evidence, and do not reconstruct
items already created. `br_reroll [all|bot slot]` queues changes for a safe spawn;
active replay ownership remains authoritative.

Unclaimed bots randomize normally. A valid plan overrides only its specified
cosmetics; release/cancellation returns those decisions to current random
settings for subsequent safe lifecycle writes. Disconnect, map change and
unload invalidate stale ownership/callbacks. Human players and human-controlled
bot pawns are not replay targets. See [API.md](API.md).

## Source layout

- `BotRandomizer.cs` owns plugin lifetime, bot events and the shared restore
  schedule. Spawn and restore use one next-frame path with the same wearable
  retries; ownership changes invalidate pending work without rebuilding items.
- `BotRandomizerItems.cs` owns signature binding and the single GiveNamedItem
  pre/post hook pair. `WeaponItemViewStore` prepares item views; `CosmeticApplicator`
  handles live wearable/agent writes and their caches.
- `BotRandomizerApiFacade.cs` adapts the v3 contract to the host.
  `ReplayPlanValidator` validates and copies requests without engine access;
  `CosmeticWriteLeaseStore` manages ownership separately from item writes.
- `CosmeticRoller` and `RandomizerOptions` choose random defaults. Catalogs
  contain item data and weights; research provenance is checked by offline
  data tests rather than being a prerequisite for runtime loading.

The self-tests exercise the production validator, ownership transitions,
quality rules and deterministic random selections. Native write timing,
pickup and reload behavior still require game-server testing.

## Build and maintain

From `server/runtime/BotRandomizer`:

```powershell
pwsh -NoProfile -File tools/check.ps1
pwsh -NoProfile -File tools/package.ps1
pwsh -NoProfile -File tools/test-package.ps1
```

The scripts accept `-DotnetPath` and target .NET 10. The shared API source is
`BotRandomizerApi/`. `tools/test.ps1` delegates to `tools/check.ps1`.

Builds use `server/runtime/common` by default. Override it with the
`DtrCommonRoot` MSBuild property or `tools/package.ps1 -CommonRoot <path>`.
Common owns the econ index, Randomizer catalog, generator and host contract.
The project installs common's `econ/randomizer-catalog.json` as
`cosmetic_catalog.json`; update it through `common/tools/cs2-lib-data` and commit
affected consumers together. `charm_placements.json` stays provider-local.

Self-tests read the build output and validate catalog loading, ownership and
random selection. Game updates also require signature and live-server checks.
DemoTracer bundles the same public ZIP used by ordinary bot servers.

Original work: ed0ard, Misaka17032, unicbm and XBribo. See `LICENSE`,
`THIRD_PARTY_NOTICES.md`, and upstream history for attribution.
