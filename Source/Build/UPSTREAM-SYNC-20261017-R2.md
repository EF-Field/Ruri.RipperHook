# Main upstream follow-up - 2026-10-17

Merged upstream `f96b19f` (four commits after `72fafab`). The backend changes
are documentation rewrites and a ShaderDecompiler gitlink update to
`fab09257b0a665ba31bad3fafe3d813865ae5865`. That child update changes only
CLAUDE.md/README.md; no backend or child C# project/runtime source changed.

The fork remote had rewritten main history. Before alignment, local main's
tree was byte-identical to remote `e1a485a` (the replacement for `e15fa9f`).
Saved the previous local reference as `archive/pre-sync-20261017-round2`, then
aligned main to the remote lineage. Do not push that backup or merge the retired
history back into main. Publication uses a normal fast-forward push.

The sole textual conflict was CLAUDE.md. Took the new upstream rule layout and
restored the fork override: keep public Ruri.Hook, exclude upstream-private
Ruri.GameHook, use our own Endfield-GameHook, preserve main/5.3 isolation and
explicit/private-first push policy.

Verification: private-module boundary tests 4/4; no unmerged entries or diff
whitespace errors; all backend runtime sources/project/target inputs unchanged
from the previous verified snapshot. The previous runtime remains compatible;
no binary snapshot or installed host was replaced. This is not a new full
AssetRipper/FModel source build or Blender visual acceptance.

The matching frontend follow-up checks all four generated Blender shader
packages: 12 stacks and 224 group stamps passed on Blender 5.3 Alpha
`73cfbda0a06d`, with first-sun/absent-sun vertex capability checks and native
weapon channel/save/reopen tests. Logs: `C:/temp/Ruri-upstream-20261017-round2`.
