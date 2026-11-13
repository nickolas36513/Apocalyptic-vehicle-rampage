
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct SwarmSpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SwarmSpawner>();
        state.RequireForUpdate<SwarmTargetPosition>();
    }

    public void OnUpdate(ref SystemState state)
    {
        SwarmTargetPosition target =
            SystemAPI.GetSingleton<SwarmTargetPosition>();

        EntityCommandBuffer ecb =
            new EntityCommandBuffer(Allocator.Temp);

        foreach (var (spawnerRef, spawnerEntity) in
                 SystemAPI.Query<RefRO<SwarmSpawner>>()
                     .WithNone<SwarmSpawned>()
                     .WithEntityAccess())
        {
            SwarmSpawner spawner = spawnerRef.ValueRO;

            if (spawner.EnemyPrefab == Entity.Null ||
                !state.EntityManager.Exists(spawner.EnemyPrefab))
            {
                continue;
            }

            int count = math.max(0, spawner.Count);

            // Mark this spawner so it cannot spawn again next update.
            ecb.AddComponent<SwarmSpawned>(spawnerEntity);

            for (int i = 0; i < count; i++)
            {
                float normalizedIndex =
                    (i + 0.5f) / math.max(1, count);

                // Golden-angle distribution avoids a simple grid.
                float angle = i * 2.39996323f;

                float spawnDistance =
                    spawner.SpawnRadius *
                    math.sqrt(normalizedIndex);

                float slotRadius = math.lerp(
                    spawner.MinSlotRadius,
                    spawner.MaxSlotRadius,
                    math.sqrt(normalizedIndex));

                float3 spawnOffset = new float3(
                    math.cos(angle) * spawnDistance,
                    0f,
                    math.sin(angle) * spawnDistance);

                float3 slotOffset = new float3(
                    math.cos(angle) * slotRadius,
                    0f,
                    math.sin(angle) * slotRadius);

                Entity zombie = ecb.Instantiate(
                    spawner.EnemyPrefab);

                ecb.SetComponent(
                    zombie,
                    LocalTransform.FromPositionRotationScale(
                        target.Value + spawnOffset,
                        quaternion.identity,
                        1f));

                ecb.SetComponent(zombie, new SwarmEnemy
                {
                    SlotOffset = slotOffset,
                    MoveSpeed = spawner.MoveSpeed
                });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
