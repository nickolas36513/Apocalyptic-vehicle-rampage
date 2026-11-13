
public enum PurchaseStatus
{
    Success,
    ProfileNotLoaded,
    InvalidItem,
    NotPurchasable,
    AlreadyOwned,
    InsufficientFunds,
    SaveFailed,
    NotOwned,
    NotAVehicle
}

public readonly struct PurchaseResult
{
    public PurchaseStatus Status { get; }
    public string Message { get; }

    public bool Success => Status == PurchaseStatus.Success;

    public PurchaseResult(
        PurchaseStatus status,
        string message)
    {
        Status = status;
        Message = message;
    }

    public static PurchaseResult Succeeded(string message)
    {
        return new PurchaseResult(
            PurchaseStatus.Success,
            message);
    }

    public static PurchaseResult Failed(
        PurchaseStatus status,
        string message)
    {
        return new PurchaseResult(status, message);
    }
}
