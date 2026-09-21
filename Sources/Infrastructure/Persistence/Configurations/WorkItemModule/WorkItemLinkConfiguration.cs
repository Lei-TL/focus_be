using Domain.Entities.WorkItemModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Persistence.Configurations.WorkItemModule;
public sealed class WorkItemLinkConfiguration : IEntityTypeConfiguration<WorkItemLink>
{
    public void Configure(EntityTypeBuilder<WorkItemLink> b)
    {
        b.ToTable("work_item_links", t => t.HasCheckConstraint("ck_work_item_links_no_self",
            "work_item_id <> depends_on_work_item_id"));
        b.HasIndex(x => new { x.WorkItemId, x.DependsOnWorkItemId }).IsUnique();
        b.HasOne(x => x.WorkItem).WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DependsOn).WithMany(x => x.DependedOnBy)
            .HasForeignKey(x => x.DependsOnWorkItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
