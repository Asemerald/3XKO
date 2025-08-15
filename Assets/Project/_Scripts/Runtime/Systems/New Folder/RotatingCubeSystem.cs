using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct RotatingCubeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RotateSpeed>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        var job = new RotatingCubeJob
        {
            DeltaTime = deltaTime
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
    
    [BurstCompile]
    public partial struct RotatingCubeJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref LocalTransform localTransform, in RotateSpeed rotateSpeed)
        {
            localTransform = localTransform.RotateY(rotateSpeed.Value * DeltaTime);
        }
    }
}
