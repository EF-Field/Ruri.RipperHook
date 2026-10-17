# EngineUbMetadata — Engine Uniform Buffer Member Layouts

This folder holds per-`(UBName, LayoutHash)` JSON files that name members
of UE engine-defined uniform buffers (`View`, `OpaqueBasePass`,
`LumenCardScene`, `VirtualShadowMap`, `LocalVF`, etc.) which cannot be
recovered from cooked shipping data on UE 5.1 (all SMs) or UE 5.4 SM5.

The seeds are generated from engine source by `Ruri.UEShaderTpkDumper`
(`Source/Ruri.UEShaderTpkDumper`); the closed-world limits they work around
are summarized in `Source/Ruri.ShaderDecompiler/CLAUDE.md`.

## Folder + filename convention

The dumper writes one folder per engine version (`4.26.2/`, `5.1.1/`, ...).
A folder named after a FModel `EGame` enum member is an override layer on top
of that, so the loader can auto-select it for the cooked game it's
decompiling. Example layout:

```
EngineUbMetadata/
    5.1.1/                                   # dumper output for UE 5.1.1
        View_13BB15AA_MetaData.json
        OpaqueBasePass_09280EDD_MetaData.json
        ...
    5.4.4/                                   # dumper output for UE 5.4.4
        View_<hash5.4>_MetaData.json
        ...
    GAME_InfinityNikki/                      # game-specific override (UE 5.4 fork)
        View_<custom_hash>_MetaData.json     # overrides the 5.4 base for this game only
    GAME_BlackMythWukong/                    # another game-specific override
        ...
```

