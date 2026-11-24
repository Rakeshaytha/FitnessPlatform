using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Filters;

public static class SqlConcurrencyExceptionHelper
{
    public static bool IsConcurrencyConflict(Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if (error.Number is 1205 or 1222 or 2601 or 2627)
                    {
                        return true;
                    }
                }
            }

            current = current.InnerException;
        }

        return false;
    }
}