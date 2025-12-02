using System;
using Unity.Collections;
using Unity.Netcode;

// Network-serializable player data
public struct NetworkPlayerInfo : INetworkSerializable, IEquatable<NetworkPlayerInfo>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public bool IsReady;
    public int CharacterIndex; // For future character selection

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CharacterIndex);
    }

    public bool Equals(NetworkPlayerInfo other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName) &&
               IsReady == other.IsReady &&
               CharacterIndex == other.CharacterIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkPlayerInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return System.HashCode.Combine(ClientId, PlayerName, IsReady, CharacterIndex);
    }
}