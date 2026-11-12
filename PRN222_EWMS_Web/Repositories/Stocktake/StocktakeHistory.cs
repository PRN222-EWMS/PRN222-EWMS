using System.Globalization;
using System.Text.Json;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Stocktaking;

/// <summary>Reads the audit snapshots emitted by the original triggers and by the current workflow.</summary>
internal static class StocktakeHistory
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = new DatabaseIdNamingPolicy()
    };

    public static async Task<List<StocktakeHandling>> HandlingAsync(this IStocktakeRepository repo, long planId, CancellationToken ct)
    {
        var rows = await repo.ReadStocktakeHistoryAsync(nameof(StocktakeHandling), planId, ct);
        return rows.Select(row =>
        {
            var handling = Read<StocktakeHandling>(row);
            handling.RowVersion = BitConverter.GetBytes(row.AuditId);
            return handling;
        }).ToList();
    }

    public static async Task<List<StocktakeInvestigation>> InvestigationsAsync(this IStocktakeRepository repo, long? planId, CancellationToken ct) =>
        (await repo.ReadStocktakeHistoryAsync(nameof(StocktakeInvestigation), planId, ct))
            .Select(Read<StocktakeInvestigation>).ToList();

    public static async Task<StocktakeInvestigation?> InvestigationForAsync(this IStocktakeRepository repo, long planId, CancellationToken ct) =>
        (await repo.InvestigationsAsync(planId, ct)).SingleOrDefault(i => i.InvestigationStocktakeId == planId);

    public static async Task<bool> HasOpenInvestigationAsync(this IStocktakeRepository repo, long planId, CancellationToken ct) =>
        (await repo.InvestigationsAsync(planId, ct)).Any(i => i.StocktakeId == planId && i.ClosedAt is null);

    public static async Task<bool> HasInvestigationRestrictionAsync(this IStocktakeRepository repo, long planId, CancellationToken ct) =>
        (await repo.InvestigationsAsync(planId, ct))
            .Any(i => i.InvestigationStocktakeId == planId || i.StocktakeId == planId && i.ClosedAt is null);

    public static void Record(this IStocktakeRepository repo, StocktakeHandling handling, int actorId, bool isNew) =>
        Append(repo, handling, handling.HandlingId, actorId, isNew, handling.Note);

    public static void Record(this IStocktakeRepository repo, StocktakeInvestigation investigation, int actorId, bool isNew) =>
        Append(repo, investigation, investigation.StocktakeInvestigationId, actorId, isNew, investigation.Conclusion);

    private static T Read<T>(AuditLog row) where T : class =>
        JsonSerializer.Deserialize<T>(row.AfterData!, Json)
        ?? throw new InvalidOperationException($"Invalid stocktake history in AuditLog row {row.AuditId}.");

    private static void Append<T>(IStocktakeRepository repo, T snapshot, long id, int actorId, bool isNew, string? reason)
    {
        repo.Add(new AuditLog
        {
            TableName = typeof(T).Name,
            EntityKey = id.ToString(CultureInfo.InvariantCulture),
            DatabasePrincipal = "EWMS",
            ActorUserId = actorId,
            ActionType = isNew ? "INSERT" : "UPDATE",
            OccurredAt = DateTime.UtcNow,
            AfterData = JsonSerializer.Serialize(snapshot, Json),
            Reason = reason
        });
    }

    private sealed class DatabaseIdNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) =>
            name.EndsWith("Id", StringComparison.Ordinal) ? name[..^2] + "ID" : name;
    }
}
