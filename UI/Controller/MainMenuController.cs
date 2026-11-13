using UnityEngine;
using UnityEngine.UIElements;

public sealed class MainMenuController : UIScreenController
{
    private Button playButton;
    private Button garageButton;
    private Button shopButton;
    private Button settingsButton;
    private Button quitButton;
    private Label currencyLabel;

    protected override void OnBindUI()
    {
        if (!TryGetElement("play-button", out playButton) ||
            !TryGetElement("garage-button", out garageButton) ||
            !TryGetElement("shop-button", out shopButton) ||
            !TryGetElement("settings-button", out settingsButton) ||
            !TryGetElement("quit-button", out quitButton))
        {
            Debug.LogError(
                "MainMenuController: One or more required UI elements are missing.");

            return;
        }

        TryGetElement("currency-label", out currencyLabel);
        playButton.clicked += HandlePlay;
        garageButton.clicked += HandleGarage;
        shopButton.clicked += HandleShop;
        settingsButton.clicked += OnSettingsClicked;
        quitButton.clicked += OnQuitClicked;
    }

    protected override void OnUnbindUI()
    {
        if (playButton != null)
            playButton.clicked -= HandlePlay;

        if (garageButton != null)
            garageButton.clicked -= HandleGarage;

        if (shopButton != null)
            shopButton.clicked -= HandleShop;

        if (settingsButton != null)
            settingsButton.clicked -= OnSettingsClicked;

        if (quitButton != null)
            quitButton.clicked -= OnQuitClicked;

        playButton = null;
        garageButton = null;
        shopButton = null;
        settingsButton = null;
        quitButton = null;
        currencyLabel = null;
    }

    protected override void OnShown()
    {
        PlayerProfileRuntimeService profiles = Bootloader.Instance?.PlayerProfiles;
        if (currencyLabel != null)
            currencyLabel.text = profiles != null && profiles.IsInitialized
                ? $"◉  {profiles.Purchases.Currency:N0}"
                : "◉  —";
        PlayerManager.Instance?.ShowMainMenuCamera();
    }

    private void HandlePlay()
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader == null || !bootloader.IsInitialized ||
            bootloader.StateMachine.CurrentState != GameState.Ready ||
            bootloader.StateMachine.IsTransitioning)
        {
            Debug.LogError("Cannot start gameplay until startup is ready.", this);
            return;
        }

        PlayerManager player = PlayerManager.Instance;
        if (player == null || !player.StartGameplay())
        {
            Debug.LogError("Cannot start gameplay because the player vehicle is unavailable.", this);
            return;
        }

        if (!bootloader.ChangeState(GameState.Running))
        {
            player.ParkForMenu();
            player.ShowMainMenuCamera();
            Debug.LogError("Could not enter the Running state.", this);
            return;
        }

        bootloader.UI.Hide("MainMenu");
    }
    private void HandleGarage()
    {
        if (!Bootloader.Instance.UI.Show("Garage", out string error))
        {
            Debug.LogError($"Could not open Garage: {error}");
        }
    }
    private void HandleShop()
    {
        if (!Bootloader.Instance.UI.Show("Shop", out string error))
        {
            Debug.LogError($"Could not open Shop: {error}");
        }
    }
    private void OnSettingsClicked()
    {
    }

    private async void OnQuitClicked()
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader != null)
            await bootloader.ShutdownAsync();

        Application.Quit();
    }
}
