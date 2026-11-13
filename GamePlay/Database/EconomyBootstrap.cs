
using UnityEngine;

public sealed class EconomyBootstrap : MonoBehaviour
{
    private PlayerProfileRuntimeService RuntimeService =>
        Bootloader.Instance != null
            ? Bootloader.Instance.PlayerProfiles
            : null;

    public PlayerProfileStore ProfileStore =>
        RuntimeService != null ? RuntimeService.ProfileStore : null;

    public PurchaseService Purchases =>
        RuntimeService != null ? RuntimeService.Purchases : null;

    public bool IsInitialized =>
        RuntimeService != null && RuntimeService.IsInitialized;
}
