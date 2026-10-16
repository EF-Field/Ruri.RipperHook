# Main / Statement upstream sync - 2026-10-17

## Inputs and scope

- Previous kernel: `2f3fc8623c7fe1b7ea2740ce36a3ed653a2bcaf2`.
- Merged upstream: `72fafab79be2d82d4eac5896df3c63e26e3aa936`
  (`5e3c13e`, `5539b22`, `d118401`, `72fafab`), with no textual conflicts.
- ShaderDecompiler: `c1fef798f7e8d9742133a956a74cc891fbe1a935`.
- Own private adapter: `1a0d74fa2d78165b0c2458f3aade9a634eb12c87`.
  The existing decoder remains `Endfield_1.6.0`, schema anchor `Endfield/1.5.3`.
- Importer upstream remains `fb77f982c97c6e3d72a998782c76c18cae39fabf`:
  already contained by the local frontend main; no new frontend commits to merge.

The upstream changes introduce raw-type-tree declarations, a single-structure
fallback when editor/release trees agree, and pass/stage-aware shader metadata
including a Compute slot. Upstream private decoder/type-tree changes are not
substituted for this fork's private adapter.

## Private module boundary

Removed the `Source/Ruri.GameHook` gitlink, its `.gitmodules` registration, and
its Unity/Unreal project inputs. It was uninitialized in this checkout.
`Source/Ruri.Hook` is the public in-tree hooking framework, not that submodule;
it remains required. `Source/Endfield-GameHook` remains the private adapter.
The TPK drift scanner now targets that adapter; default generated TPK output is
beside the tool, not inside the adapter's accepted runtime pack.

## Verification

Outputs/logs: `C:/temp/Ruri-upstream-20261017`, separate from the installed host
and from the accepted private `statement-runtime` snapshot.

- Release source builds: Ruri.Hook, Ruri.ACL, ShaderDecompiler, public kernel
  (`PureRelease=true`), private kernel (`UseEndfieldGameHook=true`): pass.
  Uses the compatible frozen AssetRipper dependency snapshot and local NuGet
  feed. Compiler/assembly-version warnings remain; no build errors.
- Private-module boundary tests: 4 pass.
- Public runtime audit: no private decoder or `RuriTypeTree.tpk`; upstream
  double-tree reflection target resolves; Compute slot set/get round-trip passes.
- VFS synthetic: versions 3/4/5/legacy pass, 7 malformed-input cases rejected;
  retired 1.4.4/1.5.3 decoder IDs remain absent.
- Existing-map live VFS: 418339 entries; 273595 expected bundle names covered;
  273611 CAB rows; roster 119; decoder 1.6.0 opens with schema 1.5.3. The existing
  map was read, not rebuilt. No hook drift/missing-baseline reports after the
  reviewed baseline addition below.
- Weapon backend synthetic/resolver tests: 25 + 38 checks pass.
- Frontend workflow tests: 39 pass (first-run, animation discovery, weapon UI,
  import completion, equipment contract, localized names).

The new four-argument TypeTreeObject.Create target lacked an unoptimized
baseline. Reviewed AssetRipper's pinned `813fed1597bac674def7256ce633a227e7b27497`
source and its factory history (`75bc44b`, `8bd1b53`): the factory constructs
DoubleTypeTreeObject with the same four arguments. Computing this one target's
fingerprint before installing hooks gives `36d5a3b810e20566`, identical to
upstream's optimized entry. Added only the missing configuration entry; did not
globally re-record baselines.

FModel and the TPK tool were not fully rebuilt. Their dependency source trees
are uninitialized here; project boundary XML/path contracts were checked.
No Blender GUI/visual acceptance, host installation, legacy-5.2 edits, accepted
snapshot replacement, push or publication is part of this sync.

## Shader reference availability

Found the tracked reference archive
[AllShader_1.6.3.7z](https://github.com/ShiyumeMeguri/FractalMiner/blob/main/Assets/Project/EndField/AllShader_1.6.3.7z),
18,722,474 bytes, Git blob `d946bfd615fe8026206d90e36972ee1c199ccae9`,
added by [fb1d596](https://github.com/ShiyumeMeguri/FractalMiner/commit/fb1d596760c4c20a054ca872bb5fcd4f219b267a)
at 2026-10-16 13:12:57 UTC (2026-10-17 00:12:57 Sydney).
Availability was checked through the GitHub tree and commit APIs. The archive
was not downloaded or inspected internally. This is not proof that the importer's
generated Blender shader libraries have been updated to that reference version.
