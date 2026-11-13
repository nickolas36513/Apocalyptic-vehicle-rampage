
using Unity.Entities;
using UnityEngine;

public sealed class SwarmSpawnerAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn")]
    [SerializeField, Min(0)] private int count = 40;
    [SerializeField, Min(0f)] private float spawnRadius = 12f;

    [Header("Swarm Formation")]
    [SerializeField, Min(0f)] private float minSlotRadius = 1.5f;
    [SerializeField, Min(0f)] private float maxSlotRadius = 4f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.2f;

    private sealed class Baker : Baker<SwarmSpawnerAuthoring>
    {
        public override void Bake(SwarmSpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new SwarmSpawner
            {
                EnemyPrefab = authoring.enemyPrefab != null
                    ? GetEntity(
                        authoring.enemyPrefab,
                        TransformUsageFlags.Dynamic)
                    : Entity.Null,

                Count = authoring.count,
                SpawnRadius = authoring.spawnRadius,
                MinSlotRadius = authoring.minSlotRadius,
                MaxSlotRadius = authoring.maxSlotRadius,
                MoveSpeed = authoring.moveSpeed
            });
        }
    }
}
