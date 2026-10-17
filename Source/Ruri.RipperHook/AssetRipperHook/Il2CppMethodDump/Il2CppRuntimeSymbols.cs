using System;
using System.Collections.Generic;
using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Ruri.RipperHook.AR;

/// <summary>
/// The code the IL2CPP runtime reaches through the registration tables instead of through a call: the invoker behind
/// every reflected call, the generator behind every custom attribute, the stub behind every virtual call the compiler
/// could not resolve, the wrapper behind every reverse-P/Invoke and each module's initializer. A method listing never
/// calls them, so they surface only as table entries and as the targets a table's pointers name.
/// </summary>
internal static class Il2CppRuntimeSymbols
{
    private static ApplicationAnalysisContext _builtFor;
    private static List<(ulong Address, string Kind, string Name)> _entries;
    private static Dictionary<ulong, string> _names;

    public static IReadOnlyList<(ulong Address, string Kind, string Name)> All(ApplicationAnalysisContext app)
    {
        Ensure(app);
        return _entries;
    }

    public static string NameAt(ApplicationAnalysisContext app, ulong address)
    {
        Ensure(app);
        return _names.TryGetValue(address, out string name) ? name : null;
    }

    private static void Ensure(ApplicationAnalysisContext app)
    {
        if (ReferenceEquals(_builtFor, app))
        {
            return;
        }
        List<(ulong, string, string)> entries = new();
        Il2CppBinary binary = app.Binary;

        foreach (Il2CppImageDefinition image in app.Metadata.imageDefinitions)
        {
            Il2CppCodeGenModule module = binary.GetCodegenModuleByName(image.Name);
            if (module is null || module.customAttributeCacheGenerator == 0 || image.customAttributeCount == 0)
            {
                continue;
            }
            AddPointers(entries, binary, module.customAttributeCacheGenerator, image.customAttributeCount,
                "attributeGenerator", "CustomAttributesCacheGenerator_" + image.Name + "_");
        }

        if (typeof(Il2CppBinary).GetField("_codeRegistration", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(binary) is Il2CppCodeRegistration registration)
        {
            AddPointers(entries, binary, registration.invokerPointers, registration.invokerPointersCount, "invoker", "Invoker_");
            AddPointers(entries, binary, registration.unresolvedVirtualCallPointers, registration.unresolvedVirtualCallCount,
                "unresolvedVirtualCall", "UnresolvedVirtualCall_");
            AddPointers(entries, binary, registration.reversePInvokeWrappers, registration.reversePInvokeWrapperCount,
                "reversePInvokeWrapper", "ReversePInvokeWrapper_");
        }

        foreach (AssemblyAnalysisContext assembly in app.Assemblies)
        {
            if (assembly.CodeGenModule is { moduleInitializer: not 0 } module)
            {
                entries.Add((module.moduleInitializer, "moduleInitializer", "ModuleInitializer_" + assembly.CleanAssemblyName));
            }
        }

        Dictionary<ulong, string> names = new();
        foreach ((ulong address, _, string name) in entries)
        {
            if (address != 0)
            {
                names.TryAdd(address, name);
            }
        }
        _entries = entries;
        _names = names;
        _builtFor = app;
    }

    private static void AddPointers(List<(ulong, string, string)> entries, Il2CppBinary binary, ulong table, ulong count,
        string kind, string prefix)
    {
        if (table == 0 || count == 0)
        {
            return;
        }
        ulong[] pointers = binary.ReadNUintArrayAtVirtualAddress(table, (long)count);
        for (int index = 0; index < pointers.Length; index++)
        {
            entries.Add((pointers[index], kind, prefix + index));
        }
    }
}
