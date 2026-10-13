using Ruri.Tpk.Pipeline;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using Ruri.RipperHook.Core.TypeTree;
using System.Text.Json;

if (args.Length is < 3 or > 4)
    throw new ArgumentException("Usage: UpgradeTypeTree input.tpk dump-root output.tpk");
byte[] input = File.ReadAllBytes(args[0]);
TypeTreeTpkBuilder.UpgradeUnityNames(args[0], args[1], args[2]);
using MemoryStream oldStream = new(input);
using FileStream newStream = File.OpenRead(args[2]);
var before = (TpkCollectionBlob)TpkFile.FromStream(oldStream).GetDataBlob();
var after = (TpkCollectionBlob)TpkFile.FromStream(newStream).GetDataBlob();
foreach (var pair in before.Blobs.Where(p => p.Value is TpkTypeTreeBlob))
{
    var retained = after.Blobs.Single(p => p.Key == pair.Key).Value;
    if (!TpkFile.FromBlob(pair.Value, TpkCompressionType.Brotli).WriteToMemory().AsSpan()
        .SequenceEqual(TpkFile.FromBlob(retained, TpkCompressionType.Brotli).WriteToMemory()))
        throw new InvalidDataException($"Accepted renamed tree changed: {pair.Key}");
}
Console.WriteLine("TYPE_TREE_RENAMED_PAYLOAD_PRESERVED_PASS");
if (args.Length == 4 && args[3] == "--self-test")
{
    string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!, "typetree-negative-tests");
    Directory.CreateDirectory(folder);
    TpkJsonBlob json = (TpkJsonBlob)before.Blobs.Single(p => p.Key == TypeTreeManifest.BlobName).Value;
    string original = json.Text;
    foreach (var test in new (string Name, Action<TypeTreeManifest> Mutate)[] {
        ("unknown-format", m => m.FormatVersion = 99),
        ("already-upgraded", m => m.FormatVersion = 2),
        ("wrong-version", m => m.Lineages[0].Versions[^1].Key = "missing-version"),
        ("wrong-engine", m => m.Lineages[0].Versions[^1].Engine = "6000.0.0f1"),
        ("mixed-schema", m => m.Lineages[0].UnityNames = "unexpected-unity-blob")
    })
    {
        TypeTreeManifest manifest = JsonSerializer.Deserialize<TypeTreeManifest>(original)!;
        test.Mutate(manifest);
        json.Text = manifest.ToJson();
        string path = Path.Combine(folder, test.Name + ".tpk");
        string output = Path.Combine(folder, test.Name + "-output.tpk");
        TpkFile.FromBlob(before, TpkCompressionType.Brotli).WriteToFile(path);
        try {
            TypeTreeTpkBuilder.UpgradeUnityNames(path, args[1], output);
            throw new Exception($"Invalid input accepted: {test.Name}");
        } catch (InvalidDataException) {
            if (File.Exists(output)) throw new Exception($"Partial output written: {test.Name}");
            Console.WriteLine($"TYPE_TREE_REJECT_PASS {test.Name}");
        }
    }
    json.Text = original;
    try {
        TypeTreeTpkBuilder.UpgradeUnityNames(args[0], args[1], args[0]);
        throw new Exception("Input overwrite accepted");
    } catch (ArgumentException) { Console.WriteLine("TYPE_TREE_REJECT_PASS input-overwrite"); }
    if (!File.ReadAllBytes(args[0]).AsSpan().SequenceEqual(input)) throw new Exception("Input pack mutated");
}
