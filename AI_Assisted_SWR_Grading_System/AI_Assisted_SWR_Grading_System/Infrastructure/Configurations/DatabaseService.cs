using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AI_Assisted_SWR_Grading_System.Infrastructure.Persistence;

namespace AI_Assisted_SWR_Grading_System.Infrastructure.Configurations;

public static class DatabaseService
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        return services;
    }
}