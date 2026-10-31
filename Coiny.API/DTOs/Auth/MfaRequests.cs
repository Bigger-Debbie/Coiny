using System.ComponentModel.DataAnnotations;
namespace Coiny.API.DTOs.Auth;
public class MfaLoginRequest {
    [Required, MaxLength(4096)] public string Challenge { get; set; } = "";
    [Required, MaxLength(64)] public string Code { get; set; } = "";
    public bool RecoveryCode { get; set; }
}
public class MfaEnableRequest {
    [Required, MaxLength(4096)] public string SetupToken { get; set; } = "";
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = "";
}
public class MfaManageRequest : ConfirmPasswordRequest {
    [Required, MaxLength(64)] public string Code { get; set; } = "";
    public bool RecoveryCode { get; set; }
}
