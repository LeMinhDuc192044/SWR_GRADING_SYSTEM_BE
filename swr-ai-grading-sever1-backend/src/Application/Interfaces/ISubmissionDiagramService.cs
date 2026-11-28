using Application.Common;
using Application.DTOs.Diagrams;

namespace Application.Interfaces;

public interface ISubmissionDiagramService
{
    /// <summary>
    /// Links a submission's parsed answers to diagram elements. Every answer whose question is
    /// connected to a diagram is matched against all elements of that question's diagram; answers
    /// of questions without a diagram are left untouched.
    /// </summary>
    Task<Result<ConnectSubmissionDiagramResultDTO>> ConnectSubmissionAsync(
        Guid submissionId, CancellationToken ct = default);
}
