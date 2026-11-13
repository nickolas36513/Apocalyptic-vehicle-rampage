
using Unity.Entities;
using Unity.Mathematics;

public struct SwarmEnemy : IComponentData
{
    public float3 SlotOffset;
    public float MoveSpeed;
}

public struct SwarmSpawner : IComponentData
{
    public Entity EnemyPrefab;
    public int Count;
    public float SpawnRadius;
    public float MinSlotRadius;
    public float MaxSlotRadius;
    public float MoveSpeed;
}

public struct SwarmSpawned : IComponentData
{
}

// Written by the GameObject bridge.
// Exactly one target bridge should be active in this prototype.
public struct SwarmTargetPosition : IComponentData
{
    public float3 Value;
}
