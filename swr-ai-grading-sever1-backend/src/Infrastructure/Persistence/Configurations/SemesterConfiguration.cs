using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.HasIndex(s => s.SemesterCode).IsUnique();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
    }
}