**Loader lookup order** when decompiling a game:
1. The game-specific folder (e.g. `GAME_InfinityNikki/`) if present, takes precedence.
2. The base UE folder `GAME_UE{major}_{minor}/` for the same major.minor, if present.
3. Version folders named `{major}.{minor}` or `{major}.{minor}.*` (the dumper's output).
4. If none of the above exist, the whole directory (recursive scan), keyed only by `(UBName, LayoutHash)`.

An entry loaded from an earlier folder is not replaced by the same `(UBName, LayoutHash)` from a later one.

Using FModel's canonical EGame names (instead of free-form "UE5.1.1")
guarantees that game-specific custom engine forks (e.g. modded UEs that
ship with custom UB members) get matched on game ID alone — no string
matching gymnastics. Every folder is scanned recursively; `_ShaderType/`
subfolders hold ShaderType seeds and are skipped.

**Build copy semantics**: seed JSONs are copied to
`<exeDir>/EngineUbMetadata/<UEVersion>/` on build **only when the
destination doesn't already exist** (`Condition="!Exists(...)"`). Edits
to a deployed JSON, or hand-added JSONs at the deployed location, are
preserved across rebuilds — `git pull` + rebuild does NOT clobber your
local symbol overrides. To force a refresh, delete the deployed file
(or the whole deployed `EngineUbMetadata` folder).

### Filename convention

```
<UBName>_<LayoutHash:X8>_MetaData.json
```

Examples:
```
View_13BB15AA_MetaData.json
OpaqueBasePass_09280EDD_MetaData.json
LumenCardScene_19D918C8_MetaData.json
```

- `<UBName>` is the uniform-buffer name UE writes into the `'u'`
  optional-data block of every cooked shader (`View`, `Material`, etc.).
- `<LayoutHash:X8>` is `FRHIUniformBufferLayoutInitializer::ComputeHash()`
  rendered as 8 uppercase hex digits (no `0x` prefix; the JSON's own
  `LayoutHash` field keeps it). The decompiler
  reads this hash from `FBaseShaderResourceTable.ResourceTableLayoutHashes[i]`
  in each cooked shader's SRT chunk and looks up
  `(UBName, ResourceTableLayoutHashes[i])` here.

## Schema

```jsonc
{
  "Name": "View",
  "EngineVersion": "5.4.4",                                       // docs only
  "EngineSource": "Engine/.../SceneView.h:1016",                   // docs only
  "LayoutHash": "0x3F8A12C5",                                      // discriminator
  "HashFormula": "SizeAndStaticSlot",                              // which formula the dumper read, see below
  "BindingFlags": "Shader",                                        // docs only
  "UsageFlags": "",                                                // docs only
  "ConstantBuffer": {                                              // numeric members, Unity ConstantBufferParameter shape
    "Name": "View", "NameIndex": -1, "Size": 3776, "IsPartialCB": false,
    "MatrixParameters": [
      { "Name": "TranslatedWorldToClip", "NameIndex": -1, "Index": 0, "ArraySize": 0,
        "Type": "Float", "RowCount": 4, "ColumnCount": 4, "IsMatrix": true }
    ],
    "VectorParameters": [
      { "Name": "ViewTilePosition", "NameIndex": -1, "Index": 960, "ArraySize": 0,
        "Type": "Float", "RowCount": 3, "ColumnCount": 1, "IsMatrix": false }
    ],
    "StructParameters": []
  },
  "Textures": [
    { "Name": "VolumetricLightmapIndirectionTexture", "NameIndex": -1, "Index": 2,
      "SamplerIndex": -1, "MultiSampled": false, "Dim": 2 }
  ],
  "Samplers": [
    { "Name": "MaterialTextureBilinearWrapedSampler", "Sampler": 0, "BindPoint": 0 }
  ],
  "Buffers": [],
  "UAVs": [],
  "Resources": [                                                   // every resource-typed member, declaration order
    { "Index": 0, "Offset": 4088, "Name": "MaterialTextureBilinearWrapedSampler",
      "UbmtType": "UBMT_SAMPLER", "ShaderType": "SamplerState" }
  ]
}
```

### Numeric parameter types

`Type` is the scalar kind (`Float`, `Int`, `UInt`, `Bool`, `Half`, `Short`);
`RowCount` x `ColumnCount` gives the vector or matrix shape, and `IsMatrix`
puts the member in `MatrixParameters` instead of `VectorParameters`. `Index`
is the member's byte offset in the constant buffer and `ArraySize` 0 means it
is not an array. A `Bool` is cbuffer-encoded as int.

### Resource types

`UbmtType` should be one of the `EUniformBufferBaseType` enum names:
- `UBMT_TEXTURE` — bound to `register(t#)`
- `UBMT_SRV` — bound to `register(t#)`
- `UBMT_SAMPLER` — bound to `register(s#)`
- `UBMT_UAV` — bound to `register(u#)`
- `UBMT_RDG_TEXTURE` / `UBMT_RDG_BUFFER` / others — same as plain types
  for our purposes (the SRT decoder already knows the register class
  from the SRT map type).

`Index` MUST match `FRHIResourceTableEntry::Unpack(token).ResourceIndex`
as decoded from the cooked SRT for this UB. It is **not** the bind
register — it is the index into `FRHIUniformBufferLayoutInitializer.Resources[]`
that the engine source declares.

`ShaderType` is the HLSL type name (e.g. `SamplerState`). `Textures`,
`Samplers`, `Buffers` and `UAVs` may be left empty: the loader derives them
from `Resources`.

## How the hash is computed (so you can derive a file's name from UE source)

The hash function is byte-identical in UE 5.1.1 and UE 5.4.4 —
`Engine/Source/Runtime/RHI/Public/RHIUniformBufferLayoutInitializer.h:62-92`:

```cpp
void ComputeHash()
{
    uint32 TmpHash = ConstantBufferSize << 16
                   | static_cast<uint32>(BindingFlags) << 8
                   | static_cast<uint32>(StaticSlot != MAX_UNIFORM_BUFFER_STATIC_SLOTS);
    for (i = 0..Resources.Num()-1) TmpHash ^= Resources[i].MemberOffset;
    // unrolled XOR-fold of Resources[i].MemberType in 4 / 2 / 1 byte groups
    Hash = TmpHash;
}
```

Inputs:
- `ConstantBufferSize` (uint32)
- `BindingFlags` (uint8: `Shader` / `Static` / `StaticAndShader`)
- `(StaticSlot != INVALID)` bit
- For each entry in `Resources[]`: `MemberOffset`, `MemberType`

Older engines (4.x) put the buffer's static-slot index into the low byte of
the hash; the slot is assigned at engine start and no source states it, so
the dumper records which formula it read out of that engine's source
(`HashFormula`) and the loader ignores the low byte for `SizeAndStaticSlot`
seeds.

Member NAMES are NOT in the hash — same shape = same hash regardless of
naming. That's why one file can serve multiple engine versions when the
layout shape happens to match.

## Generating files

Run `Ruri.UEShaderTpkDumper` on an engine source tree:

```
dotnet run --project Source/Ruri.UEShaderTpkDumper -- --ue-root <dir> --filter "5\.4"
```

For every `BEGIN_*_STRUCT` block it walks each `SHADER_PARAMETER*` macro with
the engine's own alignment rules, collects every resource-typed member into
`Resources` in declaration order, computes the hash with the formula it read
out of that engine's source, and writes the JSON under
`EngineUbMetadata/<X.Y.Z>/`. Seeds are not written by hand.

When unsure of the hash, run the decompiler once with `RURI_SRT_DEBUG=1`
and grep the stderr for `[SRT] LayoutHashes ...` — the value next to the
UB name is the discriminator you need.

## What happens when no file matches

- The decompiler logs a `[EngineUbMetadata] no match for (View, 0x3F8A12C5)`
  warning in verbose mode.
- The shader still decompiles, but `<UBName>_1_m0[N]` arrays stay as
  anonymous flat-vec4 arrays (current behaviour pre-loader).
- Bind-slot resources fall back to typed placeholders
  (`View_SRV45` / `View_Sampler39` / etc.).

This is **never** a hard failure — missing metadata just degrades to the
prior closed-world ceiling, never blocks decompile.
