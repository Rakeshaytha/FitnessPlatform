using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Authentication;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string Email { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();
}
