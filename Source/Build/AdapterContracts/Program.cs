using Ruri.Hook.Attributes;
using Ruri.Hook.Core;
using Ruri.RipperHook;
using Ruri.RipperHook.Attributes;
using Ruri.RipperHook.BlenderBridge;
using Ruri.RipperHook.BlenderBridge.Install;
using Ruri.RipperHook.Core.Capabilities;
using Ruri.RipperHook.Core.TypeTree;
using Ruri.Hook;
using Ruri.Hook.Config;
using AssetRipper.SourceGenerated;

if (args.Length == 2 && args[0] == "--write-schemas")
{
    SchemaFixtures.Write(args[1]);
    return;
}
if (args.Length > 0 && args[0] == "--schemas")
{
    SchemaFixtures.Verify(args.Skip(1).ToArray());
    return;
}

int checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    checks++;
}
void Invalid(Action action, string label)
{
    try { action(); }
    catch (ArgumentException) { checks++; return; }
    throw new Exception(label);
}

// Existing enum constructors/getters remain callable by older source and binaries.
var old = new RipperHookAttribute(GameType.Endfield, "1.6.0", "2021.3.34f5");
Check(old.GameType == GameType.Endfield && old.GameName == "Endfield", "Enum decoder compatibility");
var oldCaps = new GameCapabilitiesAttribute(GameType.Endfield);
Check(oldCaps.GameType == GameType.Endfield && oldCaps.GameName == "Endfield", "Enum capability compatibility");
var fresh = new RipperHookAttribute("FixtureProduct", "1.0.0", "2021.3.0f1");
Check(fresh.GameName == "FixtureProduct" && fresh.GameType == GameType.Unknown, "Open product identity");
Check(new RipperHookAttribute("Product With Spaces ").GameName == "Product With Spaces ", "Do not rewrite product names");
Check(new GameCapabilitiesAttribute("FixtureProduct").GameName == "FixtureProduct", "Open capability identity");
Check(new RipperHookAttribute("Endfield").GameType == GameType.Endfield, "Known string compatibility getter");
Invalid(() => new RipperHookAttribute(" "), "Empty product accepted");
Invalid(() => new GameCapabilitiesAttribute(""), "Empty capability product accepted");

