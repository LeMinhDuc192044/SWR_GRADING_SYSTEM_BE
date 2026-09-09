using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Infrastructure.Persistence.Converters
{
    public class ExaminationTypeConverter : ValueConverter<ExaminationType, string>
    {
        public ExaminationTypeConverter()
            : base(
                v => ToProviderValue(v),
                v => FromProviderValue(v))
        {
        }

        private static string ToProviderValue(ExaminationType v) => v switch
        {
            ExaminationType.RE => "RE",
            ExaminationType.PE => "PE",
            ExaminationType.ThreeW => "3W",
            _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExaminationType.")
        };

        private static ExaminationType FromProviderValue(string v) => v switch
        {
            "RE" => ExaminationType.RE,
            "PE" => ExaminationType.PE,
            "3W" => ExaminationType.ThreeW,
            _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown examination type value in database.")
        };
    }
}