using Unity.Collections;
using Unity.Netcode;
using System;

[System.Serializable]
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public bool IsReady;
    public int CharacterIndex;

    public LobbyPlayerData(ulong clientId, string playerName, bool isReady, int characterIndex)
    {
        ClientId = clientId;
        PlayerName = playerName;
        IsReady = isReady;
        CharacterIndex = characterIndex;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CharacterIndex);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName) &&
               IsReady == other.IsReady &&
               CharacterIndex == other.CharacterIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayerData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ClientId.GetHashCode();
            hashCode = (hashCode * 397) ^ PlayerName.GetHashCode();
            hashCode = (hashCode * 397) ^ IsReady.GetHashCode();
            hashCode = (hashCode * 397) ^ CharacterIndex;
            return hashCode;
        }
    }

    public static bool operator ==(LobbyPlayerData left, LobbyPlayerData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LobbyPlayerData left, LobbyPlayerData right)
    {
        return !(left == right);
    }
}