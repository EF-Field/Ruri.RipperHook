using System.Collections.Concurrent;
using System.Reflection;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Import.Structure.Assembly.TypeTrees;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Tpk.TypeTrees;
using Ruri.Hook.Attributes;
using Ruri.Hook.Core;
using Ruri.RipperHook.Core.TypeTree;

namespace Ruri.RipperHook.HookUtils.TypeTreeFallbackHook;

/// <summary>
/// AssetRipper reads a class it generated no type for through a type tree in its own vocabulary --
/// Unity's node and type names, as its embedded package states them. This answers that lookup from
/// the hook's database for the active build, out of the tree the database keeps in the same
/// vocabulary, converted the way AssetRipper converts its own package's nodes -- unless the title
/// states the class's release tree itself (<see cref="TitleReleaseTree"/>). A class the title declares raw
/// (<see cref="TypeTreeRawAttribute"/>) has no tree answered, so AssetRipper keeps its bytes.
///
/// The object that reads it keeps one structure when release and editor trees are one tree: the
/// two-structure form copies the release read over to the editor structure, and that copy has no
/// case for an array of arrays -- a class holding one could not be read at all.
/// </summary>
public class TypeTreeFallbackHook : CommonHook, IHookModule
{
    private static readonly Type DualStructureObjectType = typeof(TypeTreeObject).GetNestedType("DoubleTypeTreeObject", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(TypeTreeObject), "DoubleTypeTreeObject");

    private static readonly ConcurrentDictionary<ClassIDType, byte> RawClasses = new();

    public void OnApply()
    {
    }

    public static void KeepRaw(IEnumerable<ClassIDType> classIds)
    {
        ClassIDType[] declared = classIds.ToArray();
        foreach (ClassIDType classId in declared)
        {
            RawClasses[classId] = 0;
        }

        HookManager.RegisterCleanup(() =>
        {
            foreach (ClassIDType classId in declared)
            {
                RawClasses.TryRemove(classId, out _);
            }
        });
    }

    [RetargetMethod(typeof(TypeTreeObject), nameof(TypeTreeObject.Create), true, true,
        typeof(AssetInfo), typeof(TypeTreeNodeStruct), typeof(TypeTreeNodeStruct), typeof(ITypeResolver))]
    public static TypeTreeObject CreateObject(AssetInfo assetInfo, TypeTreeNodeStruct releaseRoot, TypeTreeNodeStruct editorRoot, ITypeResolver resolver)
    {
        if (releaseRoot.Equals(editorRoot))
        {
            return TypeTreeObject.Create(assetInfo, releaseRoot, resolver);
        }

        return (TypeTreeObject)Activator.CreateInstance(
            DualStructureObjectType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [assetInfo, releaseRoot, editorRoot, resolver],
            null)!;
    }

    [RetargetMethod(typeof(TypeTreeNodeStruct), nameof(TryMakeFromTpk))]
    public static bool TryMakeFromTpk(ClassIDType classID, UnityVersion version, out TypeTreeNodeStruct releaseTree, out TypeTreeNodeStruct editorTree)
    {
        TypeTreeVersion activeVersion = TypeTreeDatabase.ActiveVersion;
        if (RawClasses.ContainsKey(classID)
            || TypeTreeDatabase.GetUnityRoot(classID, activeVersion, editor: false) is not { } release)
        {
            releaseTree = default;
            editorTree = default;
            return false;
        }

        TypeTreeNodeStruct packaged = Convert(release.Root, release.Blob);
        releaseTree = TitleReleaseTree(classID, version, packaged) ?? packaged;
        editorTree = TypeTreeDatabase.GetUnityRoot(classID, activeVersion, editor: true) is { } editor
            ? Convert(editor.Root, editor.Blob)
            : packaged;
        return true;
    }

    /// <summary>A class's release tree as the title's own build writes it, given the tree the package holds for it
    /// and the build's engine version: null when the title states none and the package's tree is read. A title's
    /// hook retargets this for the classes its build writes differently from its dump.</summary>
    public static TypeTreeNodeStruct? TitleReleaseTree(ClassIDType classID, UnityVersion version, TypeTreeNodeStruct packaged) => null;

    internal static TypeTreeNodeStruct Convert(TpkUnityNode node, TpkTypeTreeBlob blob)
    {
        TypeTreeNodeStruct[] subNodes = new TypeTreeNodeStruct[node.SubNodes.Length];
        for (int i = 0; i < subNodes.Length; i++)
        {
            subNodes[i] = Convert(blob.NodeBuffer[node.SubNodes[i]], blob);
        }
        return new TypeTreeNodeStruct(blob.StringBuffer[node.TypeName], blob.StringBuffer[node.Name], node.Version,
            (TransferMetaFlags)node.MetaFlag, subNodes);
    }
}
