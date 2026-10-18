using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using Ruri.Hook.Core;

namespace Ruri.RipperHook.Core.TypeTree;

public static class TypeTreeDatabase
{
    public const string ResourceName = "RuriTypeTree.tpk";
    public const string AdditionalResourcePrefix = "Ruri.GameHook.TypeTrees.";
    public const string PathEnvironmentVariable = "RURI_TYPE_TREE_TPK";

    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<(int ClassID, string Lineage, string Version), TypeTreeNode?> ReleaseRootCache = new();
    private static readonly ConcurrentDictionary<(int ClassID, string Lineage, string Version), TypeTreeNode?> EditorRootCache = new();

    private static TypeTreeVersion _activeVersion;
    public static TypeTreeVersion ActiveVersion
    {
        get => _activeVersion;
        set
        {
            TypeTreeVersion previous = _activeVersion;
            _activeVersion = value;
            if (previous != value && HookManager.CurrentScopeId is not null)
                HookManager.RegisterCleanup(() => { if (_activeVersion == value) _activeVersion = previous; });
        }
    }

    private static TypeTreeManifest? _manifest;
    private static Dictionary<string, Lineage>? _lineages;
    private static string _origin = "<unloaded>";

    private sealed class Lineage
    {
        public required Dictionary<int, (TpkClassInformation Class, TpkTypeTreeBlob Blob)> ClassesById;
        public required Dictionary<int, (TpkClassInformation Class, TpkTypeTreeBlob Blob)> UnityClassesById;
        public Dictionary<string, int>? IdsByName;

        public static Lineage From(IReadOnlyList<TpkTypeTreeBlob> blobs, IReadOnlyList<TpkTypeTreeBlob> unityNames) =>
            new() { ClassesById = Index(blobs), UnityClassesById = Index(unityNames) };

        private static Dictionary<int, (TpkClassInformation, TpkTypeTreeBlob)> Index(IReadOnlyList<TpkTypeTreeBlob> blobs)
        {
            Dictionary<int, (TpkClassInformation, TpkTypeTreeBlob)> classesById = new();
            foreach (TpkTypeTreeBlob blob in blobs)
            {
                foreach (TpkClassInformation classInformation in blob.ClassInformation)
                {
                    classesById[classInformation.ID] = (classInformation, blob);
                }
            }
            return classesById;
        }
    }

    public static string Origin
    {
        get
        {
            EnsureLoaded();
            return _origin;
        }
    }

    public static TypeTreeManifest Manifest
    {
        get
        {
            EnsureLoaded();
            return _manifest!;
        }
    }

