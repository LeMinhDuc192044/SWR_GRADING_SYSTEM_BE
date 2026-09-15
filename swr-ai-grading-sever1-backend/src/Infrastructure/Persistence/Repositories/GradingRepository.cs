using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository riêng cho Grading — kế thừa Repository&lt;Grading&gt;.
/// </summary>
public sealed class GradingRepository : Repository<Grading>, IGradingRepository
{
    public GradingRepository(AppDbContext db) : base(db) { }

    /// <summary>
    /// Kiểm tra mã Grading đã tồn tại hay chưa (dùng khi client cung cấp GradingCode).
    /// </summary>
    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(g => g.GradingCode == code, ct);

    /// <summary>
    /// Sinh mã Grading tự động: "G_xxxxxx" (6 số random), đảm bảo unique.
    /// </summary>
    public async Task<string> GenerateUniqueCodeAsync(CancellationToken ct = default)
    {
        do
        {
            var code = $"G{Random.Shared.Next(0, 1_000_000):D6}";
            if (!await IsCodeExistsAsync(code, ct))
                return code;
        } while (true);
    }
}
