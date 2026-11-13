
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

[DisallowMultipleComponent]
public sealed class VehicleMemoryDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool captureMemory = true;
    [SerializeField] private bool logToConsole;
    [SerializeField, Min(0.25f)] private float logInterval = 2f;

    private ProfilerRecorder gcAllocatedRecorder;
    private float timer;

    private void OnEnable()
    {
        timer = 0f;

#if UNITY_2020_2_OR_NEWER
        if (captureMemory)
        {
            gcAllocatedRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                15);
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_2020_2_OR_NEWER
        if (gcAllocatedRecorder.Valid)
            gcAllocatedRecorder.Dispose();
#endif
    }

    private void Update()
    {
        if (!captureMemory || !logToConsole)
            return;

        timer += Time.unscaledDeltaTime;

        if (timer < logInterval)
            return;

        timer = 0f;

        // These are process-wide Unity memory measurements,
        // not memory used exclusively by the vehicle.
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long totalReserved = Profiler.GetTotalReservedMemoryLong();
        long unusedReserved = Profiler.GetTotalUnusedReservedMemoryLong();

        long gcAllocated = -1;

#if UNITY_2020_2_OR_NEWER
        if (gcAllocatedRecorder.Valid)
            gcAllocated = gcAllocatedRecorder.LastValue;
#endif

        string gcText = gcAllocated >= 0
            ? $"{gcAllocated / 1024f:F2} KB"
            : "Unavailable";

        Debug.Log(
            $"[VehicleMemoryDebugger]\n" +
            $"GC Allocated (recorded frame): {gcText}\n" +
            $"Mono Used: {ToMB(monoUsed):F2} MB\n" +
            $"Unity Allocated: {ToMB(totalAllocated):F2} MB\n" +
            $"Unity Reserved: {ToMB(totalReserved):F2} MB\n" +
            $"Unused Reserved: {ToMB(unusedReserved):F2} MB");
    }

    private static float ToMB(long bytes)
    {
        return bytes / (1024f * 1024f);
    }
}
