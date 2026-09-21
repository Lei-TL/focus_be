using Application.WorkItemModule.Abstractions;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using Domain.Entities.WorkItemModule;
using Domain.Enums;
using Xunit;
namespace Focus.Tests;

public sealed class WorkItemApplicationTests
{
    private readonly FakeStore store = new();
    private WorkItemApplicationService Service => new(store);

    [Fact]
    public async Task Create_assigns_owner_trims_and_applies_defaults()
    {
        var ownerId = Guid.CreateVersion7();
        var detail = await Service.CreateAsync(new CreateWorkItem("  Title  ", " Desc ",
            TaskType.Coding, Complexity.M, DateTimeOffset.UtcNow.AddDays(1), 25), ownerId, default);

        Assert.Equal("Title", detail.Title);
        Assert.Equal("Desc", detail.Description);
        Assert.Equal("Coding", detail.Type);
        Assert.Equal("M", detail.Complexity);
        Assert.Equal("Open", detail.Status);
        Assert.Null(detail.ClosedAt);
        Assert.Equal(25, detail.UserEstimateMinutes);
        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal(ownerId, store.Saved!.UserId);
        Assert.Equal(WorkItemStatus.Open, store.Saved.Status);
    }

    [Fact]
    public async Task Create_nulls_blank_description_and_normalizes_deadline_to_utc()
    {
        var deadline = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(7));
        var detail = await Service.CreateAsync(new CreateWorkItem("T", "   ",
            TaskType.Testing, Complexity.S, deadline, null), Guid.CreateVersion7(), default);

        Assert.Null(detail.Description);
        Assert.Equal(TimeSpan.Zero, detail.Deadline!.Value.Offset);
        Assert.Equal(deadline.UtcDateTime, detail.Deadline.Value.UtcDateTime);
    }

    private sealed class FakeStore : IWorkItemStore
    {
        public WorkItem? Saved;
        public Task AddAsync(WorkItem item, CancellationToken ct)
        {
            Saved = item;
            return Task.CompletedTask;
        }
    }
}
