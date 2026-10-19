# Main upstream sync - 2026-10-19

Merged upstream `878683d4e5a7d5a3d374f458531e92928249a05b` (three commits
after `bfb28ed`) without textual conflicts. Changes reserve retarget sources
against JIT inlining before callers compile, scope dataset handles to their
CabTable identity, and run FastPng module type initializers during serialized
warmup. Upstream implementations were retained.

The private source pin is `feb603a92ab5acb4c18a4ea14bf0febd893218d3`, the current
KWZE.GameHook checkout used by this build through GameHookRoot. It composes all
registered adapters into the same kernel. The removed upstream-private gitlink
stays absent; no game-specific build selector or separate game DLL is introduced.

## Fresh verification

- Public and private snapshot-profile kernel builds: zero errors (229/318
  existing compiler/trim warnings). Ruri.Hook/ACL/ShaderDecompiler source builds
  pass. This is not a full AssetRipper/FModel dependency rebuild.
- AdapterContracts, both public/private dependency closures: 35 existing checks
  plus 10 new checks. New checks verify per-map handles/cache/search separation,
  late detours after caller warmup, cleanup/reactivation, and 32 concurrent PNG
  encodes. The public CI already runs this test assembly.
- Public compiled boundary audit: no private decoder, class or RuriTypeTree.tpk.
- Compiled private reader contracts: weapons 114, material coverage/channels 63,
  neutral projections 54; VFS 3/4/5/legacy and 7 malformed-input rejections pass.
- Public source/profile gates: 4 boundary and 9 composition tests. Private Python
  gates: 44 tests. Frontend package-CI scripts: 68 test cases, including new
  active-row search regressions.
- Blender 5.3 Alpha `73cfbda0a06d`, isolated background: one real weapon resource
  imports 3 meshes/8 textures, its 2 emission and 2 parallax channels are found,
  edits/restores stay source-preserving. No baseline drift, missing reservation
  or reservation-failure warning appears in that real import.
- Synthetic native controls/save and fresh offline reopen (without a reader)
  pass. No user scene or installed add-on was used.

## Runtime delivery boundary

Outputs/logs: `C:/temp/Ruri-upstream-20261019`. The separately packaged private
candidate uses the accepted dependency closure, replacing Ruri.RipperHook.dll
and Ruri.Hook.dll and adding the reviewed HookBaselines.json. Kernel SHA256:
`d6f4729033b458f42fad9234b0bcc39a85ac803a09e16e180ce775f250c8620b`.
The private repository's accepted statement-runtime bytes and provenance remain
unchanged; the source-sync gate still passes for that historical snapshot.
Candidate metadata records current source identities/hashes separately.

No new visual acceptance, full-game/effect coverage, native disassembly,
external retarget solver run, installation, or legacy-5.2 mutation is claimed.
