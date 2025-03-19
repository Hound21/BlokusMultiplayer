using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public struct Vector2IntList : INetworkSerializable
{
    public List<Vector2Int> Values;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(Values.Count);
            foreach (var value in Values)
            {
                serializer.GetFastBufferWriter().WriteValueSafe(value);
            }
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out int count);
            Values = new List<Vector2Int>(count);
            for (int i = 0; i < count; i++)
            {
                serializer.GetFastBufferReader().ReadValueSafe(out Vector2Int value);
                Values.Add(value);
            }
        }
    }
}
