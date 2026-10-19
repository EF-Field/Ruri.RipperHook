using System.Reflection;
using System.Reflection.Metadata;
using AssetRipper.SourceGenerated;
using Ruri.Hook.Attributes;
using Ruri.Hook.Core;
using Ruri.RipperHook.Attributes;

namespace Ruri.RipperHook.Core.TypeTree;

/// <summary>
/// The upstream methods type-tree hooks retarget from code: a decoder names the classes it re-reads by
/// <see cref="TypeTreeHookAttribute"/>, and the hook lands on the <c>ReadRelease</c> of whichever generated version of each
/// class its build resolves to -- so every version of every named class is a source. The generated assembly holds tens of
/// thousands of types, so they are found by name in its metadata and only the matching ones are loaded.
/// </summary>
internal static class TypeTreeHookSources
{
    private const string GeneratedNamespace = "AssetRipper.SourceGenerated.Classes.ClassID_";

    [RetargetSources]
    internal static unsafe IEnumerable<MethodBase> ReadReleaseMethods()
    {
        HashSet<string> namespaces = new(StringComparer.Ordinal);
        foreach (Type hook in HookCatalog.TypesOf(typeof(TypeTreeHookSources).Assembly))
        {
            foreach (TypeTreeHookAttribute attribute in hook.GetCustomAttributes<TypeTreeHookAttribute>(false))
            {
                namespaces.Add(GeneratedNamespace + (int)attribute.ClassID);
            }
        }

        Assembly generated = typeof(ClassIDType).Assembly;
        List<MethodBase> sources = new();
        if (!generated.TryGetRawMetadata(out byte* metadata, out int length))
        {
            return sources;
        }
        MetadataReader reader = new(metadata, length);
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);
            if (definition.Namespace.IsNil)
            {
                continue;
            }
            string typeNamespace = reader.GetString(definition.Namespace);
            if (!namespaces.Contains(typeNamespace) ||
                generated.GetType(typeNamespace + "." + reader.GetString(definition.Name)) is not { } type)
            {
                continue;
            }
            sources.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "ReadRelease"));
        }
        return sources;
    }
}
