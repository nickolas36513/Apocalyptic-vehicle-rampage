
using Unity.Entities;
using UnityEngine;

public sealed class SwarmEnemyAuthoring : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float moveSpeed = 2.2f;

    private sealed class Baker : Baker<SwarmEnemyAuthoring>
    {
        public override void Bake(SwarmEnemyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SwarmEnemy
            {
                SlotOffset = Unity.Mathematics.float3.zero,
                MoveSpeed = authoring.moveSpeed
            });
        }
    }
}
