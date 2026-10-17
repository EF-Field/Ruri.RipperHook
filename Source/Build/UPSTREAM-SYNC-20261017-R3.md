# Main upstream follow-up, round 3 - 2026-10-17

Merged upstream `86c663a27d393fbf6fa79192e9b920795fdd6762` (seven commits
after `f96b19f`). This includes root-host/source-skeleton measurement, material
variant datasets and keyword reading, and the upstream IL2CPP metadata/listing
implementation. Native disassembly/export was not executed during validation.

The only backend merge conflict was the upstream-private `Source/Ruri.GameHook`
gitlink. Its removal is intentional in this fork and remains in place; our
`Source/Endfield-GameHook` is the sole optional private adapter. The upstream
public implementation files are retained without rewriting their new logic.

Both public and private snapshot-profile kernels compile with zero errors.
This is not a full AssetRipper/FModel checkout build. Existing trim/reflection
warnings remain. The candidate uses the accepted 138-file dependency closure
with only `Ruri.RipperHook.dll` replaced, and is tested against those exact
shipped dependency bytes, rather than silently shipping rebuilt dependencies.

Compiled private contracts: weapons/labels 114, coverage/channels 63, neutral
source targets 54. Real morph fields retain 89 avatars, 67 shader targets and
92 shape selectors. Blender 5.3 Alpha `73cfbda0a06d` real import/effect tests
pass 116 checks, including 231 bound controls and all 17 effect references.
Six real weapon/prop material imports, native toggle/offline reopen, synthetic
equipment, effect transaction/reopen and Weapon UI lifecycle checks pass.

The matching frontend includes upstream `a957321`, untouched generated shader
bytes, and six additional synthetic merge-seam checks. Detailed local logs and
temporary scenes are under `C:/temp/Ruri-upstream-20261017-round3`.

The matching verified runtime is recorded in the private repository and pinned
by the subsequent dependency checkpoint. Legacy runtime, installed add-ons,
preferences and user scenes are unchanged. This is not new manual visual
acceptance or coverage of every new game-specific dataset/retarget plugin.
Commits remain local until a separate explicit push request.
