using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Models;
using RaceDay.NewFolder;

namespace RaceDay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EnrolmentsController : ControllerBase
    {
        private readonly DataStore _data;

        public EnrolmentsController(DataStore data)
        {
            _data = data;
        }

        [HttpGet]
        public IActionResult GetAllEnrolments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var user = _data.Users.FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Role != "Organiser")
                return StatusCode(403, new { message = "Only organisers can view all enrolments" });

            return Ok(_data.Enrolments
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.ParticipantId,
                    e.EventId,
                    e.CategoryId,
                    e.EnrolmentDate,
                    e.Status,
                    Participant = _data.Participants
                    .Where(p => p.ParticipantId == e.ParticipantId)
                    .Select(p => new { p.ParticipantId, p.FullName })
                    .FirstOrDefault(),
                    Event = _data.Events
                    .Where(ev => ev.EventId == e.EventId)
                    .Select(ev => new { ev.EventId, ev.Name })
                    .FirstOrDefault(),
                    Category = _data.Categories
                    .Where(c => c.CategoryId == e.CategoryId)
                    .Select(c => new { c.CategoryId, c.Name })
                    .FirstOrDefault(),
                    Result = _data.Results
                    .Where(r => r.EnrolmentId == e.EnrolmentId)
                     .Select(r => new { r.ResultId, r.FinishTime, r.FinishPosition })
                     .FirstOrDefault()
                })
            .ToList());
        }

        [HttpGet("{id}")]
        public IActionResult GetEnrolment(int id)
        {
            var enrolment = _data.Enrolments.FirstOrDefault(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found" });

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var enrolParticipant = _data.Participants.FirstOrDefault(p => p.ParticipantId == enrolment.ParticipantId);
            var enrolEvent = _data.Events.FirstOrDefault(e => e.EventId == enrolment.EventId);
            var enrolOrganiser = _data.Organisers.FirstOrDefault(o => o.OrganiserId == enrolEvent?.OrganiserId);

            bool isOwner = enrolParticipant != null && enrolParticipant.UserId == userId.Value;
            bool isEventOrganiser = enrolOrganiser != null && enrolOrganiser.UserId == userId.Value;
            if (!isOwner && !isEventOrganiser)
                return StatusCode(403, new { message = "You can only view your own enrolment, or enrolments for your event" });

            return Ok(new
            {
                enrolment.EnrolmentId,
                enrolment.ParticipantId,
                enrolment.EventId,
                enrolment.CategoryId,
                enrolment.EnrolmentDate,
                enrolment.Status,
                Participant = _data.Participants
                .Where(p => p.ParticipantId == enrolment.ParticipantId)
                .Select(p => new { p.ParticipantId, p.FullName })
                .FirstOrDefault(),
                Event = _data.Events
                .Where(ev => ev.EventId == enrolment.EventId)
                 .Select(ev => new { ev.EventId, ev.Name })
                 .FirstOrDefault(),
                Category = _data.Categories
                .Where(c => c.CategoryId == enrolment.CategoryId)
                 .Select(c => new { c.CategoryId, c.Name })
                 .FirstOrDefault(),
                Result = _data.Results
                .Where(r => r.EnrolmentId == enrolment.EnrolmentId)
                .Select(r => new { r.ResultId, r.FinishTime, r.FinishPosition })
                .FirstOrDefault()
            });
        }

        [HttpPost("~/api/events/{eventId}/enrolments")]
        public IActionResult Enrol(int eventId, [FromBody] EnrolRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var user = _data.Users.FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Role != "Participant")
                return StatusCode(403, new { message = "Only participants can enrol in events" });

            var participant = _data.Participants.FirstOrDefault(p => p.UserId == userId.Value);
            if (participant == null)
                return StatusCode(403, new { message = "Participant profile not found" });

            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == eventId);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            var category = _data.Categories.FirstOrDefault(c => c.CategoryId == request.CategoryId);
            if (category == null)
                return NotFound(new { message = "Category not found" });

            if (category.EventId != eventId)
                return BadRequest(new { message = "Category does not belong to this event" });

            if (_data.Enrolments.Any(e => e.ParticipantId == participant.ParticipantId && e.EventId == eventId))
                return Conflict(new { message = "Already enrolled in this event" });

            // Age validation
            int age = CalculateAge(participant.DateOfBirth);
            if (category.MinAge.HasValue && age < category.MinAge.Value)
                return BadRequest(new { message = $"You must be at least {category.MinAge} years old" });
            if (category.MaxAge.HasValue && age > category.MaxAge.Value)
                return BadRequest(new { message = $"You must be {category.MaxAge} years or younger" });

            var enrolment = new Enrolment
            {
                EnrolmentId = _data.EnrolmentIdCounter++,
                ParticipantId = participant.ParticipantId,
                EventId = eventId,
                CategoryId = request.CategoryId,
                EnrolmentDate = DateTime.Now,
                Status = "Pending"
            };

            _data.Enrolments.Add(enrolment);

            return Created(string.Empty, new
            {
                enrolment.EnrolmentId,
                enrolment.EventId,
                enrolment.CategoryId,
                enrolment.Status,
                enrolment.EnrolmentDate
            });
        }

        [HttpGet("~/api/users/me/enrolments")]
        public IActionResult GetMyEnrolments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var participant = _data.Participants.FirstOrDefault(p => p.UserId == userId.Value);
            if (participant == null)
                return StatusCode(403, new { message = "Participant profile not found" });

            var enrolments = _data.Enrolments
                .Where(e => e.ParticipantId == participant.ParticipantId)
                .Select(e => new
                {
                    e.EnrolmentId,
                    Event = _data.Events
                    .Where(ev => ev.EventId == e.EventId)
                    .Select(ev => new { ev.EventId, ev.Name, ev.EventDate, ev.Location })
                    .FirstOrDefault(),
                    Category = _data.Categories
                    .Where(c => c.CategoryId == e.CategoryId)
                    .Select(c => new { c.CategoryId, c.Name }).FirstOrDefault(),
                    Result = _data.Results
                    .Where(r => r.EnrolmentId == e.EnrolmentId)
                    .Select(r => new { r.ResultId, r.FinishTime, r.FinishPosition })
                    .FirstOrDefault(),
                    e.Status,
                    e.EnrolmentDate
                })
                .ToList();

            return Ok(enrolments);
        }

        [HttpGet("~/api/events/{eventId}/enrolments")]
        public IActionResult GetEventEnrolments(int eventId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var user = _data.Users.FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Role != "Organiser")
                return StatusCode(403, new { message = "Only organisers can view all enrolments" });

            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == eventId);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            var organiser = _data.Organisers.FirstOrDefault(o => o.OrganiserId == eventItem.OrganiserId);
            if (organiser == null || organiser.UserId != userId.Value)
                return StatusCode(403, new { message = "Only the organiser who owns this event can view enrolments" });

            var enrolments = _data.Enrolments
                .Where(e => e.EventId == eventId)
                .Select(e => new
                {
                    e.EnrolmentId,
                    Participant = _data.Participants
                    .Where(p => p.ParticipantId == e.ParticipantId)
                    .Select(p => new { p.ParticipantId, p.FullName, p.PhoneNumber })
                    .FirstOrDefault(),
                    Category = _data.Categories
                    .Where(c => c.CategoryId == e.CategoryId)
                    .Select(c => new { c.CategoryId, c.Name })
                    .FirstOrDefault(),
                    e.Status,
                    e.EnrolmentDate
                })
                .ToList();

            return Ok(enrolments);
        }

        [HttpDelete("{id}")]
        public IActionResult CancelEnrolment(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var enrolment = _data.Enrolments.FirstOrDefault(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found" });

            var participant = _data.Participants.FirstOrDefault(p => p.ParticipantId == enrolment.ParticipantId);
            if (participant == null || participant.UserId != userId.Value)
                return StatusCode(403, new { message = "You can only cancel your own enrolments" });

            _data.Enrolments.Remove(enrolment);
            return NoContent();
        }

        [HttpPut("{id}/status")]
        public IActionResult UpdateEnrolmentStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return Unauthorized(new { message = "You must be logged in" });

            var user = _data.Users.FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Role != "Organiser")
                return StatusCode(403, new { message = "Only organisers can update enrolment status" });

            var enrolment = _data.Enrolments.FirstOrDefault(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found" });

            var eventItem = _data.Events.FirstOrDefault(e => e.EventId == enrolment.EventId);
            if (eventItem == null)
                return NotFound(new { message = "Event not found" });

            var organiser = _data.Organisers.FirstOrDefault(o => o.OrganiserId == eventItem.OrganiserId);
            if (organiser == null || organiser.UserId != userId.Value)
                return StatusCode(403, new { message = "Only the organiser who owns this event can update enrolment status" });

            if (request.Status != "Pending" && request.Status != "Confirmed" && request.Status != "Cancelled")
                return BadRequest(new { message = "Status must be 'Pending', 'Confirmed', or 'Cancelled'" });

            enrolment.Status = request.Status;

            return Ok(new
            {
                enrolment.EnrolmentId,
                enrolment.Status,
                enrolment.EnrolmentDate
            });
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        public class EnrolRequest
        {
            public int CategoryId { get; set; }
        }

        public class UpdateStatusRequest
        {
            public string Status { get; set; }
        }
    }
}
