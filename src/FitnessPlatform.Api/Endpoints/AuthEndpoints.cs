using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FitnessPlatform.Contracts.Authentication;
using FitnessPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace FitnessPlatform.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new
                {
                    Error = "Email and password are required."
                });
            }

            var user = await userManager.FindByEmailAsync(
                request.Email.Trim());

            if (user == null)
            {
                return Results.Unauthorized();
            }

            if (!user.IsActive)
            {
                return Results.Unauthorized();
            }

            if (await userManager.IsLockedOutAsync(user))
            {
                return Results.Unauthorized();
            }

            var passwordValid = await userManager.CheckPasswordAsync(
                user,
                request.Password);

            if (!passwordValid)
            {
                await userManager.AccessFailedAsync(user);

                return Results.Unauthorized();
            }

            await userManager.ResetAccessFailedCountAsync(user);

            var roles = await userManager.GetRolesAsync(user);

            var jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT signing key is missing.");

            var jwtIssuer = configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException(
                    "JWT issuer is missing.");

            var jwtAudience = configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException(
                    "JWT audience is missing.");

            if (!int.TryParse(
                    configuration["Jwt:ExpirationMinutes"],
                    out var expirationMinutes) ||
                expirationMinutes <= 0)
            {
                throw new InvalidOperationException(
                    "JWT expiration configuration is invalid.");
            }

            var key = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    user.Id.ToString()),

                new Claim(
                    JwtRegisteredClaimNames.Email,
                    user.Email ?? string.Empty),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()),

                new Claim(
                    "name",
                    user.UserName ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(
                expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            var tokenHandler = new JwtSecurityTokenHandler();

            var accessToken = tokenHandler.WriteToken(token);

            return Results.Ok(new LoginResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = expirationMinutes * 60,
                ExpiresAtUtc = expiresAtUtc,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            });
        })
        .AllowAnonymous()
        .WithName("Login");


        group.MapPost("/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) =>
        {
            var email = request.Email?.Trim();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new
                {
                    Error = "Email and password are required."
                });
            }

            if (request.Password != request.ConfirmPassword)
            {
                return Results.BadRequest(new
                {
                    Error = "Password and ConfirmPassword do not match."
                });
            }

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                return Results.Conflict(new
                {
                    Error = "An account with this email already exists."
                });
            }

            if (!await roleManager.RoleExistsAsync("Member"))
            {
                throw new InvalidOperationException(
                    "Member role has not been initialized.");
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(
                user,
                request.Password);

            if (!createResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    Errors = createResult.Errors.Select(
                        error => error.Description)
                });
            }

            var roleResult = await userManager.AddToRoleAsync(
                user,
                "Member");

            if (!roleResult.Succeeded)
            {
                var deleteResult = await userManager.DeleteAsync(user);

                if (!deleteResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Member role assignment failed and account cleanup was unsuccessful.");
                }

                throw new InvalidOperationException(
                    "Member role assignment failed.");
            }

            return Results.Created(
                $"/api/auth/users/{user.Id}",
                new RegisterResponse
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    Role = "Member",
                    CreatedAtUtc = user.CreatedAtUtc
                });
        })
.AllowAnonymous()
.WithName("RegisterMember");
        return app;
    }
}