# Public kernel and optional private source adapter

## Mainline policy (2026-10-13)

`main` is the Blender 5.3 / Statement kernel line. The frontend follows main and
pins a verified private adapter/runtime; `codex/blender-5.2` remains the separate
legacy compatibility-maintenance line. User acceptance covers manual model,
animation and checked-material appearance; effects are deferred, and this does
not certify all rendering or game-runtime features. Preserve
public/private source and artifact isolation.

```
KawakazeNotFound/RuriRipperImporter (public host)
  Ruri.RipperHook -> KawakazeNotFound/Ruri.RipperHook (public kernel)
    Source/Endfield-GameHook -> KawakazeNotFound/Endfield-GameHook (private)
```

The private repository is a pinned source submodule, not an independent loaded
assembly. Public history contains its URL and Git commit ID, not its files.
The upstream private `Source/Ruri.GameHook` gitlink and its Unity/Unreal build
inputs are removed. The default private gitlink remains `Source/Endfield-GameHook`;
an external private adapter checkout can be selected with `GameHookRoot`.
`Source/Ruri.Hook` is the public hook framework and remains in-tree.
Initialize only the submodules needed for the selected profile.

`Ruri.Tpk` scans the private adapter's `Endfield` directory for drift handling.
Its default output is `generated/RuriTypeTree.tpk` beside the tool executable,
not a published private runtime pack. Supply an explicit output path when needed.

## Tested snapshot build (.NET 10, Windows)

```powershell
git submodule update --init Source/Ruri.ShaderDecompiler
git submodule update --init Source/Endfield-GameHook
./Source/Build/build-kernel.ps1 -PrivateHook
```

The private profile requires private repository access. It compiles kernel, hook
utilities, ACL and ShaderDecompiler from source against the compatible AssetRipper
snapshot in the private module's `runtime`. Use `-PackageSource <local-nuget-feed>`
for offline restore, or the default NuGet service. Provision packages before
disconnecting. Outputs default to `Source/0Bins/private/bin`; point the host's
**Bin Dir** there after validation. Installed applications and the existing
`statement-runtime` snapshot are not overwritten.

Public-only compilation accepts an independently supplied dependency snapshot:

```powershell
./Source/Build/build-kernel.ps1 -DependencySnapshot <compatible-AssetRipper-DLL-directory>
```

Its default output is `Source/0Bins/public/bin`, distinct from private builds.
No private submodule is required for public compilation. Dependency binaries are
not part of this public repository; a snapshot input is not a public distribution
license. Keep private builds and private modules out of public release packages.

## Upstream source-build route

The upstream project references remain intact. With the necessary public source
submodules initialized, the kernel project can be built with
`-p:PureRelease=true`; the private profile instead uses
`-p:UseEndfieldGameHook=true` (without PureRelease). Full upstream dependency
rebuilds are separate from the tested snapshot route above.

Combining `PureRelease=true` and `UseEndfieldGameHook=true` is a build error.
Private sources and their type-tree resource are excluded from PureRelease.
Adapter-specific profile recognition and container decoding live in the private
module; this kernel exposes generic decoding/profile callbacks and mathematical
solvers. The adapter registers and clears callbacks with its session lifecycle.

Preserve upstream licenses. Repository privacy is an access boundary, not a
replacement for license obligations.

## Open product registration and external adapter roots

The install probe and decoder catalog already return string identities through
the existing host bridge. A private adapter can now declare
`[RipperHook("ProductName", "1.0.0", "2021.3.0f1")]` and
`[GameCapabilities("ProductName")]` without editing the kernel's legacy `GameType`
enum. Keep the exact product name stated by the install. Enum constructors and
method overloads remain supported. `RipperHookCommon.ApplyCapabilities()` uses
the current hook attribute's product and decoder version. Runtime loaded-game
registration/query/cleanup also accepts strings, scoped to the hook lifecycle.

New private source trees provide a generic `GameHook.targets` entry. Select one
using `UseGameHook=true` and `GameHookRoot`; adapter-specific selection stays
inside that private tree, which composes all registered games into the SAME
kernel. Runtime install identity selects a decoder; there is no per-game Hook
build. `UseEndfieldGameHook` and the default nested gitlink
remain compatibility defaults. Older pinned trees with only
`Integration/Statement.targets` still build through their old Statement entry.

```powershell
./Source/Build/build-kernel.ps1 -PrivateHook `
  -GameHookRoot 'PATH_TO_PRIVATE_ADAPTERS' `
  -DependencySnapshot 'COMPATIBLE_SNAPSHOT' -OutputDirectory 'C:/temp/adapter-build'
```

New private products can provide `[RipperInstallProbe]` for custom installation
identity and `[RipperEngineFile]` for transformed Unity identity-file reading.
No per-product host detection branch is needed. Game-specific UI/shader work is
separate. The optional TPK drift/stub generator retains its historical routing;
it is not required to register runtime product strings.

Additional schema packs can be embedded with unique logical names under
`Ruri.GameHook.TypeTrees.*.tpk`. The loader combines declared lineages using
pack-local original/renamed blob lookups and rejects duplicate lineage identities.
The legacy base resource/adjacent-file precedence is preserved. An explicit
`RURI_TYPE_TREE_TPK` remains authoritative and excludes embedded additions.
Capability module delegates, type-tree overrides and active schema versions have
scoped cleanup so changing the runtime decoder does not retain another game's state.

Public source-profile guards (no private files required):
`python -m unittest discover -s Source/Build -p "test_*.py"`.
`Source/Build/AdapterContracts` exercises synthetic string-product discovery,
the existing install/decoder bridge, capability version selection and scoped
cleanup against a separately built kernel (`-p:BackendDir=...`).
