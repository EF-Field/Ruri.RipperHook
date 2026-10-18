using AssetRipper.Primitives;
using AssetRipper.SourceGenerated;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using Ruri.RipperHook.Core.TypeTree;

public static class SchemaFixtures
{
    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        Pack("ContractA", "ClassA", "2021.3.0f1").WriteToFile(Path.Combine(directory, "A.tpk"));
        Pack("ContractB", "ClassB", "2022.3.0f1").WriteToFile(Path.Combine(directory, "B.tpk"));
        Pack("ContractA", "Duplicate", "2021.3.0f1").WriteToFile(Path.Combine(directory, "Duplicate.tpk"));
        File.WriteAllText(Path.Combine(directory, "Malformed.tpk"), "not a pack");
        Console.WriteLine("SCHEMA_FIXTURES_WRITTEN");
    }

    private static TpkFile Pack(string lineage, string className, string engine)
    {
        TpkTypeTreeBlob blob = new();
        blob.Versions.Add(TypeTreeOrdinal.ToUnityVersion(0));
        blob.CommonString.Add(UnityVersion.MinVersion, []);
        var name = blob.StringBuffer.AddString(className);
        var node = blob.NodeBuffer.AddNode(new TpkUnityNode {
            Name = blob.StringBuffer.AddString("Base"), TypeName = name, Version = 1, SubNodes = [] });
        TpkClassInformation info = new(1);
        info.Classes.Add(new(TypeTreeOrdinal.ToUnityVersion(0), new TpkUnityClass {
            Name = name, Flags = TpkUnityClassFlags.HasReleaseRootNode, ReleaseRootNode = node }));
        blob.ClassInformation.Add(info);
        TypeTreeManifest manifest = new();
        manifest.Lineages.Add(new() { Key = lineage, UnityNames = "shared-unity", Versions = [new() { Key = "1", Engine = engine }] });
        TpkCollectionBlob collection = new();
        collection.Add(lineage, blob);
        collection.Add("shared-unity", blob);
        collection.Add(TypeTreeManifest.BlobName, new TpkJsonBlob { Text = manifest.ToJson() });
        return TpkFile.FromBlob(collection, TpkCompressionType.Brotli);
    }

    public static void Verify(string[] expected)
    {
        if (expected.Length > 0 && expected[0] == "only")
        {
            expected = expected.Skip(1).ToArray();
            if (TypeTreeDatabase.Manifest.Lineages.Count != expected.Length) throw new Exception("Override mixed with embedded packs");
        }
        if (expected.Length == 1 && expected[0] is "duplicate" or "malformed" or "missing")
        {
            try { _ = TypeTreeDatabase.Manifest; }
            catch (Exception ex)
            {
                if (expected[0] == "duplicate" && !ex.Message.Contains("Duplicate lineage")) throw;
                if (expected[0] == "missing" && ex is not FileNotFoundException) throw;
                if (expected[0] == "malformed" && ex is FileNotFoundException) throw;
                Console.WriteLine("SCHEMA_REJECTION_REASON " + ex.GetType().Name + ": " + ex.Message);
                // Failed validation must not publish a partial manifest; a retry must fail again.
                try { _ = TypeTreeDatabase.Manifest; } catch { Console.WriteLine("SCHEMA_REJECTION_PASS " + expected[0]); return; }
                throw new Exception("Partial schema state was published");
            }
            throw new Exception("Invalid schema inputs accepted");
        }
        foreach (string key in expected)
        {
            string engine = key == "ContractA" ? "2021.3.0f1" : "2022.3.0f1";
            string name = key == "ContractA" ? "ClassA" : "ClassB";
            var version = new TypeTreeVersion(key, "1");
            if (TypeTreeDatabase.GetEngineVersion(version) != UnityVersion.Parse(engine)) throw new Exception("Schema engine collision");
            if (TypeTreeDatabase.GetReleaseRoot((ClassIDType)1, version)?.TypeName != name) throw new Exception("Class ID schema collision");
            if (TypeTreeDatabase.ClassIdByName(key, name) != 1) throw new Exception("Pack-local original-name blob collision");
        }
        Console.WriteLine("MULTI_SCHEMA_CONTRACT_PASS lineages=" + string.Join(',', expected));
    }
}
