using System;

namespace Ruri.RipperHook.Attributes;

/// <summary>
/// One game's decoder. <paramref name="version"/> is the game's own bundleVersion that this
/// decoder applies FROM: a build is read by the newest decoder at or below its version, so a
/// patch that changes nothing needs no edit here and a patch that breaks something gets its own
/// class. <paramref name="engineVersion"/> is the Unity version those builds report, which the
/// panel checks the install against.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RipperHookAttribute : GameHookAttribute
{
    public GameType GameType { get; }
    public override string GameName { get; }
    public override string Version { get; }
    public override string EngineVersion { get; }

    public RipperHookAttribute(GameType gameType, string version = "", string engineVersion = "")
        : this(gameType.ToString(), version, engineVersion)
    {
        GameType = gameType;
    }

    /// <summary>Register the install's productName without extending the kernel's legacy enum.</summary>
    public RipperHookAttribute(string gameName, string version = "", string engineVersion = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        GameName = gameName;
        GameType = Enum.TryParse<GameType>(gameName, out var known) && Enum.IsDefined(known)
            ? known : GameType.Unknown;
        Version = version;
        EngineVersion = engineVersion;
    }
}
