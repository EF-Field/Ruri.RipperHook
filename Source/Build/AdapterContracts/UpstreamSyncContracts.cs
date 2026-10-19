using System.Runtime.CompilerServices;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.TextureDecoder.Rgb.Formats;
using Ruri.Hook;
using Ruri.Hook.Attributes;
using Ruri.Hook.Core;
using Ruri.RipperHook.BlenderBridge.Data;
using Ruri.RipperHook.BlenderBridge.Statements;
using Ruri.RipperHook.BlenderBridge.Tables;
using Ruri.RipperHook.CabMapping;

public static class UpstreamSyncContracts
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            checks++;
        }

        CabTable first = EmptyMap("fixture-first"), second = EmptyMap("fixture-second");
        const string dataset = "fixture.map-isolation";
        Datasets.Publish(dataset, DataRole.Diagnostic, [], "Synthetic map isolation", request =>
        {
            TableBuilder builder = new(dataset, "name");
            builder.Add(request.Map.BaseFolder);
            return builder.Build();
        });
        try
        {
            var one = Datasets.Table(dataset, [], map: first);
            var two = Datasets.Table(dataset, [], map: second);
            var again = Datasets.Table(dataset, [], map: first);
            Check(one.Handle != two.Handle, "Different maps reused a dataset handle");
            Check(one.Handle == again.Handle && ReferenceEquals(one.Table, again.Table), "Same map cache changed identity");
            Check(one.Table["name"].Text(0) == "fixture-first", "First map table replaced");
            Check(two.Table["name"].Text(0) == "fixture-second", "Second map table replaced");
            Check(TableRegistry.Search(one.Handle, "first", null).SequenceEqual([0]), "First handle searches second map");
            Check(TableRegistry.Search(two.Handle, "first", null).Length == 0, "Map search leaked across handles");
        }
        finally { Datasets.Clear(dataset); }

        Parallel.For(0, 32, _ =>
        {
            if (!TextureEncoding.Ready) throw new Exception("Texture warmup failed");
            using MemoryStream stream = new();
            new DirectBitmap<ColorRGBA<byte>, byte>(16, 16).Save(stream, ImageExportFormat.Png);
            if (stream.Length < 8 || !stream.ToArray().AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new Exception("Parallel PNG encoding failed");
        });
        Check(TextureEncoding.Ready, "Texture warmup was not stable");

        HookInlining.ReserveDeclared();
        for (int index = 0; index < 10000; index++)
            if (InliningProbe.Caller(index) != index + 1) throw new Exception("Unexpected unhooked probe");
        const string scope = "fixture-late-inlining";
        try
        {
            HookManager.RunInScope(scope, () => new LateInliningHook().Initialize());
            Check(InliningProbe.Caller(10) == 1010, "Caller compiled before late hook bypassed it");
            HookManager.DisposeScope(scope);
            Check(InliningProbe.Caller(10) == 11, "Late hook did not clean up");
            HookManager.RunInScope(scope, () => new LateInliningHook().Initialize());
            Check(InliningProbe.Caller(10) == 1010, "Late hook reactivation failed");
        }
        finally { HookManager.DisposeScope(scope); }
        Console.WriteLine($"UPSTREAM_MAP_INLINING_PNG_PASS checks={checks} parallel_encodes=32");
    }

    private static CabTable EmptyMap(string name) => new()
    {
        BaseFolder = name, Count = 0, PhantomCount = 0,
        CabBlob = [], CabOffsets = [0], DistinctFileBlob = [], DistinctFileOffsets = [0], FileIndex = [],
        EntryFileNameBlob = [], EntryFileNameOffsets = [0], FactBlob = [], FactOffsets = [0], FactStarts = [0],
        ContainerPathBlob = [], ContainerPathOffsets = [0], ContainerPathStarts = [0],
        ClassIdsFlat = [], ClassIdStarts = [0], DependenciesFlat = [], DependencyStarts = [0],
        ReverseFlat = [], ReverseStarts = [0]
    };
}

public static class InliningProbe
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Value(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Caller(int value) => Value(value);
}

public sealed class LateInliningHook : RuriHook
{
    [RetargetMethod(typeof(InliningProbe), nameof(InliningProbe.Value), true, true, typeof(int))]
    public static int Replacement(int value) => value + 1000;
}
