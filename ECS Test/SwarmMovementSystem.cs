
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SwarmMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SwarmTargetPosition>();
        state.RequireForUpdate<SwarmEnemy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SwarmTargetPosition target =
            SystemAPI.GetSingleton<SwarmTargetPosition>();

        var job = new SwarmMovementJob
        {
            TargetPosition = target.Value,
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    private partial struct SwarmMovementJob : IJobEntity
    {
        public float3 TargetPosition;
        public float DeltaTime;

        private void Execute(
            ref LocalTransform transform,
            in SwarmEnemy enemy)
        {
            float3 destination =
                TargetPosition + enemy.SlotOffset;

            float3 direction = destination - transform.Position;

            // Keep movement on the horizontal ground plane.
            direction.y = 0f;

            float distanceSq = math.lengthsq(direction);

            if (distanceSq < 0.0001f)
                return;

            float distance = math.sqrt(distanceSq);
            float3 normalizedDirection = direction / distance;

            float step = math.max(0f, enemy.MoveSpeed) * DeltaTime;
            step = math.min(step, distance);

            transform.Position += normalizedDirection * step;

            quaternion targetRotation =
                quaternion.LookRotationSafe(
                    normalizedDirection,
                    math.up());

            transform.Rotation = math.slerp(
                transform.Rotation,
                targetRotation,
                math.saturate(8f * DeltaTime));
        }
    }
}
