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
    private WorkItemApplicationService Service => new(store, TimeProvider.System);

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

    [Fact]
    public async Task Update_replaces_all_fields_but_keeps_owner_status_and_closedAt()
    {
        var ownerId = Guid.CreateVersion7();
        var existing = new WorkItem {
            Id = Guid.CreateVersion7(), UserId = ownerId, Title = "Old",
            Status = WorkItemStatus.Done, ClosedAt = DateTimeOffset.UtcNow
        };
        store.Tracked = existing;

        var detail = await Service.UpdateAsync(new UpdateWorkItem("  New  ", null,
            TaskType.Refactor, Complexity.XL, DateTimeOffset.UtcNow.AddDays(-2), null),
            ownerId, existing.Id, default);

        Assert.NotNull(detail);
        Assert.Equal("New", detail.Title);
        Assert.Null(detail.Description);
        Assert.Equal("Refactor", detail.Type);
        Assert.Equal("Done", detail.Status);
        Assert.NotNull(detail.ClosedAt);
        Assert.Equal(ownerId, existing.UserId);
        Assert.Equal(existing.Id, store.UpdatedId);
    }

    [Fact]
    public async Task Update_missing_item_returns_null()
    {
        store.Tracked = null;
        Assert.Null(await Service.UpdateAsync(new UpdateWorkItem("T", null,
            TaskType.Coding, Complexity.S, null, null), Guid.CreateVersion7(),
            Guid.CreateVersion7(), default));
    }

    // initial, initialClosedAt, target, expected: "now" | "old" | "null".
    public static TheoryData<WorkItemStatus, bool, WorkItemStatus, string> StatusMatrix() => new()
    {
        { WorkItemStatus.Open, false, WorkItemStatus.Done, "now" },
        { WorkItemStatus.Archived, false, WorkItemStatus.Done, "now" },
        { WorkItemStatus.Archived, true, WorkItemStatus.Done, "now" },
        { WorkItemStatus.Done, true, WorkItemStatus.Done, "old" },
        { WorkItemStatus.Open, false, WorkItemStatus.Open, "null" },
        { WorkItemStatus.Open, false, WorkItemStatus.Archived, "null" },
        { WorkItemStatus.Archived, false, WorkItemStatus.Archived, "null" },
        { WorkItemStatus.Done, true, WorkItemStatus.Archived, "old" },
        { WorkItemStatus.Archived, true, WorkItemStatus.Archived, "old" },
        { WorkItemStatus.Done, true, WorkItemStatus.Open, "null" },
        { WorkItemStatus.Archived, true, WorkItemStatus.Open, "null" },
        { WorkItemStatus.Archived, false, WorkItemStatus.Open, "null" }
    };

    [Theory]
    [MemberData(nameof(StatusMatrix))]
    public async Task ChangeStatus_applies_closedAt_matrix_with_injected_clock(
        WorkItemStatus initial, bool hasClosedAt, WorkItemStatus target, string expected)
    {
        var old = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        store.Tracked = new WorkItem {
            Id = Guid.CreateVersion7(), UserId = Guid.CreateVersion7(), Title = "M",
            Status = initial, ClosedAt = hasClosedAt ? old : null
        };
        var service = new WorkItemApplicationService(store, new FixedClock());

        var detail = await service.ChangeStatusAsync(
            new ChangeWorkItemStatus(target), store.Tracked.UserId, store.Tracked.Id, default);

        Assert.NotNull(detail);
        Assert.Equal(target.ToString(), detail.Status);
        Assert.Equal(expected switch { "now" => FixedClock.Now, "old" => old, _ => (DateTimeOffset?)null },
            detail.ClosedAt);
    }

    [Fact]
    public async Task ChangeStatus_missing_item_returns_null()
    {
        store.Tracked = null;
        Assert.Null(await Service.ChangeStatusAsync(
            new ChangeWorkItemStatus(WorkItemStatus.Done),
            Guid.CreateVersion7(), Guid.CreateVersion7(), default));
    }

    private static readonly Guid N1 = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid N2 = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid N3 = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid N4 = new("44444444-4444-4444-4444-444444444444");

    [Theory]
    [MemberData(nameof(GraphCases))]
    public void Graph_detects_paths_without_database(
        List<(Guid, Guid)> edges, Guid start, Guid target, bool expected) =>
        Assert.Equal(expected, WorkItemGraph.HasPath(edges, start, target));

    public static TheoryData<List<(Guid, Guid)>, Guid, Guid, bool> GraphCases() => new()
    {
        { [], N1, N2, false },
        { [(N1, N2)], N1, N2, true },
        { [(N1, N2)], N2, N1, false },
        { [(N1, N2), (N2, N3)], N1, N3, true },
        { [(N1, N2), (N2, N3)], N3, N1, false },
        { [(N1, N2), (N1, N3), (N2, N3)], N1, N3, true },
        { [(N1, N3), (N2, N3)], N1, N2, false },
        { [(N1, N2), (N2, N1)], N2, N1, true },
        { [(N1, N1)], N2, N1, false }
    };

    [Fact]
    public async Task AddDependency_short_circuits_self_loop_without_store_call()
    {
        var id = Guid.CreateVersion7();
        Assert.Equal(AddDependencyOutcome.SelfLoop,
            await Service.AddDependencyAsync(Guid.CreateVersion7(), id, id, default));
        Assert.False(store.AddDependencyCalled);
    }

    [Fact]
    public async Task AddDependency_returns_store_outcome()
    {
        store.DependencyOutcome = AddDependencyOutcome.Duplicate;
        Assert.Equal(AddDependencyOutcome.Duplicate, await Service.AddDependencyAsync(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.True(store.AddDependencyCalled);
    }

    [Fact]
    public async Task RemoveDependency_returns_store_outcome()
    {
        store.RemoveOutcome = RemoveDependencyOutcome.ItemNotFound;
        Assert.Equal(RemoveDependencyOutcome.ItemNotFound, await Service.RemoveDependencyAsync(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.True(store.RemoveDependencyCalled);
    }

    [Fact]
    public async Task Delete_returns_store_result()
    {
        store.Deleted = true;
        Assert.True(await Service.DeleteAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), default));
        Assert.True(store.DeleteCalled);
        store.Deleted = false;
        Assert.False(await Service.DeleteAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), default));
    }

    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-21T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Get_returns_detail_with_links_or_null()
    {
        var ownerId = Guid.CreateVersion7();
        var item = new WorkItem { Id = Guid.CreateVersion7(), Title = "D", UserId = ownerId };
        store.Found = (item, [Guid.CreateVersion7(), Guid.CreateVersion7()]);

        var detail = await Service.GetAsync(ownerId, item.Id, default);
        Assert.NotNull(detail);
        Assert.Equal("D", detail.Title);
        Assert.Equal(2, detail.DependsOnWorkItemIds.Count);

        store.Found = (null, []);
        Assert.Null(await Service.GetAsync(ownerId, Guid.CreateVersion7(), default));
    }

    private sealed class FakeStore : IWorkItemStore
    {
        public WorkItem? Saved;
        public WorkItem? Tracked;
        public Guid UpdatedId;
        public (WorkItem? Item, IReadOnlyList<Guid> DependsOn) Found = (null, []);
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
        public Task<(WorkItem? Item, IReadOnlyList<Guid> DependsOnIds)> FindAsync(Guid ownerId, Guid id, CancellationToken ct) =>
            Task.FromResult<(WorkItem?, IReadOnlyList<Guid>)>(Found);
        public Task<WorkItem?> UpdateAsync(Guid ownerId, Guid id, Action<WorkItem> apply, CancellationToken ct)
        {
            if (Tracked is null) return Task.FromResult<WorkItem?>(null);
            UpdatedId = id;
            apply(Tracked);
            return Task.FromResult<WorkItem?>(Tracked);
        }
        public bool AddDependencyCalled;
        public AddDependencyOutcome DependencyOutcome = AddDependencyOutcome.Added;
        public Task<AddDependencyOutcome> TryAddDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct)
        {
            AddDependencyCalled = true;
            return Task.FromResult(DependencyOutcome);
        }
        public bool RemoveDependencyCalled;
        public RemoveDependencyOutcome RemoveOutcome = RemoveDependencyOutcome.Removed;
        public Task<RemoveDependencyOutcome> TryRemoveDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct)
        {
            RemoveDependencyCalled = true;
            return Task.FromResult(RemoveOutcome);
        }
        public bool DeleteCalled;
        public bool Deleted = true;
        public Task<bool> TryDeleteAsync(Guid ownerId, Guid id, CancellationToken ct)
        {
            DeleteCalled = true;
            return Task.FromResult(Deleted);
        }
    }
}
