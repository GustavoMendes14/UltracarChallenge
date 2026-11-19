using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Scalar.AspNetCore;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Application.Interfaces.Services;
using UltracarInventoryApi.Application.Services;
using UltracarInventoryApi.Infrastructure.Persistence;
using UltracarInventoryApi.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddSingleton(provider =>
{
    var connectionString = provider.GetRequiredService<IConfiguration>()
        .GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "Connection string 'Postgres' is required.");

    return NpgsqlDataSource.Create(connectionString);
});

// Add services to the container.
builder.Services.AddScoped<UnitOfWork>();

builder.Services.AddScoped<IUnitOfWork>(provider =>
    provider.GetRequiredService<UnitOfWork>());
    
builder.Services.AddScoped<IPartRepository, PartRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddScoped<IReservationService, ReservationService>();

builder.Services.AddProblemDetails();
    
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            JsonNamingPolicy.CamelCase;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(
                context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request."
            };

            problem.Extensions["code"] = "INVALID_REQUEST";

            var result = new BadRequestObjectResult(problem);

            result.ContentTypes.Add("application/problem+json");

            return result;
        };
    });
    
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
