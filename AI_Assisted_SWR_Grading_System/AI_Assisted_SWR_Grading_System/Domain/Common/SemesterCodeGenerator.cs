namespace AI_Assisted_SWR_Grading_System.Domain.Common;
public static class SemesterCodeGenerator
{
    /// <summary>
    /// Generates a semester code following the FPT-style Vietnamese trimester calendar:
    /// Spring (Jan–Apr) = SP, Summer (May–Aug) = SU, Fall (Sep–Dec) = FA.
    /// </summary>
    public static string Generate(DateTime startDate)
    {
        var season = startDate.Month switch
        {
            >= 1 and <= 4 => "SP",
            >= 5 and <= 8 => "SU",
            >= 9 and <= 12 => "FA",
            _ => throw new ArgumentOutOfRangeException(
                nameof(startDate), "Month must be between 1 and 12.")
        };

        var yy = startDate.Year % 100;
        return $"{season}{yy:D2}"; // e.g. SP26, SU26, FA26, SP27
    }
}
