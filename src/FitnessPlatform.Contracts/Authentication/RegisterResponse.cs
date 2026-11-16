using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Authentication;

public sealed class RegisterResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
