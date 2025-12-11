using System;
using Unity.Collections;
using Unity.Netcode;

public struct NetworkPlayerInfo : INetworkSerializable, IEquatable<NetworkPlayerInfo>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public bool IsReady;
    public int CharacterIndex;

    // Serialize network data
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CharacterIndex);
    }

    // Check equality with another NetworkPlayerInfo
    public bool Equals(NetworkPlayerInfo other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName) &&
               IsReady == other.IsReady &&
               CharacterIndex == other.CharacterIndex;
    }

    // Check equality with any object
    public override bool Equals(object obj)
    {
        return obj is NetworkPlayerInfo other && Equals(other);
    }

    // Generate hash code
    public override int GetHashCode()
    {
        return System.HashCode.Combine(ClientId, PlayerName, IsReady, CharacterIndex);
    }
}