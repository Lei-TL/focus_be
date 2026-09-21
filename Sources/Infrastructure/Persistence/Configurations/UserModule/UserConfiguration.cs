using Domain.Entities.UserModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Persistence.Configurations.UserModule;
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t => t.HasCheckConstraint("ck_users_session_minutes", "default_session_minutes BETWEEN 15 AND 90"));
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
    }
}
