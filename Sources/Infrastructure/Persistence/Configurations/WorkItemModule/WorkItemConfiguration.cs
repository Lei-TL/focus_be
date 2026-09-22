using Domain.Entities.WorkItemModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Persistence.Configurations.WorkItemModule;
public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> b)
    {
        b.ToTable("work_items", t => t.HasCheckConstraint("ck_work_items_estimate",
            "user_estimate_minutes IS NULL OR user_estimate_minutes > 0"));
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(5000);
        b.HasIndex(x => new { x.UserId, x.Status, x.Type, x.Complexity });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
