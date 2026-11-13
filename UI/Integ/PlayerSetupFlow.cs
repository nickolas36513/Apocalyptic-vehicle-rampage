using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Connects profile preparation to the setup screen during application startup.</summary>
internal static class PlayerSetupFlow
{
    private const string SetupScreenID = "PlayerSetup";

    public static async Task PrepareAsync(
        PlayerProfileSelectionService selection,
        UIScreenRegistry ui,
        string loadingScreenID,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!selection.TryPrepareForGameplay(out bool requiresSetup, out string error))
            throw new InvalidOperationException(error);

        if (!requiresSetup)
            return;

        if (!ui.Show(SetupScreenID, out error))
            throw new InvalidOperationException("Could not show player setup. " + error);

        if (!ui.TryGetController<PlayerSetupController>(SetupScreenID, out var controller))
            throw new InvalidOperationException("Player setup controller is unavailable.");

        bool completed = await controller.BeginSetupAsync(selection, token);
        token.ThrowIfCancellationRequested();
        if (!completed)
            throw new InvalidOperationException("Player setup closed before confirmation.");

        if (!ui.Show(loadingScreenID, out error))
            throw new InvalidOperationException("Could not restore the loading screen. " + error);
    }
}
