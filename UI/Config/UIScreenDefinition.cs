using System;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public struct UIScreenDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string groupId;
    [SerializeField] private VisualTreeAsset sourceAsset;
    [SerializeField] private UIScreenController controllerPrefab;

    public string ID => id;

    public string GroupID =>
        string.IsNullOrWhiteSpace(groupId) ? "Default" : groupId;

    public VisualTreeAsset SourceAsset => sourceAsset;

    public UIScreenController ControllerPrefab => controllerPrefab;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(id) &&
        sourceAsset != null &&
        controllerPrefab != null;
}