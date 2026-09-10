using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public class AppDbContextDesignFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Current directory when running from src/Infrastructure
        var infrastructurePath = Directory.GetCurrentDirectory();

        // Infrastructure -> src -> project root
        var envPath = Path.GetFullPath(
            Path.Combine(infrastructurePath, "..", "..", ".env")
        );

        if (!File.Exists(envPath))
        {
            throw new FileNotFoundException(
                $".env file not found: {envPath}");
        }

        Env.Load(envPath);

        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is not set.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            options =>
                options.MigrationsAssembly("Infrastructure"));

        return new AppDbContext(optionsBuilder.Options);
    }
}