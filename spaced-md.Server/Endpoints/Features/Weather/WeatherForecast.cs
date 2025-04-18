using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using System.Linq;

namespace spaced_md.Server
{
    public class WeatherForecastEndpoint : IEndpoint
    {

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("weatherforecast", Handler)
                .Produces<WeatherForecast[]>()
                .WithTags("Weather");
        }

        public static IResult Handler(IServer sender, CancellationToken cancellationToken)
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();

            return Results.Ok(forecast);
        }
    }

    public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }

}