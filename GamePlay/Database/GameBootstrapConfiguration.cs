using UnityEngine;

[CreateAssetMenu(
    fileName = "GameBootstrapConfiguration",
    menuName = "Game/Configuration/Bootstrap")]
public sealed class GameBootstrapConfiguration : ScriptableObject
{
    [SerializeField] private GameDefinitionRegistry definitions;
    [Min(0)]
    [SerializeField] private int startingCurrency = 1000;

    public GameDefinitionRegistry Definitions => definitions;
    public int StartingCurrency => Mathf.Max(0, startingCurrency);

#if UNITY_EDITOR
    private void OnValidate()
    {
        startingCurrency = Mathf.Max(0, startingCurrency);
    }
#endif
}
