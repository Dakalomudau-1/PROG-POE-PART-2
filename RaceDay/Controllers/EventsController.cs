using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Models;
using RaceDay.NewFolder;

namespace RaceDay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly DataStore _data;

        public EventsController(DataStore data)
        {
            _data = data;
        }

        [HttpGet]
        public IActionResult GetEvents(
            [FromQuery] string eventType = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string location = null)
        {
            var query = _data.Events.AsQueryable();

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);
            if (dateFrom.HasValue)
                query = query.Where(e => e.EventDate >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(e => e.EventDate <= dateTo.Value);
            if (!string.IsNullOrEmpty(location))
                query = query.Where(e => e.Location.Contains(location));

            var results = query.Select(e => new
            {
                e.EventId,
                e.Name,
                e.Description,
                e.EventDate,
                e.Location,
                e.DistanceKm,
                e.EventType,
                e.BannerImageUrl,
                OrganiserName = _data.Organisers.Where(o => o.OrganiserId == e.OrganiserId)
                .Select(o => o.FullName).FirstOrDefault()
            }).ToList();

            return Ok(results);
        }

        [HttpGet("{id}")]
        public IActionResult GetEvent(int id)
        {
            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == id);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            return Ok(new
            {
                eventItem.EventId,
                eventItem.Name,
                eventItem.Description,
                eventItem.EventDate,
                eventItem.Location,
                eventItem.DistanceKm,
                eventItem.EventType,
                eventItem.BannerImageUrl,
                eventItem.CreatedAt,
                Organiser = _data.Organisers
                .Where(o => o.OrganiserId == eventItem.OrganiserId)
                .Select(o => new { o.OrganiserId, o.FullName })
                .FirstOrDefault(),
                Categories = _data.Categories
                .Where(c => c.EventId == eventItem.EventId)
                .Select(c => new { c.CategoryId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm })
                .ToList(),
                Route = _data.Routes
                .Where(r => r.EventId == eventItem.EventId)
                .Select(r => new
                {
                    r.RouteId,
                    r.RouteName,
                    r.StartLatitude,
                    r.StartLongitude,
                    r.ElevationGain,
                    r.MapUrl,
                    Waypoints = _data.Waypoints
                    .Where(w => w.RouteId == r.RouteId)
                    .OrderBy(w => w.SequenceNo).Select(w => new { w.SequenceNo, w.Latitude, w.Longitude })
                })
                .FirstOrDefault()
            });
        }

        [HttpPost]
        public IActionResult CreateEvent([FromBody] CreateEventRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var user = _data.Users.FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Role != "Organiser")
                return StatusCode(403, new { message = "Only organisers can create events" });

            var organiser = _data.Organisers.FirstOrDefault(o => o.UserId == userId.Value);
            if (organiser == null)
                return StatusCode(403, new { message = "Organiser profile not found" });

            var validTypes = new[] { "Run", "Walk", "Cycle" };
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location) || !validTypes.Contains(request.EventType))
                return BadRequest(new { message = "Name and location are required, and eventType must be 'Run', 'Walk' or 'Cycle'" });

            var newEvent = new Event
            {
                EventId = _data.EventIdCounter++,
                OrganiserId = organiser.OrganiserId,
                Name = request.Name,
                Description = request.Description,
                EventDate = request.EventDate,
                Location = request.Location,
                DistanceKm = request.DistanceKm,
                EventType = request.EventType,
                CreatedAt = DateTime.Now
            };

            _data.Events.Add(newEvent);

            return Created(string.Empty, new
            {
                eventId = newEvent.EventId,
                newEvent.Name,
                newEvent.Description,
                newEvent.EventDate,
                newEvent.Location,
                newEvent.DistanceKm,
                newEvent.EventType,
                newEvent.CreatedAt
            });
        }

        [HttpPut("{id}")]
        public IActionResult UpdateEvent(int id, [FromBody] UpdateEventRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == id);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            var organiser = _data.Organisers.FirstOrDefault(o => o.OrganiserId == eventItem.OrganiserId);
            if (organiser == null || organiser.UserId != userId.Value)
                return StatusCode(403, new { message = "Only the organiser who owns this event can update it" });

            if (!string.IsNullOrEmpty(request.EventType) && !new[] { "Run", "Walk", "Cycle" }.Contains(request.EventType))
                return BadRequest(new { message = "eventType must be 'Run', 'Walk' or 'Cycle'" });

            if (!string.IsNullOrEmpty(request.Name))
                eventItem.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description))
                eventItem.Description = request.Description;
            if (request.EventDate.HasValue)
                eventItem.EventDate = request.EventDate.Value;
            if (!string.IsNullOrEmpty(request.Location))
                eventItem.Location = request.Location;
            if (request.DistanceKm.HasValue)
                eventItem.DistanceKm = request.DistanceKm.Value;
            if (!string.IsNullOrEmpty(request.EventType))
                eventItem.EventType = request.EventType;

            return Ok(eventItem);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteEvent(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == id);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            var organiser = _data.Organisers .FirstOrDefault(o => o.OrganiserId == eventItem.OrganiserId);
            if (organiser == null || organiser.UserId != userId.Value)
                return StatusCode(403, new { message = "Only the organiser who owns this event can delete it" });

            _data.Events.Remove(eventItem);
            return NoContent();
        }

        public class CreateEventRequest
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public DateTime EventDate { get; set; }
            public string Location { get; set; }
            public decimal DistanceKm { get; set; }
            public string EventType { get; set; }
        }

        public class UpdateEventRequest
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public DateTime? EventDate { get; set; }
            public string Location { get; set; }
            public decimal? DistanceKm { get; set; }
            public string EventType { get; set; }
        }
    }
}
