using FitnessPlatform.Api.Endpoints;
using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register OpenAPI
builder.Services.AddOpenApi();


// Register SQL Server database context
builder.Services.AddDbContext<FitnessPlatformDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("FitnessPlatformDb")
        ?? throw new InvalidOperationException(
            "Connection string 'FitnessPlatformDb' is missing.")));

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is missing.");

var jwtKeyBytes = Convert.FromBase64String(jwtKey);

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKeyBytes),

                ValidateLifetime = true,

                RequireExpirationTime = true,
                RequireSignedTokens = true,

                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = "name",
                RoleClaimType = "role"
            };
    });

builder.Services.AddAuthorization();
// Register data protection required by Identity token providers
builder.Services.AddDataProtection();


// Register ASP.NET Core Identity
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Password requirements
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;

        // Account lockout
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User requirements
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddSignInManager()
    .AddEntityFrameworkStores<FitnessPlatformDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Initialize application roles
// Initialize development identity data
if (app.Environment.IsDevelopment() &&
    !app.Configuration.GetValue<bool>("IntegrationTesting:Enabled"))
{
    using var scope = app.Services.CreateScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    await IdentityRoleSeeder.SeedRolesAsync(roleManager);

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    var adminEmail = app.Configuration["BootstrapAdmin:Email"];
    var adminPassword = app.Configuration["BootstrapAdmin:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) &&
        !string.IsNullOrWhiteSpace(adminPassword))
    {
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin != null)
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                throw new InvalidOperationException(
                    "Bootstrap administrator email belongs to an existing non-admin account.");
            }

            var resetRequested = string.Equals(
                app.Configuration["BootstrapAdmin:ResetPasswordOnce"],
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (resetRequested)
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(
                    existingAdmin);

                var resetResult = await userManager.ResetPasswordAsync(
                    existingAdmin,
                    resetToken,
                    adminPassword);

                if (!resetResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Administrator password reset failed: " +
                        string.Join("; ", resetResult.Errors.Select(x => x.Description)));
                }

                await userManager.ResetAccessFailedCountAsync(existingAdmin);

                Console.WriteLine(
                    "Development administrator password reset completed.");
            }
        }
        else
        {
            await AdminAccountSeeder.SeedAdminAsync(
                userManager,
                adminEmail,
                adminPassword);
        }
    }
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// JWT Authentication
app.UseAuthentication();

// Role-based Authorization
app.UseAuthorization();

// Health check endpoint
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        Status = "Healthy",
        Application = "FitnessPlatform",
        Timestamp = DateTime.UtcNow
    });
});

// Profile endpoints
app.MapProfileEndpoints();
app.MapFitnessGoalEndpoints();
app.MapUserFitnessGoalEndpoints();
app.MapWorkoutPreferenceEndpoints();
app.MapDietaryPreferenceEndpoints();
app.MapOnboardingEndpoints();
app.MapExerciseEndpoints();
app.MapAuthEndpoints();

app.Run();
public partial class Program
{
}