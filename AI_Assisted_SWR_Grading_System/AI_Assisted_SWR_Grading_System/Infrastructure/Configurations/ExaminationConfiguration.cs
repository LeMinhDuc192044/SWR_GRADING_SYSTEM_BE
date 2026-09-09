using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using AI_Assisted_SWR_Grading_System.Infrastructure.Persistence.Converters;

namespace AI_Assisted_SWR_Grading_System.Infrastructure.Persistence.Configurations;

public class ExaminationConfiguration : IEntityTypeConfiguration<Examination>
{
    public void Configure(EntityTypeBuilder<Examination> builder)
    {
        builder.Property(e => e.ExaminationType)
            .HasConversion(new ExaminationTypeConverter())
            .HasMaxLength(5);

        builder.HasIndex(e => e.ExaminationCode).IsUnique();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}