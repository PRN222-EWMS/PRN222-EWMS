namespace PRN222_EWMS_Web.ViewModels.Returns;
public record ReturnOrderRow(long Id,string Code,string SalesCode,string Customer,string Status,int Requested,int Received,List<ReturnOutboundOption> Outbounds);
public record ReturnOutboundOption(long Id,string Code);
public record ReturnReceiptRow(long Id,string Code,string ReturnCode,string Status,int Scanned,DateTime CreatedAt);
public record ReturnSerialRow(long Id,string Serial,string Product,string Condition,string? Note,bool Active,byte? ManufacturingMonth,short? ManufacturingYear,string SoldCondition,string? PreviousNote);
public record ReturnSerialPreview(string Serial,string Product,byte? ManufacturingMonth,short? ManufacturingYear,string SoldCondition,string? PreviousNote);
public class ReturnReceivePage
{
 public List<ReturnOrderRow> Orders {get;set;}=[];
 public List<ReturnReceiptRow> Receipts {get;set;}=[];
 public int Page {get;set;}=1;
 public int TotalPages {get;set;}=1;
 public string? Search {get;set;}
}
public class ReturnReceiveDetail
{
 public long Id {get;set;}
 public long ReturnOrderId {get;set;}
 public string Code {get;set;}="";
 public string ReturnCode {get;set;}="";
 public string SalesCode {get;set;}="";
 public string Customer {get;set;}="";
 public string Status {get;set;}="";
 public string Version {get;set;}="";
 public int Requested {get;set;}
 public int Received {get;set;}
 public string? Note {get;set;}
 public List<ReturnSerialRow> Serials {get;set;}=[];
}
