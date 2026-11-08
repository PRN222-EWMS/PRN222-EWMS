namespace PRN222_EWMS_Web.ViewModels.Shared;

public class SerialCaptureViewModel
{
    public string SerialName { get; init; } = "Serial";
    public string MethodName { get; init; } = "CaptureMethod";
    public string Value { get; init; } = "";
    public string Method { get; init; } = "MANUAL";
    public string HelpText { get; init; } = "Quét hoặc đọc ảnh chỉ điền Serial. Đối chiếu mã trên tem trước khi tiếp tục.";
    public string Hint { get; init; } = "Nhập tay hoặc máy quét · Enter để tra cứu.";
    public string InputId => SerialName.Replace('.', '_');
}