// Use the same catalog family and bridge calls as the Python host, with no frontend changes.
Bootstrap.InstallAssemblyResolver();
Check(HookCatalog.DecoderById("FixtureProduct_1.0.0")?.Type == typeof(FixtureHookV1), "Attribute discovery");
Check(HookCatalog.Resolve("FixtureProduct", "1.5.0", "2021.3.0f1")?.Type == typeof(FixtureHookV1), "Version floor");
Check(HookCatalog.Resolve("FixtureProduct", "2.1.0", "2021.3.0f1")?.Type == typeof(FixtureHookV2), "Newest compatible decoder");
Check(HookCatalog.Resolve("FixtureProduct", "0.5.0", "2021.3.0f1") is null, "Future decoder rejection");
Check(HookCatalog.Resolve("FixtureProduct", "2.1.0", "other-engine-version") is null, "Engine mismatch");
Check(RipperBlenderBridge.ListDecoders().Contains("FixtureProduct"), "Bridge publishes product");
string install = Path.Combine(Path.GetTempPath(), "gamehook-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(install);
try
{
    File.WriteAllText(Path.Combine(install, ".adapter-contract"), "fixture");
    string[] identity = RipperBlenderBridge.ReadInstall(install);
    Check(identity.Length == 7 && identity[2] == "FixtureProduct" && identity[6] == "1", "Backend-owned install probe");
    Check(RipperBlenderBridge.ResolveDecoder(identity[2], identity[3], identity[4], identity[5]) == "FixtureProduct_1.0.0",
        "Identity-to-decoder through existing host bridge");
}
finally { Directory.Delete(install, recursive: true); }

// Optional integration checks: every named adapter must coexist with the existing one.
foreach (string expectedProduct in args)
{
    var added = HookCatalog.VersionsOf(expectedProduct);
    var existing = HookCatalog.VersionsOf(nameof(GameType.Endfield));
    Check(added.Count > 0, "Additional adapter published its decoder");
    Check(existing.Count > 0 && existing[0].Type.Assembly == added[0].Type.Assembly,
        "Existing and additional adapters reside in the SAME Hook assembly");
    string folder = Path.Combine(Path.GetTempPath(), "new-game-contract-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        File.WriteAllText(Path.Combine(folder, ".new-game-contract-" + expectedProduct), "fixture");
        string[] identity = RipperBlenderBridge.ReadInstall(folder);
        Check(identity.Length == 7 && identity[2] == expectedProduct, "Build-selected adapter identifies installation");
        Check(RipperBlenderBridge.ResolveDecoder(identity[2], identity[3], identity[4], identity[5]) == expectedProduct + "_1.0.0",
            "Build-selected adapter reaches the unchanged host bridge");
    }
    finally { Directory.Delete(folder, recursive: true); }
}

// Capability slots use string identity: two products absent from the enum must not collide.
var registry = new HookRegistry();
CapabilityResolver.Apply("FixtureProduct", "1.0.0", registry);
Check(FixtureModule.Value?.Invoke() == "v1", "String capability v1");
CapabilityResolver.Apply("fixtureproduct", "2.0.0", registry);
Check(FixtureModule.Value?.Invoke() == "v2", "String capability v2 and catalog-compatible case matching");
CapabilityResolver.Apply("FixtureOtherProduct", "1.0.0", registry);
Check(FixtureModule.Value?.Invoke() == "other", "Distinct unknown enum products stay distinct");
new FixtureHookV1().ApplyFixtureCapabilities();
Check(FixtureModule.Value?.Invoke() == "v1", "Parameterless adapter capability registration");
CapabilityResolver.Apply(GameType.Unknown, "1.0.0", registry); // Old overload remains callable.

HookManager.RunInScope("fixture-one", () =>
{
    RuriRuntimeHook.RegisterLoadedGameHook("FixtureProduct");
    RuriRuntimeHook.RegisterLoadedGameHook("FixtureProduct");
    RuriRuntimeHook.RegisterLoadedGameHook(GameType.Endfield);
});
HookManager.RunInScope("fixture-two", () => RuriRuntimeHook.RegisterLoadedGameHook("fixtureproduct"));
Check(RuriRuntimeHook.IsGameLoaded("FixtureProduct"), "String loaded query");
Check(RuriRuntimeHook.IsGameLoaded(GameType.Endfield) && RuriRuntimeHook.IsGameLoaded("Endfield"), "Enum/string loaded interop");
HookManager.DisposeScope("fixture-one");
Check(RuriRuntimeHook.IsGameLoaded("FixtureProduct"), "Other scope still owns same product");
Check(!RuriRuntimeHook.IsGameLoaded(GameType.Endfield), "Scoped enum cleanup");
HookManager.DisposeScope("fixture-two");
Check(!RuriRuntimeHook.IsGameLoaded("FixtureProduct"), "Final scope cleanup");
RuriRuntimeHook.RegisterLoadedGameHook(GameType.Unknown);
Check(!RuriRuntimeHook.IsGameLoaded(GameType.Unknown), "Unknown enum no-op");
Invalid(() => RuriRuntimeHook.RegisterLoadedGameHook(" "), "Empty loaded product accepted");

// Exercise the real decoder lifecycle without native detours. B deliberately has no capabilities.
FixtureModule.Value = null;
var state = new HookConfig();
void Select(string? decoder)
{
    state.EnabledHooks.Clear();
    if (decoder is not null) state.EnabledHooks.Add(decoder);
    RuriHook.ApplyHooks(state);
}
var gate = typeof(TypeTreeOverrides).GetMethod("FindGate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
Select("SwitchProductA_1.0.0");
Check(FixtureModule.Value?.Invoke() == "switch-a" && RuriRuntimeHook.IsGameLoaded("SwitchProductA"), "A active");
Check(!TypeTreeDatabase.ActiveVersion.IsEmpty && gate.Invoke(null, [ClassIDType.Mesh, "fixture"]) is not null, "A schema state active");
Select("SwitchProductB_1.0.0");
Check(FixtureModule.Value is null && !RuriRuntimeHook.IsGameLoaded("SwitchProductA"), "A callbacks removed before B");
Check(TypeTreeDatabase.ActiveVersion.IsEmpty && gate.Invoke(null, [ClassIDType.Mesh, "fixture"]) is null, "A type-tree state removed before capability-free B");
Check(RuriRuntimeHook.IsGameLoaded("SwitchProductB"), "B active from same Hook catalog");
Select("SwitchProductA_1.0.0");
Check(FixtureModule.Value?.Invoke() == "switch-a" && !RuriRuntimeHook.IsGameLoaded("SwitchProductB"), "B to A");
Select(null);
Check(FixtureModule.Value is null && !RuriRuntimeHook.IsGameLoaded("SwitchProductA"), "A to no decoder");
Check(TypeTreeDatabase.ActiveVersion.IsEmpty && gate.Invoke(null, [ClassIDType.Mesh, "fixture"]) is null, "No stale schema state");
Console.WriteLine($"ADAPTER_REGISTRATION_CONTRACT_PASS checks={checks}");
UpstreamSyncContracts.Run();

[RipperHook("FixtureProduct", "1.0.0", "2021.3.0f1")]
public sealed class FixtureHookV1 : RipperHookCommon
{
    public void ApplyFixtureCapabilities() => ApplyCapabilities();
}

[RipperHook("FixtureProduct", "2.0.0", "2021.3.0f1")]
public sealed class FixtureHookV2 : RipperHookCommon { }

[RipperInstallProbe]
public static class FixtureInstallProbe
{
    public static IEnumerable<PlayerIdentity> Probe(string root)
    {
        if (File.Exists(Path.Combine(root, ".adapter-contract")))
            yield return new PlayerIdentity { DataFolder = root, Company = "Fixture", Product = "FixtureProduct",
                GameVersion = "1.5.0", EngineVersion = "2021.3.0f1", Engine = "FixtureEngine" };
    }
}

public static class FixtureModule
{
    public static Func<string>? Value;
}

[GameCapabilities("FixtureProduct")]
public static class FixtureCapabilities
{
    [Since("1.0.0"), FeedsModule(typeof(FixtureModule), nameof(FixtureModule.Value))]
    public static string V1() => "v1";
    [Since("2.0.0"), FeedsModule(typeof(FixtureModule), nameof(FixtureModule.Value))]
    public static string V2() => "v2";
}

[GameCapabilities("FixtureOtherProduct")]
public static class FixtureOtherCapabilities
{
    [Since("1.0.0"), FeedsModule(typeof(FixtureModule), nameof(FixtureModule.Value))]
    public static string V1() => "other";
}

[RipperHook("SwitchProductA", "1.0.0")]
public sealed class SwitchHookA : RuriHook
{
    public override void Initialize()
    {
        CapabilityResolver.Apply("SwitchProductA", "1.0.0", Registry);
        RuriRuntimeHook.RegisterLoadedGameHook("SwitchProductA");
        TypeTreeDatabase.ActiveVersion = new("fixture", "1");
        TypeTreeOverrides.RegisterGate(ClassIDType.Mesh, "fixture", _ => true, []);
    }
}

[RipperHook("SwitchProductB", "1.0.0")]
public sealed class SwitchHookB : RuriHook
{
    public override void Initialize() => RuriRuntimeHook.RegisterLoadedGameHook("SwitchProductB");
}

[GameCapabilities("SwitchProductA")]
public static class SwitchCapabilities
{
    [Since("1.0.0"), FeedsModule(typeof(FixtureModule), nameof(FixtureModule.Value))]
    public static string Value() => "switch-a";
}
