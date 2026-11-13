using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerSetupController : UIScreenController
{
    private TextField playerNameField;
    private Label profileNameLabel;
    private Label descriptionLabel;
    private Label errorLabel;
    private Image portraitImage;

    private VisualElement nameFieldContainer;
    private Button[] profileButtons;
    private Button confirmButton;

    private IReadOnlyList<CharacterDefinition> profiles;
    private PlayerProfileSelectionService selection;

    private string selectedProfileId;
    private TaskCompletionSource<bool> completion;
    private CancellationTokenRegistration cancellationRegistration;

    protected override void OnBindUI()
    {
        playerNameField = Get<TextField>("player-name");
        profileNameLabel = Get<Label>("profile-name");
        descriptionLabel = Get<Label>("profile-description");
        errorLabel = Get<Label>("error-label");
        portraitImage = Get<Image>("profile-portrait");

        nameFieldContainer = Get<VisualElement>("setup-root")
            .Q<VisualElement>(className: "name-field-container");

        confirmButton = Get<Button>("confirm-button");

        profileButtons = new[]
        {
            Get<Button>("mechanic-button"),
            Get<Button>("recon-button"),
            Get<Button>("carnage-button")
        };

        profileButtons[0].clicked += SelectMechanic;
        profileButtons[1].clicked += SelectRecon;
        profileButtons[2].clicked += SelectCarnage;

        confirmButton.clicked += Confirm;

        playerNameField.RegisterCallback<FocusInEvent>(OnNameFocusIn);
        playerNameField.RegisterCallback<FocusOutEvent>(OnNameFocusOut);
    }

    public Task<bool> BeginSetupAsync(
        PlayerProfileSelectionService profileSelection,
        CancellationToken token)
    {
        if (profileSelection == null)
            throw new System.ArgumentNullException(nameof(profileSelection));

        if (completion != null && !completion.Task.IsCompleted)
            return completion.Task;

        selection = profileSelection;
        profiles = selection.Profiles;

        if (profiles == null || profiles.Count != 3)
        {
            throw new System.InvalidOperationException(
                "Exactly three player profiles are required by the setup screen.");
        }

        cancellationRegistration.Dispose();

        var newCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        completion = newCompletion;

        playerNameField.SetValueWithoutNotify(profileSelection.PlayerName);

        errorLabel.text = string.Empty;

        int initialIndex = 0;
        for (int i = 0; i < profiles.Count; i++)
        {
            if (profiles[i].DefinitionId == profileSelection.SelectedCharacterId)
            {
                initialIndex = i;
                break;
            }
        }
        SelectProfileAt(initialIndex);

        cancellationRegistration = token.Register(
            () => newCompletion.TrySetResult(false));

        return newCompletion.Task;
    }

    protected override void OnUnbindUI()
    {
        completion?.TrySetResult(false);

        cancellationRegistration.Dispose();
        cancellationRegistration = default;

        if (profileButtons != null)
        {
            profileButtons[0].clicked -= SelectMechanic;
            profileButtons[1].clicked -= SelectRecon;
            profileButtons[2].clicked -= SelectCarnage;
        }

        if (confirmButton != null)
            confirmButton.clicked -= Confirm;

        if (playerNameField != null)
        {
            playerNameField.UnregisterCallback<FocusInEvent>(OnNameFocusIn);
            playerNameField.UnregisterCallback<FocusOutEvent>(OnNameFocusOut);
        }

        selection = null;
        profiles = null;
        profileButtons = null;
        confirmButton = null;
        playerNameField = null;
        nameFieldContainer = null;
        selectedProfileId = null;
    }

    private T Get<T>(string elementName) where T : VisualElement
    {
        if (!TryGetElement(elementName, out T element))
        {
            throw new System.InvalidOperationException(
                $"PlayerSetup screen is missing '{elementName}'.");
        }

        return element;
    }

    private void SelectProfileAt(int index)
    {
        if (profiles == null || index < 0 || index >= profiles.Count)
            return;

        CharacterDefinition profile = profiles[index];

        if (profile == null)
            return;

        selectedProfileId = profile.DefinitionId;

        profileNameLabel.text = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.ArchetypeLabel
            : profile.DisplayName;

        descriptionLabel.text = profile.Description ?? string.Empty;

        portraitImage.image = profile.Portrait != null
            ? profile.Portrait.texture
            : null;

        for (int i = 0; i < profileButtons.Length; i++)
        {
            profileButtons[i].EnableInClassList(
                "selected",
                i == index);
        }

        errorLabel.text = string.Empty;
    }

    private void Confirm()
    {
        if (selection == null ||
            completion == null ||
            completion.Task.IsCompleted)
        {
            return;
        }

        if (!selection.TrySelectProfile(
            playerNameField.value,
            selectedProfileId,
            out string error))
        {
            errorLabel.text = error;
            return;
        }

        cancellationRegistration.Dispose();
        completion.TrySetResult(true);
    }

    private void OnNameFocusIn(FocusInEvent evt)
    {
        nameFieldContainer?.EnableInClassList("focused", true);
    }

    private void OnNameFocusOut(FocusOutEvent evt)
    {
        nameFieldContainer?.EnableInClassList("focused", false);
    }

    private void SelectMechanic() => SelectProfileAt(0);
    private void SelectRecon() => SelectProfileAt(1);
    private void SelectCarnage() => SelectProfileAt(2);
}
