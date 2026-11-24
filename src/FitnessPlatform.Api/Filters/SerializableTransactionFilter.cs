using System.Data;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Filters;

public sealed class SerializableTransactionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        var cancellationToken = httpContext.RequestAborted;

        var db = httpContext.RequestServices
            .GetRequiredService<FitnessPlatformDbContext>();

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var result = await next(context);

            if (result is IStatusCodeHttpResult statusResult &&
                statusResult.StatusCode is int statusCode &&
                statusCode >= 200 &&
                statusCode < 300)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception exception)
            when (SqlConcurrencyExceptionHelper
                .IsConcurrencyConflict(exception))
        {
            return CreateConflictResponse();
        }
    }

    private static IResult CreateConflictResponse()
    {
        return Results.Conflict(new
        {
            Error = "The requested operation conflicts with another database operation or an existing record. Refresh the data and try again.",
            Code = "CONCURRENCY_CONFLICT"
        });
    }
}