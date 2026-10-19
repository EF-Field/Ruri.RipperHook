using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MonoMod.Core.Platforms;
using Ruri.Hook.Attributes;
using Ruri.Hook.Utils;

namespace Ruri.Hook.Core
{
    /// <summary>
    /// A detour is seen by every caller that calls the method and by none the JIT inlined it into, and no runtime can
    /// un-inline one (MonoMod's RuntimeFeature.Uninlining). A host that switches game in a live process applies the next
    /// game's hooks after the last game's loads have already optimized the callers, so a method a hook retargets must be
    /// non-inlinable before ANY caller can be compiled -- not when its own hook is applied. Every method any hook declares
    /// is therefore reserved once, ahead of the first hook.
    /// </summary>
    public static class HookInlining
    {
        private static readonly object SyncRoot = new();
        private static readonly HashSet<MethodBase> Reserved = new();
        private static readonly HashSet<Assembly> Scanned = new();

        /// <summary>
        /// Reserve everything the hook-declaring assemblies loaded so far declare as retarget sources, by attribute and by
        /// <see cref="RetargetSourcesAttribute"/>. Idempotent: an assembly is scanned once, so a module loaded later is
        /// covered by the next call.
        /// </summary>
        public static void ReserveDeclared()
        {
            lock (SyncRoot)
            {
                int before = Reserved.Count;
                Stopwatch clock = Stopwatch.StartNew();
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!DeclaresHooks(assembly) || !Scanned.Add(assembly))
                    {
                        continue;
                    }
                    foreach (Type type in HookCatalog.TypesOf(assembly))
                    {
                        ReserveDeclaredBy(type);
                    }
                }
                if (Reserved.Count != before)
                {
                    HookLogger.Log($"{Reserved.Count - before} upstream method(s) reserved against inlining in {clock.ElapsedMilliseconds} ms, so a hook applied later still reaches every caller.");
                }
            }
        }

        /// <summary>
        /// A hook is about to retarget this method. Reserve it if nothing did ahead of time, and say so when it was late:
        /// a caller compiled before now may already have inlined it and will not see the hook.
        /// </summary>
        public static void Require(MethodBase source)
        {
            if (!Forbid(source))
            {
                HookLogger.LogWarning(
                    $"{source.DeclaringType?.FullName}.{source.Name} was hooked without being reserved against inlining first, " +
                    "so a caller compiled before this hook may bypass it. Declare it by attribute, or from a [RetargetSources] method.");
            }
        }

        private static void ReserveDeclaredBy(Type type)
        {
            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(ReflectionExtensions.AnyBindFlag() | BindingFlags.DeclaredOnly);
            }
            catch
            {
                return;
            }

            foreach (MethodInfo method in methods)
            {
                if (method.IsDefined(typeof(RetargetSourcesAttribute), false))
                {
                    foreach (MethodBase source in DeclaredBy(method))
                    {
                        Forbid(source);
                    }
                }
            }
            foreach (MethodBase source in HookRegistry.DeclaredSources(methods))
            {
                Forbid(source);
            }
        }

        private static IEnumerable<MethodBase> DeclaredBy(MethodInfo provider)
        {
            try
            {
                return ((IEnumerable<MethodBase>)provider.Invoke(null, null)!).ToList();
            }
            catch (Exception exception)
            {
                HookLogger.LogWarning($"{provider.DeclaringType?.FullName}.{provider.Name} could not list its retarget sources: {(exception.InnerException ?? exception).Message}");
                return Array.Empty<MethodBase>();
            }
        }

        private static bool Forbid(MethodBase source)
        {
            lock (SyncRoot)
            {
                if (!Reserved.Add(source))
                {
                    return true;
                }
            }
            bool disabled;
            try
            {
                disabled = PlatformTriple.Current.TryDisableInlining(source);
            }
            catch (Exception exception)
            {
                HookLogger.LogWarning($"{source.DeclaringType?.FullName}.{source.Name} could not be reserved against inlining: {exception.Message}");
                return false;
            }
            if (!disabled)
            {
                HookLogger.LogWarning($"{source.DeclaringType?.FullName}.{source.Name} cannot be reserved against inlining on this runtime.");
            }
            return false;
        }

        private static bool DeclaresHooks(Assembly assembly)
        {
            return assembly == typeof(HookInlining).Assembly ||
                assembly.GetReferencedAssemblies().Any(static reference => reference.Name == "Ruri.Hook");
        }
    }
}
