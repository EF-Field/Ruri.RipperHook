using System;

namespace Ruri.RipperHook.Core.Capabilities;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GameCapabilitiesAttribute : Attribute
{
    public GameType GameType { get; }
    public string GameName { get; }

    public GameCapabilitiesAttribute(GameType gameType)
        : this(gameType.ToString())
    {
        GameType = gameType;
    }

    public GameCapabilitiesAttribute(string gameName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        GameName = gameName;
        GameType = Enum.TryParse<GameType>(gameName, out var known) && Enum.IsDefined(known)
            ? known : GameType.Unknown;
    }
}
