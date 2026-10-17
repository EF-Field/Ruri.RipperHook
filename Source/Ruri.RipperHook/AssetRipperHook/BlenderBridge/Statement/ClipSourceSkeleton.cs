using AssetRipper.Assets;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_90;
using AssetRipper.SourceGenerated.Classes.ClassID_95;
using AssetRipper.SourceGenerated.Extensions;
using Ruri.RipperHook.CabMapping;
using Ruri.RipperHook.BlenderBridge.Data;
using Ruri.RipperHook.BlenderBridge.Tables;

namespace Ruri.RipperHook.BlenderBridge.Statements;

/// <summary>
/// Which rig a set of clip bindings was authored on -- the measured answer, with no name guessing: every
/// candidate Avatar's transform table is scored by how many of the binding hashes it covers, a full cover
/// ends the search, and the argmax wins otherwise. The bindings are the paths a clip statement states
/// (a restored path or a hashed placeholder, either one is a hash), so the question is about exactly the
/// clips a caller holds. Candidates come in tiers that are only an ORDER (what the map can reach from the
/// archive the clips came from, then every archive carrying an Avatar, cheapest first). The HOST of that
/// avatar is the prefab whose root Animator names exactly it -- a cutscene that merely stages the
/// character carries the staged pose.
/// </summary>
public static class ClipSourceSkeleton
{
    public const string SourceSkeletonId = "core.clip.source_skeleton";
    public const string Paths = "paths";
    public const string Clip = "clip";

    private sealed record Scanned(string Cab, string Name, int DependencyCount, int TosSize, HashSet<uint> Crcs);

    private sealed record Scored(Scanned Avatar, int Hits, double Ratio);

    public static void Register()
    {
        Datasets.Publish(SourceSkeletonId, DataRole.Binding,
            [DataParam.List(Paths, required: true), DataParam.Text(Clip, required: false)],
            "The avatar whose transform table covers the most of a clip's binding paths, best first: the "
            + "avatar's name, the archive it lives in, how many bindings it covers and the fraction, its table size, "
            + "and the prefab rooted on exactly that avatar -- the host to build the source rig from -- as a seed. "
            + "paths are the bindings as core.statement.clips states them; clip is the archive or container path "
            + "they came from and only decides which archives are tried first.", Table);
    }

    private static ColumnTable Table(DataRequest request)
    {
        CabTable map = request.Map;
        HashSet<uint> crcs = [];
        foreach (string path in request.List(Paths))
        {
            if (path.Length > 0)
            {
                crcs.Add(UnitySkinning.EntryCrc(path));
            }
        }
        string clipCab = request.Given(Clip) ? ArchiveOf(map, request.Text(Clip)) : string.Empty;
        TableBuilder table = new(SourceSkeletonId, "skeleton", "cab", "hits#", "ratio#", "tos#", "host_cab", "host_seed");
        table.Role(ColumnRole.Label | ColumnRole.Key, "skeleton").Role(ColumnRole.Payload, "host_seed");
        if (crcs.Count == 0)
        {
            return table.Build();
        }
        List<Scored> ranked = Score(map, clipCab, crcs, request.Cancellation);
        foreach (Scored entry in ranked.Take(8))
        {
            (string hostCab, string hostSeed) = entry.Ratio > 0 ? Host(map, entry.Avatar, request.Cancellation) : (string.Empty, string.Empty);
            table.Row(entry.Avatar.Name, entry.Avatar.Cab, entry.Hits, entry.Ratio, entry.Avatar.TosSize, hostCab, hostSeed);
        }
        return table.Build();
    }

    private static string ArchiveOf(CabTable map, string seed) =>
        map.TryGetId(seed, out _) ? seed : CabMap.ResolveCabsForPaths(map, [seed]).FirstOrDefault()
            ?? throw new ArgumentException($"'{seed}' is neither an archive nor a container path of the loaded map.");

    private static List<Scored> Score(CabTable map, string clipCab, HashSet<uint> crcs, CancellationToken cancellation)
    {
        List<Scored> ranked = [];
        HashSet<string> parsed = new(StringComparer.OrdinalIgnoreCase);
        bool Absorb(IEnumerable<Scanned> avatars)
        {
            foreach (Scanned avatar in avatars)
            {
                int hits = avatar.Crcs.Count(crcs.Contains);
                ranked.Add(new Scored(avatar, hits, hits / (double)crcs.Count));
                if (hits >= crcs.Count)
                {
                    return true;
                }
            }
            return false;
        }
        foreach ((_, string cab) in GraphCandidates(map, clipCab))
        {
            cancellation.ThrowIfCancellationRequested();
            if (parsed.Add(cab) && Absorb(Scan(map, cab)))
            {
                return Ranked(ranked);
            }
        }
        foreach ((_, string cab) in AvatarCabs(map))
        {
            cancellation.ThrowIfCancellationRequested();
            if (parsed.Add(cab) && Absorb(Scan(map, cab)))
            {
                return Ranked(ranked);
            }
        }
        return Ranked(ranked);
    }

