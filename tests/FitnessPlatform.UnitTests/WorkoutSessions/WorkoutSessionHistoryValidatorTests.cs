using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using Xunit;

namespace FitnessPlatform.UnitTests.WorkoutSessions;

public sealed class WorkoutSessionHistoryValidatorTests
{
    // ==========================================
    // DEFAULT REQUEST VALIDATION
    // ==========================================

    [Fact]
    public void DefaultRequest_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest();

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DefaultRequest_HasExpectedPagination()
    {
        var request = new WorkoutSessionHistoryRequest();

        Assert.Equal(1, request.PageNumber);
        Assert.Equal(20, request.PageSize);
    }

    [Fact]
    public void DefaultRequest_HasNoOptionalFilters()
    {
        var request = new WorkoutSessionHistoryRequest();

        Assert.Null(request.FromDate);
        Assert.Null(request.ToDate);
        Assert.Null(request.Status);
        Assert.Null(request.WorkoutPlanId);
    }

    [Fact]
    public void MaxPageSize_HasExpectedValue()
    {
        Assert.Equal(
            100,
            WorkoutSessionHistoryValidator.MaxPageSize);
    }

    // ==========================================
    // PAGE NUMBER VALIDATION
    // ==========================================

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(100000)]
    public void PageNumber_ValidValue_ReturnsNull(int pageNumber)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = pageNumber
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void PageNumber_InvalidValue_ReturnsError(int pageNumber)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = pageNumber
        };

        Assert.Equal(
            "Page number must be greater than or equal to 1.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // PAGE SIZE VALIDATION
    // ==========================================

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void PageSize_ValidValue_ReturnsNull(int pageSize)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageSize = pageSize
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void PageSize_InvalidValue_ReturnsError(int pageSize)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageSize = pageSize
        };

        Assert.Equal(
            "Page size must be between 1 and 100.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // PAGINATION OVERFLOW VALIDATION
    // ==========================================

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(21474837, 100)]
    [InlineData(1073741824, 2)]
    public void Pagination_ValidOffset_ReturnsNull(
        int pageNumber,
        int pageSize)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Theory]
    [InlineData(int.MaxValue, 100)]
    [InlineData(int.MaxValue, 2)]
    [InlineData(21474838, 100)]
    [InlineData(1073741825, 2)]
    public void Pagination_OffsetExceedsIntegerLimit_ReturnsError(
        int pageNumber,
        int pageSize)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        Assert.Equal(
            "The requested page exceeds the supported pagination range.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // DATE FILTER VALIDATION
    // ==========================================

    [Fact]
    public void DateRange_NoDates_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = null,
            ToDate = null
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_FromDateOnly_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 1)
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_ToDateOnly_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            ToDate = new DateOnly(2026, 11, 30)
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_ValidRange_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 30)
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_SameDate_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 25),
            ToDate = new DateOnly(2026, 11, 25)
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_ReversedDates_ReturnsError()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 30),
            ToDate = new DateOnly(2026, 11, 1)
        };

        Assert.Equal(
            "ToDate cannot be earlier than FromDate.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_CrossYearRange_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2025, 12, 1),
            ToDate = new DateOnly(2026, 1, 31)
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void DateRange_MinimumAndMaximumDates_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = DateOnly.MinValue,
            ToDate = DateOnly.MaxValue
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // WORKOUT PLAN FILTER VALIDATION
    // ==========================================

    [Fact]
    public void WorkoutPlanId_NullValue_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            WorkoutPlanId = null
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void WorkoutPlanId_ValidGuid_ReturnsNull()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            WorkoutPlanId = Guid.NewGuid()
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void WorkoutPlanId_EmptyGuid_ReturnsError()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            WorkoutPlanId = Guid.Empty
        };

        Assert.Equal(
            "Workout plan ID cannot be an empty GUID.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // HISTORY STATUS VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData(WorkoutSessionStatuses.Completed)]
    [InlineData(WorkoutSessionStatuses.Cancelled)]
    public void Status_ValidValue_ReturnsNull(string? status)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            Status = status
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Theory]
    [InlineData(WorkoutSessionStatuses.InProgress)]
    [InlineData(WorkoutSessionStatuses.Paused)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("Pending")]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    [InlineData("cancelled")]
    [InlineData("CANCELLED")]
    [InlineData(" Completed")]
    [InlineData("Completed ")]
    [InlineData("Cancelled ")]
    public void Status_InvalidValue_ReturnsError(string status)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            Status = status
        };

        Assert.Equal(
            "History status must be Completed or Cancelled.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // COMBINED FILTER VALIDATION
    // ==========================================

    [Theory]
    [InlineData(WorkoutSessionStatuses.Completed)]
    [InlineData(WorkoutSessionStatuses.Cancelled)]
    public void CombinedFilters_AllValid_ReturnsNull(string status)
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 30),
            Status = status,
            WorkoutPlanId = Guid.NewGuid(),
            PageNumber = 2,
            PageSize = 50
        };

        Assert.Null(
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void CombinedFilters_InvalidDateRange_ReturnsError()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 30),
            ToDate = new DateOnly(2026, 11, 1),
            Status = WorkoutSessionStatuses.Completed,
            WorkoutPlanId = Guid.NewGuid(),
            PageNumber = 1,
            PageSize = 20
        };

        Assert.Equal(
            "ToDate cannot be earlier than FromDate.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void CombinedFilters_InvalidWorkoutPlanId_ReturnsError()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 30),
            Status = WorkoutSessionStatuses.Completed,
            WorkoutPlanId = Guid.Empty,
            PageNumber = 1,
            PageSize = 20
        };

        Assert.Equal(
            "Workout plan ID cannot be an empty GUID.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void CombinedFilters_InvalidStatus_ReturnsError()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 30),
            Status = WorkoutSessionStatuses.Paused,
            WorkoutPlanId = Guid.NewGuid(),
            PageNumber = 1,
            PageSize = 20
        };

        Assert.Equal(
            "History status must be Completed or Cancelled.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // VALIDATION ORDER
    // ==========================================

    [Fact]
    public void MultipleInvalidFields_PageNumberValidatedFirst()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = 0,
            PageSize = 0,
            WorkoutPlanId = Guid.Empty,
            Status = "Invalid"
        };

        Assert.Equal(
            "Page number must be greater than or equal to 1.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    [Fact]
    public void MultipleInvalidFields_PageSizeValidatedBeforeFilters()
    {
        var request = new WorkoutSessionHistoryRequest
        {
            PageNumber = 1,
            PageSize = 101,
            WorkoutPlanId = Guid.Empty,
            Status = "Invalid"
        };

        Assert.Equal(
            "Page size must be between 1 and 100.",
            WorkoutSessionHistoryValidator.Validate(request));
    }

    // ==========================================
    // RESPONSE CONTRACT DEFAULTS
    // ==========================================

    [Fact]
    public void HistoryResponse_DefaultCollection_IsEmpty()
    {
        var response = new WorkoutSessionHistoryResponse();

        Assert.NotNull(response.Sessions);
        Assert.Empty(response.Sessions);
    }

    [Fact]
    public void HistoryResponse_DefaultPaginationFlags_AreFalse()
    {
        var response = new WorkoutSessionHistoryResponse();

        Assert.False(response.HasPreviousPage);
        Assert.False(response.HasNextPage);
    }
}