    /// <summary>
    /// Add (or replace) a lineage built at run time -- a schema read from the install itself,
    /// such as another engine's reflection dump restated as type trees -- so its classes resolve
    /// exactly like the packed ones. <paramref name="versions"/> lists the lineage's snapshots in
    /// ordinal order with the Unity layout version each was emitted against. Such a schema has
    /// one vocabulary, its own.
    /// </summary>
    public static void RegisterLineage(string key, IReadOnlyList<TpkTypeTreeBlob> blobs, IReadOnlyList<(string Version, string Engine)> versions)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(blobs);
        EnsureLoaded();
        lock (SyncRoot)
        {
            _lineages![key] = Lineage.From(blobs, blobs);

            TypeTreeManifest.LineageEntry entry = new() { Key = key, UnityNames = key };
            foreach ((string version, string engine) in versions)
            {
                entry.Versions.Add(new TypeTreeManifest.VersionEntry { Key = version, Engine = engine });
            }
            _manifest!.Lineages.RemoveAll(existing => existing.Key == key);
            _manifest.Lineages.Add(entry);
            _manifest.Invalidate();
        }
        foreach (var cached in ReleaseRootCache.Keys.Where(cacheKey => cacheKey.Lineage == key).ToArray())
        {
            ReleaseRootCache.TryRemove(cached, out _);
        }
        foreach (var cached in EditorRootCache.Keys.Where(cacheKey => cacheKey.Lineage == key).ToArray())
        {
            EditorRootCache.TryRemove(cached, out _);
        }
    }

    /// <summary>
    /// The class id a lineage files <paramref name="className"/> under, or -1. Class ids of a
    /// lineage restated from a named schema are ordinals assigned at pack time, so a name is
    /// the only identity the source can ask by.
    /// </summary>
    public static int ClassIdByName(string lineageKey, string className)
    {
        EnsureLoaded();
        if (!_lineages!.TryGetValue(lineageKey, out Lineage? lineage))
        {
            return -1;
        }
        Dictionary<string, int>? index = lineage.IdsByName;
        if (index is null)
        {
            index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((TpkClassInformation classInformation, TpkTypeTreeBlob blob) in lineage.ClassesById.Values)
            {
                foreach (KeyValuePair<UnityVersion, TpkUnityClass?> pair in classInformation.Classes)
                {
                    if (pair.Value is not null)
                    {
                        index[blob.StringBuffer[pair.Value.Name]] = classInformation.ID;
                        break;
                    }
                }
            }
            lineage.IdsByName = index;
        }
        return index.TryGetValue(className, out int id) ? id : -1;
    }

    public static UnityVersion GetEngineVersion(TypeTreeVersion version)
    {
        EnsureLoaded();

        string? engine = _manifest!.GetEngine(version.Lineage, version.Version);
        if (string.IsNullOrEmpty(engine))
        {
            throw new InvalidOperationException(
                $"[TypeTreeDatabase] {version} declares no engine version in {_origin}. Repack the tpk with Ruri.Tpk.");
        }

        return UnityVersion.Parse(engine);
    }

    public static TypeTreeNode? GetReleaseRoot(ClassIDType classID, TypeTreeVersion version)
    {
        if (version.IsEmpty)
        {
            return null;
        }

        return ReleaseRootCache.GetOrAdd(
            ((int)classID, version.Lineage, version.Version),
            static key => BuildRoot(key.ClassID, key.Lineage, key.Version, editor: false));
    }

    public static TypeTreeNode? GetEditorRoot(ClassIDType classID, TypeTreeVersion version)
    {
        if (version.IsEmpty)
        {
            return null;
        }

        return EditorRootCache.GetOrAdd(
            ((int)classID, version.Lineage, version.Version),
            static key => BuildRoot(key.ClassID, key.Lineage, key.Version, editor: true));
    }

    /// <summary>
    /// A class's tree as the engine spells it (see <see cref="TypeTreeManifest.LineageEntry.UnityNames"/>),
    /// for a consumer that hands it to AssetRipper's own reader rather than to this hook's interpreter:
    /// the root node and the blob whose buffers it indexes, or null wherever <see cref="GetReleaseRoot"/>
    /// and <see cref="GetEditorRoot"/> answer null.
    /// </summary>
    public static (TpkUnityNode Root, TpkTypeTreeBlob Blob)? GetUnityRoot(ClassIDType classID, TypeTreeVersion version, bool editor)
    {
        if (version.IsEmpty)
        {
            return null;
        }

        return FindRoot((int)classID, version.Lineage, version.Version, editor, unityNames: true);
    }

    private static TypeTreeNode? BuildRoot(int classID, string lineageKey, string versionKey, bool editor) =>
        FindRoot(classID, lineageKey, versionKey, editor, unityNames: false) is { } found
            ? TypeTreeNode.FromTpk(found.Root, found.Blob.StringBuffer, found.Blob.NodeBuffer)
            : null;

    private static (TpkUnityNode Root, TpkTypeTreeBlob Blob)? FindRoot(int classID, string lineageKey, string versionKey,
        bool editor, bool unityNames)
    {
        EnsureLoaded();

        if (!_lineages!.TryGetValue(lineageKey, out Lineage? lineage))
        {
            throw new InvalidOperationException(
                $"[TypeTreeDatabase] No lineage '{lineageKey}' in {_origin}. Known: {string.Join(", ", _lineages.Keys)}.");
        }

        int ordinal = _manifest!.GetOrdinal(lineageKey, versionKey);
        if (ordinal < 0)
        {
            throw new InvalidOperationException(
                $"[TypeTreeDatabase] Lineage '{lineageKey}' does not declare version '{versionKey}' in {_origin}. " +
                "Dump that build's type tree and repack -- reading it with a neighbouring build's layout is not safe.");
        }

        Dictionary<int, (TpkClassInformation Class, TpkTypeTreeBlob Blob)> classes =
            unityNames ? lineage.UnityClassesById : lineage.ClassesById;
        if (!classes.TryGetValue(classID, out (TpkClassInformation Class, TpkTypeTreeBlob Blob) entry))
        {
            return null;
        }

        TpkUnityClass? unityClass = GetItemForOrdinal(entry.Class.Classes, ordinal);
        if (unityClass is null)
        {
            return null;
        }

        TpkUnityClassFlags required = editor ? TpkUnityClassFlags.HasEditorRootNode : TpkUnityClassFlags.HasReleaseRootNode;
        if ((unityClass.Flags & required) == 0)
        {
            return null;
        }

        ushort root = editor ? unityClass.EditorRootNode : unityClass.ReleaseRootNode;
        return (entry.Blob.NodeBuffer[root], entry.Blob);
    }

    private static TpkUnityClass? GetItemForOrdinal(List<KeyValuePair<UnityVersion, TpkUnityClass?>> list, int ordinal)
    {
        TpkUnityClass? result = null;
        foreach (KeyValuePair<UnityVersion, TpkUnityClass?> pair in list)
        {
            if (TypeTreeOrdinal.ToOrdinal(pair.Key) > ordinal)
            {
                break;
            }
            result = pair.Value;
        }
        return result;
    }

    private static void EnsureLoaded()
    {
        if (Volatile.Read(ref _manifest) is not null)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_manifest is not null)
            {
                return;
            }

            Assembly assembly = typeof(TypeTreeDatabase).Assembly;
            // An explicit override remains authoritative; it never silently mixes with embedded packs.
            bool overridden = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PathEnvironmentVariable));
            string[] additional = overridden ? [] : assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(AdditionalResourcePrefix, StringComparison.Ordinal) &&
                               name.EndsWith(".tpk", StringComparison.Ordinal))
                .OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            TypeTreeManifest loaded = new();
            Dictionary<string, Lineage> lineages = new(StringComparer.Ordinal);
            Dictionary<string, string> origins = new(StringComparer.Ordinal);
            List<string> packs = new();
            using (Stream? stream = OpenTpkStream(out string origin, optional: additional.Length > 0))
            {
                if (stream is not null) AddPack(stream, origin, loaded, lineages, origins, packs);
            }
            foreach (string name in additional)
            {
                using Stream stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidDataException($"[TypeTreeDatabase] Missing registered resource {name}.");
                AddPack(stream, "embedded:" + name, loaded, lineages, origins, packs);
            }
            // Publish atomically only after every pack validates. A failing pack leaves no partial catalog.
            _lineages = lineages;
            _origin = string.Join("; ", packs);
            Volatile.Write(ref _manifest, loaded);
        }
    }

    private static void AddPack(Stream stream, string origin, TypeTreeManifest combined,
        Dictionary<string, Lineage> lineages, Dictionary<string, string> origins, List<string> packs)
    {
        TpkDataBlob root = TpkFile.FromStream(stream).GetDataBlob();
        if (root is not TpkCollectionBlob collection)
            throw new InvalidDataException($"[TypeTreeDatabase] {origin} holds a {root.DataType} blob; expected a Collection.");
        TypeTreeManifest? manifest = null;
        Dictionary<string, TpkTypeTreeBlob> blobs = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, TpkDataBlob> pair in collection.Blobs)
        {
            if (pair.Value is TpkJsonBlob json && pair.Key == TypeTreeManifest.BlobName)
                manifest = TypeTreeManifest.FromJson(json.Text);
            else if (pair.Value is TpkTypeTreeBlob tree) blobs.Add(pair.Key, tree);
        }
        TypeTreeManifest loaded = manifest ?? throw new InvalidDataException(
            $"[TypeTreeDatabase] {origin} has no '{TypeTreeManifest.BlobName}' manifest.");
        foreach (TypeTreeManifest.LineageEntry entry in loaded.Lineages)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                throw new InvalidDataException($"[TypeTreeDatabase] Empty lineage key in {origin}.");
            if (origins.TryGetValue(entry.Key, out string? previous))
                throw new InvalidDataException($"[TypeTreeDatabase] Duplicate lineage '{entry.Key}' in {previous} and {origin}.");
            // Blob names are PACK-LOCAL: two games may both have a helper blob called 'unity'.
            lineages.Add(entry.Key, Lineage.From([Blob(entry.Key)], [Blob(entry.UnityNames)]));
            origins.Add(entry.Key, origin);
            combined.Lineages.Add(entry);
        }
        packs.Add(origin);

        TpkTypeTreeBlob Blob(string key) => blobs.TryGetValue(key, out TpkTypeTreeBlob? blob) ? blob
            : throw new InvalidDataException($"[TypeTreeDatabase] {origin}'s manifest names missing blob '{key}'.");
    }

    private static Stream? OpenTpkStream(out string origin, bool optional = false)
    {
        string? overridePath = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                throw new FileNotFoundException($"[TypeTreeDatabase] {PathEnvironmentVariable} points at a missing file.", overridePath);
            }
            origin = overridePath;
            return File.OpenRead(overridePath);
        }

        Assembly assembly = typeof(TypeTreeDatabase).Assembly;

        string adjacentPath = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory, ResourceName);
        if (File.Exists(adjacentPath))
        {
            origin = adjacentPath;
            return File.OpenRead(adjacentPath);
        }

        Stream? resource = assembly.GetManifestResourceStream($"{nameof(Ruri)}.{nameof(RipperHook)}.{ResourceName}")
            ?? assembly.GetManifestResourceStream(ResourceName);
        if (resource is not null)
        {
            origin = $"embedded:{ResourceName}";
            return resource;
        }

        if (optional) { origin = "<no base pack>"; return null; }
        throw new FileNotFoundException(
            $"[TypeTreeDatabase] {ResourceName} was not found next to {assembly.GetName().Name} nor embedded in it. " +
            $"Build it with Ruri.Tpk, or set {PathEnvironmentVariable}.", adjacentPath);
    }
}
