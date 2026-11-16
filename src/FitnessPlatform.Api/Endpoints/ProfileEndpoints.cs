using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitnessPlatform.Contracts.Profiles;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FitnessPlatform.Application.Profiles;
namespace FitnessPlatform.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
            .WithTags("Profiles")
            .RequireAuthorization();

        // POST: Create profile
        group.MapPost("/", async (
            CreateUserProfileRequest request,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authenticatedUserId =
                GetAuthenticatedUserId(httpContext);

            if (authenticatedUserId is null)
            {
                return Results.Unauthorized();
            }


            var validationError = UserProfileValidator.Validate(
                request.FirstName,
                request.LastName,
                request.HeightCm,
                request.WeightKg,
                request.CountryCode);

            if (validationError is not null)
            {
                return Results.BadRequest(new
                {
                    Error = validationError
                });
            }

            var existingProfile = await db.UserProfiles
    .AnyAsync(
        x => x.UserId == authenticatedUserId.Value,
        cancellationToken);

            if (existingProfile)
            {
                return Results.Conflict(new
                {
                    Error = "A profile already exists for this user."
                });
            }

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = authenticatedUserId.Value,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                DateOfBirth = request.DateOfBirth,
                HeightCm = request.HeightCm,
                WeightKg = request.WeightKg,
                CountryCode = request.CountryCode.ToUpperInvariant(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.UserProfiles.Add(profile);

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/profiles/{profile.Id}",
                ToResponse(profile));
        });

        // GET: Retrieve profile by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authenticatedUserId =
                GetAuthenticatedUserId(httpContext);

            if (authenticatedUserId is null)
            {
                return Results.Unauthorized();
            }

            var profile = await db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (profile is null)
            {
                return Results.NotFound(new
                {
                    Error = "User profile not found."
                });
            }

            if (!CanAccessUser(
                httpContext,
                authenticatedUserId.Value,
                profile.UserId))
            {
                return Results.Forbid();
            }

            return Results.Ok(ToResponse(profile));
        });

        // PUT: Update existing profile
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserProfileRequest request,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authenticatedUserId =
                GetAuthenticatedUserId(httpContext);

            if (authenticatedUserId is null)
            {
                return Results.Unauthorized();
            }

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (profile is null)
            {
                return Results.NotFound(new
                {
                    Error = "User profile not found."
                });
            }

            if (!CanAccessUser(
                httpContext,
                authenticatedUserId.Value,
                profile.UserId))
            {
                return Results.Forbid();
            }

            var validationError = UserProfileValidator.Validate(
     request.FirstName,
     request.LastName,
     request.HeightCm,
     request.WeightKg,
     request.CountryCode);

            if (validationError is not null)
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid profile information."
                });
            }

            profile.FirstName = request.FirstName.Trim();
            profile.LastName = request.LastName.Trim();
            profile.DateOfBirth = request.DateOfBirth;
            profile.HeightCm = request.HeightCm;
            profile.WeightKg = request.WeightKg;
            profile.CountryCode = request.CountryCode.ToUpperInvariant();
            profile.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(profile));
        });

        return app;
    }

    private static Guid? GetAuthenticatedUserId(
        HttpContext httpContext)
    {
        var userIdValue =
            httpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId) ||
            userId == Guid.Empty)
        {
            return null;
        }

        return userId;
    }

    private static bool CanAccessUser(
        HttpContext httpContext,
        Guid authenticatedUserId,
        Guid requestedUserId)
    {
        return authenticatedUserId == requestedUserId ||
               httpContext.User.IsInRole("Admin");
    }

    private static UserProfileResponse ToResponse(
        UserProfile profile)
    {
        return new UserProfileResponse
        {
            Id = profile.Id,
            UserId = profile.UserId,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            DateOfBirth = profile.DateOfBirth,
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            CountryCode = profile.CountryCode,
            CreatedAtUtc = profile.CreatedAtUtc
        };
    }
}