using System;
using AssetRipper.SourceGenerated;

namespace Ruri.RipperHook.Core.TypeTree;

/// <summary>
/// Names a class of this build that AssetRipper must not read through its type tree: the build writes more after the tree
/// than the tree states, so no read by the tree can end where the object does. The object stays what AssetRipper keeps of
/// a class it has no reader for -- its bytes -- and whatever decodes that class reads them itself.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TypeTreeRawAttribute : Attribute
{
    public ClassIDType ClassID { get; }

    public TypeTreeRawAttribute(ClassIDType classID)
    {
        ClassID = classID;
    }
}
