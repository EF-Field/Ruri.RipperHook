using MonoMod.RuntimeDetour;
using Ruri.Hook.Core;
using Ruri.Hook.Utils;
using Ruri.RipperHook.Core;

namespace Ruri.RipperHook
{
    public static class RuriRuntimeHook
    {
        public static List<ILHook> ilHooks = new List<ILHook>();

        private static readonly object LoadedGameHooksSyncRoot = new();
        private static readonly Dictionary<string, HashSet<string>> LoadedGameHooks = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsGameLoaded(GameType gameType)
            => gameType != GameType.Unknown && IsGameLoaded(gameType.ToString());

        public static bool IsGameLoaded(string gameName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
            lock (LoadedGameHooksSyncRoot)
            {
                foreach (HashSet<string> gameTypes in LoadedGameHooks.Values)
                {
                    if (gameTypes.Contains(gameName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void RegisterLoadedGameHook(GameType gameType)
        {
            if (gameType == GameType.Unknown)
            {
                return;
            }

            RegisterLoadedGameHook(gameType.ToString());
        }

        public static void RegisterLoadedGameHook(string gameName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
            if (string.Equals(gameName, nameof(GameType.Unknown), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string scopeId = HookManager.CurrentScopeId ?? string.Empty;
            bool added;
            lock (LoadedGameHooksSyncRoot)
            {
                if (!LoadedGameHooks.TryGetValue(scopeId, out HashSet<string>? gameTypes))
                {
                    gameTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    LoadedGameHooks[scopeId] = gameTypes;
                }

                added = gameTypes.Add(gameName);
            }

            if (added)
            {
                HookManager.RegisterCleanup(() => RemoveLoadedGameHook(scopeId, gameName));
            }
        }

        public static void DisposeAll()
        {
            HookManager.DisposeAll();
            global::Ruri.Hook.RuriHook.ClearAppliedHooks();
            ReflectionExtensions.ClearAppliedHookGuards();
            HookDispatcher.Clear();
            lock (LoadedGameHooksSyncRoot)
            {
                LoadedGameHooks.Clear();
            }

            foreach (var hook in ilHooks)
            {
                hook.Dispose();
            }
            ilHooks.Clear();
        }

        private static void RemoveLoadedGameHook(string scopeId, string gameName)
        {
            lock (LoadedGameHooksSyncRoot)
            {
                if (!LoadedGameHooks.TryGetValue(scopeId, out HashSet<string>? gameTypes))
                {
                    return;
                }

                gameTypes.Remove(gameName);
                if (gameTypes.Count == 0)
                {
                    LoadedGameHooks.Remove(scopeId);
                }
            }
        }
    }
}
