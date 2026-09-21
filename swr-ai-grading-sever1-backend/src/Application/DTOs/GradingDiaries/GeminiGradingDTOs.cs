using System.Text.Json.Serialization;

namespace Application.DTOs.GradingDiaries;

public sealed class CriterionScoreDto
{
    [JsonPropertyName("criterion")]
    public string Criterion { get; set; } = string.Empty;

    [JsonPropertyName("max_score")]
    public decimal MaxScore { get; set; }

    [JsonPropertyName("actual_score")]
    public decimal ActualScore { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

public sealed class GradingResultDto
{
    [JsonPropertyName("total_score")]
    public decimal TotalScore { get; set; }

    [JsonPropertyName("criteria_scores")]
    public IReadOnlyList<CriterionScoreDto> CriteriaScores { get; set; } = Array.Empty<CriterionScoreDto>();

    [JsonPropertyName("missing_items")]
    public IReadOnlyList<string> MissingItems { get; set; } = Array.Empty<string>();

    [JsonPropertyName("overall_comment")]
    public string OverallComment { get; set; } = string.Empty;

    [JsonIgnore]
    public string RawAiLogJson { get; set; } = string.Empty;
}