    private static List<Scored> Ranked(List<Scored> ranked) => ranked
        .OrderByDescending(entry => entry.Hits)
        .ThenBy(entry => entry.Avatar.DependencyCount)
        .ThenByDescending(entry => entry.Avatar.TosSize)
        .ThenBy(entry => entry.Avatar.Name, StringComparer.Ordinal)
        .ToList();

    private static bool Carries(CabTable map, int cabId, params ClassIDType[] classes)
    {
        ReadOnlySpan<int> ids = map.ClassIds(cabId);
        foreach (ClassIDType wanted in classes)
        {
            if (!ids.Contains((int)wanted))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Two reverse hops then forward: the clips' archive's dependents, their dependents, and
    /// those archives' forward closures -- where an Avatar finally sits.</summary>
    private static List<(int Dependencies, string Cab)> GraphCandidates(CabTable map, string clipCab)
    {
        if (clipCab.Length == 0 || !map.TryGetId(clipCab, out int clipId))
        {
            return [];
        }
        int[] hop1 = map.Dependents(clipId).ToArray();
        if (hop1.Length == 0)
        {
            return [];
        }
        HashSet<int> first = new(hop1);
        List<int> hop2 = [];
        foreach (int cab in hop1)
        {
            foreach (int dependent in map.Dependents(cab))
            {
                if (!first.Contains(dependent))
                {
                    hop2.Add(dependent);
                }
            }
        }
        List<(int, string)> found = [];
        foreach (int cab in map.ClosureIds(hop1.Concat(hop2)))
        {
            if (cab < map.Count && Carries(map, cab, ClassIDType.Avatar))
            {
                found.Add((map.DependencyCount(cab), map.CabName(cab)));
            }
        }
        found.Sort();
        return found;
    }

    private static List<(int Dependencies, string Cab)> AvatarCabs(CabTable map)
    {
        List<(int, string)> candidates = [];
        for (int id = 0; id < map.Count; id++)
        {
            if (Carries(map, id, ClassIDType.Avatar))
            {
                candidates.Add((map.DependencyCount(id), map.CabName(id)));
            }
        }
        candidates.Sort();
        return candidates;
    }

    private static IEnumerable<Scanned> Scan(CabTable map, string cab)
    {
        GameData? loaded = ClosureReader.Read(map, [cab], reachThroughDependents: true);
        if (loaded is null)
        {
            yield break;
        }
        int dependencies = map.TryGetId(cab, out int id) ? map.DependencyCount(id) : 0;
        foreach (IUnityObjectBase asset in loaded.GameBundle.FetchAssets())
        {
            if (asset is not IAvatar avatar || !string.Equals(asset.Collection.Name, cab, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            HashSet<uint> crcs = [];
            foreach (var pair in avatar.TOS)
            {
                crcs.Add(pair.Key);
            }
            yield return new Scanned(cab, avatar.Name.String, dependencies, crcs.Count, crcs);
        }
    }

    /// <summary>The prefab whose ROOT Animator names exactly this avatar, among the archives
    /// that use the avatar's archive, smallest closures first -- as a seed naming that root, since
    /// the archive routinely files several prefabs.</summary>
    private static (string Cab, string Seed) Host(CabTable map, Scanned avatar, CancellationToken cancellation)
    {
        if (!map.TryGetId(avatar.Cab, out int avatarId))
        {
            return (string.Empty, string.Empty);
        }
        List<(int Closure, int Dependencies, string Cab)> ranked = [];
        foreach (int cab in map.Dependents(avatarId))
        {
            if (cab < map.Count && Carries(map, cab, ClassIDType.GameObject, ClassIDType.Animator))
            {
                ranked.Add((map.ClosureIds([cab]).Length, map.DependencyCount(cab), map.CabName(cab)));
            }
        }
        ranked.Sort();
        List<string> ordered = ranked.Select(entry => entry.Cab).ToList();
        if (!ordered.Contains(avatar.Cab, StringComparer.OrdinalIgnoreCase) && Carries(map, avatarId, ClassIDType.GameObject, ClassIDType.Animator))
        {
            ordered.Add(avatar.Cab);
        }
        foreach (string cab in ordered)
        {
            cancellation.ThrowIfCancellationRequested();
            GameData? loaded = ClosureReader.Read(map, [cab], reachThroughDependents: true);
            if (loaded is null)
            {
                continue;
            }
            foreach (IUnityObjectBase asset in loaded.GameBundle.FetchAssets())
            {
                if (asset is not IGameObject root || !root.IsRoot()
                    || !string.Equals(root.Collection.Name, cab, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                IAnimator? animator = root.TryGetComponent<IAnimator>();
                if (animator?.AvatarP is { } found && string.Equals(found.Name.String, avatar.Name, StringComparison.Ordinal))
                {
                    return (cab, cab + StatementSources.RootMarker + root.Name.String);
                }
            }
        }
        return (string.Empty, string.Empty);
    }
}
