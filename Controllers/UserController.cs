using Microsoft.AspNetCore.Mvc;
using SCIABackendDemo.Data;
using SCIABackendDemo.Models;
using Serilog;

namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly SellerCallDbContext _db;

        public UserController(SellerCallDbContext db)
        {
            _db = db;
        }

        //Ver que configuración tiene guardada
        [HttpGet("autotrigger/{userId}")]
        public async Task<IActionResult> GetAutoTriggerConfig(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Usuario no encontrado");
            }

            return Ok(new
            {
                enabled = user.AutoTriggerEnabled,
                days = user.AutoTriggerDays
            });
        }

        [HttpPost("toggle-autotrigger/{userId}")]
        public async Task<IActionResult> ToggleAutoTrigger(int userId, [FromBody] AutoTriggerDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("Usuario no encontrado al intentar configurar autotrigger: {UserId}", userId);
                return NotFound("Usuario no encontrado");
            }

            user.AutoTriggerEnabled = dto.Enabled;
            user.AutoTriggerDays = dto.Days;

            await _db.SaveChangesAsync();
            Log.Information("Usuario {UserId} actualizó autoTrigger: {Estado}, {Días}", userId, dto.Enabled, dto.Days);
            return Ok(new { message = "Preferencia de llamadas automáticas actualizada." });
        }

        public class AutoTriggerDto
        {
            public bool Enabled { get; set; }
            public int Days { get; set; }
        }
    }
}
