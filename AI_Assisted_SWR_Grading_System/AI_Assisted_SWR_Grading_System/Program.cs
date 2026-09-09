using AI_Assisted_SWR_Grading_System.Infrastructure.Configurations;
using DotNetEnv;

namespace AI_Assisted_SWR_Grading_System;

public class Program
{
    public static void Main(string[] args)
    {
        // Load variables from .env
        Env.Load();

        var builder = WebApplication.CreateBuilder(args);

        // Database
        builder.Services.AddDatabase(builder.Configuration);

        // Controllers
        builder.Services.AddControllers();

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));


        var app = builder.Build();

        // Swagger
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}