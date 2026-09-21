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

    [Fact]
    public async Task List_maps_items_and_echoes_paging()
    {
        var ownerId = Guid.CreateVersion7();
        var filter = new WorkItemFilter(WorkItemStatus.Open, TaskType.Coding, 2, 20);
        var result = await Service.ListAsync(filter, ownerId, default);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["A", "B"], result.Items.Select(x => x.Title));
        Assert.Equal(ownerId, store.ListedOwner);
        Assert.Equal(WorkItemStatus.Open, store.ListedStatus);
        Assert.Equal(TaskType.Coding, store.ListedType);
    }

    private sealed class FakeStore : IWorkItemStore
    {
        public WorkItem? Saved;
        public Guid ListedOwner;
        public WorkItemStatus? ListedStatus;
        public TaskType? ListedType;
        public Task AddAsync(WorkItem item, CancellationToken ct)
        {
            Saved = item;
            return Task.CompletedTask;
        }
        public Task<(IReadOnlyList<WorkItem> Items, int TotalCount)> ListAsync(Guid ownerId,
            WorkItemStatus? status, TaskType? type, int page, int pageSize, CancellationToken ct)
        {
            ListedOwner = ownerId;
            ListedStatus = status;
            ListedType = type;
            IReadOnlyList<WorkItem> items =
            [
                new() { Title = "A", UserId = ownerId },
                new() { Title = "B", UserId = ownerId }
            ];
            return Task.FromResult<(IReadOnlyList<WorkItem>, int)>((items, 2));
        }
    }
}
