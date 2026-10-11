using Microsoft.AspNetCore.Mvc;
using RaceDay.NewFolder;

namespace RaceDay.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly DataStore _data;

        public WeatherForecastController(IHttpClientFactory httpClientFactory, IConfiguration configuration, DataStore data)
        {
            _httpClient = httpClientFactory.CreateClient("OpenWeatherMap");
            _configuration = configuration;
            _data = data;
        }

        [HttpGet("~/api/events/{id}/weather")]
        public async Task<IActionResult> GetEventWeather(int id)
        {
            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == id);
            
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            if (eventItem.Route == null)
                return NotFound(new { message = "Route not found" });

            var apiKey = _configuration["OpenWeatherMap:ApiKey"];
            var url = $"data/2.5/weather?lat={eventItem.Route.StartLatitude}&lon={eventItem.Route.StartLongitude}&appid={apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { message = "OpenWeather service unavailable or API key invalid" });

            var json = await response.Content.ReadAsStringAsync();
            var forecast = System.Text.Json.JsonSerializer.Deserialize<OpenWeatherResponse>(json);

            return Ok(forecast);
        }
    }
}
