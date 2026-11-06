namespace PRN222_EWMS_Web.ViewModels;

public static class AccountValidation
{
    public const string PasswordPattern = @"^(?=[\s\S]*[A-Z])(?=[\s\S]*[0-9])(?=[\s\S]*[^A-Za-z0-9\s])[\s\S]+$";
    public const string PasswordLengthMessage = "Mật khẩu phải từ 11 đến 128 ký tự.";
    public const string PasswordComplexityMessage = "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ số và 1 ký tự đặc biệt.";
    public const string PhonePattern = @"^0[0-9]{9}$";
    public const string PhoneMessage = "Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0.";
}
