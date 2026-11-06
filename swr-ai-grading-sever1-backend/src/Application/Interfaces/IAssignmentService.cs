using Application.Common;
using Application.DTOs.Assignments;

namespace Application.Interfaces;

public interface IAssignmentService
{
    Task<Result<IReadOnlyList<AssignmentDTO>>> GetByGradingSessionAsync(
        Guid gradingSessionId, CancellationToken ct = default);

    Task<Result<AssignmentDTO>> GetByIdAsync(
        Guid gradingSessionId, Guid assignmentId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<AssignmentDTO>>> CreateAsync(
        Guid gradingSessionId, CreateAssignmentRequest request, CancellationToken ct = default);

    Task<Result<AssignmentDTO>> UpdateAsync(
        Guid gradingSessionId, Guid assignmentId, UpdateAssignmentRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(
        Guid gradingSessionId, Guid assignmentId, CancellationToken ct = default);
}