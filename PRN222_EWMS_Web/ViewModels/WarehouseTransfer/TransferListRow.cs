namespace PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

public sealed record TransferListRow(
    long Id,
    string Code,
    string FromWarehouse,
    string ToWarehouse,
    bool IsSource,
    string Status,
    int Dispatched,
    int Received,
    int InTransit)
{
    public bool HasRemainingToDispatch { get; init; }
    public int OpenCount { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public int ViewerId { get; init; }
    public bool IsDraft { get; init; }
    public bool IsReleased => Status is "RELEASED" or "IN_PROGRESS";
    public bool CanClaim => IsReleased && (IsSource ? HasRemainingToDispatch : InTransit > 0)
        && (OpenCount == 0 || (OpenCount == 1 && OwnerId == null));
    public bool CanContinue => IsReleased && OpenCount == 1 && IsDraft && OwnerId == ViewerId;
}
