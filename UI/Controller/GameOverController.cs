using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GameOverController : UIScreenController
{
    private Button mainMenuButton;

    protected override void OnBindUI()
    {
        if (!TryGetElement("main-menu-button", out mainMenuButton))
            throw new InvalidOperationException("Game Over is missing its Main Menu button.");

        mainMenuButton.clicked += ReturnToMainMenu;
    }

    protected override void OnUnbindUI()
    {
        if (mainMenuButton != null)
            mainMenuButton.clicked -= ReturnToMainMenu;
    }

    private void ReturnToMainMenu()
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader == null || bootloader.StateMachine?.CurrentState != GameState.Transitioning)
            return;

        PlayerManager player = PlayerManager.Instance;
        if (player != null && player.HasVehicle && !player.ReturnToMainMenuState())
        {
            Debug.LogError("Could not reset the vehicle for Main Menu.", this);
            return;
        }

        if (!bootloader.UI.Show("MainMenu", out string error))
        {
            Debug.LogError($"Could not open Main Menu: {error}", this);
            return;
        }

        if (bootloader.ChangeState(GameState.Ready))
            bootloader.UI.Hide("GameOver");
        else
            bootloader.UI.Hide("MainMenu");
    }
}
