using System;

namespace PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

public record EligibleSalesOrderOptionDto(
    long SalesOrderId,
    string OrderCode,
    string CustomerName,
    DateTime OrderDate,
    int DefaultWarehouseId,
    int EligibleLinesCount
);

public record EligibleSalesOrderLineDto(
    long SolineId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string? Unit,
    int ShippedQuantity,
    int PreviouslyReturnedQuantity,
    int OutstandingCommittedQuantity,
    int EligibleQuantity
);

public record WarehouseOptionDto(
    int WarehouseId,
    string WarehouseName,
    string Status
);

