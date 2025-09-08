
using EliteRentalsAPI.Data;
using EliteRentalsAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // PostgreSQL (Supabase)
            builder.Services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions => npgsqlOptions.CommandTimeout(180) // 2 minutes
                )
            );

            // JWT Auth
            builder.Services.AddScoped<TokenService>();

            builder.Services.AddCors(opt => {
                opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
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
}
