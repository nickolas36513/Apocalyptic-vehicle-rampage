
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SwarmTargetBridge : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    private World targetWorld;
    private Entity targetEntity = Entity.Null;

    private void Start()
    {
        EnsureTargetEntity();
        PushPosition();
    }

    private void LateUpdate()
    {
        EnsureTargetEntity();
        PushPosition();
    }

    private void EnsureTargetEntity()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return;

        // Recreate the bridge entity if the ECS world changed.
        if (targetWorld == world &&
            targetEntity != Entity.Null &&
            targetWorld.EntityManager.Exists(targetEntity))
        {
            return;
        }

        DestroyTargetEntity();

        targetWorld = world;

        EntityManager entityManager = targetWorld.EntityManager;
        targetEntity = entityManager.CreateEntity(
            typeof(SwarmTargetPosition));

        entityManager.SetComponentData(
            targetEntity,
            new SwarmTargetPosition
            {
                Value = (float3)transform.TransformPoint(localOffset)
            });
    }

    private void PushPosition()
    {
        if (targetWorld == null ||
            !targetWorld.IsCreated ||
            targetEntity == Entity.Null)
        {
            return;
        }

        EntityManager entityManager = targetWorld.EntityManager;

        if (!entityManager.Exists(targetEntity))
        {
            targetEntity = Entity.Null;
            return;
        }

        entityManager.SetComponentData(
            targetEntity,
            new SwarmTargetPosition
            {
                Value = (float3)transform.TransformPoint(localOffset)
            });
    }

    private void OnDisable()
    {
        DestroyTargetEntity();
    }

    private void DestroyTargetEntity()
    {
        if (targetWorld != null &&
            targetWorld.IsCreated &&
            targetEntity != Entity.Null)
        {
            EntityManager entityManager = targetWorld.EntityManager;

            if (entityManager.Exists(targetEntity))
                entityManager.DestroyEntity(targetEntity);
        }

        targetEntity = Entity.Null;
        targetWorld = null;
    }
}
