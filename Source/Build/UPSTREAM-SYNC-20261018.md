# Main upstream sync - 2026-10-18

Merged upstream `bfb28edcba4d4519f8d4047f79698a8ced79bc10` after `86c663a`.
Its only change is the upstream-private Ruri.GameHook gitlink (irradiance-volume
index handling). Resolved modify/delete by preserving this fork's deletion.
No upstream private source was fetched or substituted for our own adapter.
Ruri.Hook remains the public in-tree framework.

This merge changes no runtime C# source, project inputs or public dependency pins.
Own adapter/runtime stays `f53b8ddc1a8757472b41d40a8c42eb2cec6cf5f9`;
the shipped kernel hash is
`6476adbeeda277b2c50e237acbea93af63e31b95f80087cef45fe6546fa0875a`.
The previous locally committed synchronization and runtime checkpoint are
included in the dependency-ordered push, not replaced or re-recorded here.

Fresh verification against the shipped reader (UseCompiledReader=true): weapon
contracts 114 total checks, material coverage/channels 63, neutral source
projections 54. Private Python package/provenance gates: 20 tests; source-sync
gate: 30 inputs; exact Statement archive: 138 runtime files. Public private-module
boundary tests: 4. No full dependency rebuild or native disassembly was run.

Frontend upstream `46d972b` is tested separately: 65 host-free cases, 12 shader
stacks/228 group stamps, FogSimple material construction, sun-angle units and
medium runtime-data save exclusion/restoration. Logs and synthetic scenes stay
in `C:/temp/Ruri-upstream-20261018`. No host installation, legacy-5.2 or user
scene changes; these are not manual visual-parity checks.
