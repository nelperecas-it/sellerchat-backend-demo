using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Serilog;
using Twilio;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using SCIABackendDemo.Hubs;
using SCIABackendDemo.Services;
using SCIABackendDemo.Configuration;
using SCIABackendDemo.Data;
using SCIABackendDemo.Models;
using Microsoft.EntityFrameworkCore;


namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LlamadaController : ControllerBase
    {
        private readonly IHubContext<LogHub> _hubContext;
        private readonly UltravoxService      _ultravoxService;
        private readonly IConfiguration       _configuration;
        private readonly IHubContext<CallHub> _callHubContext;
        private readonly LlamadaService _llamadaService;
        private readonly SellerCallDbContext _db;


        public LlamadaController(
            IHubContext<LogHub> hubContext,
            IHubContext<CallHub> callHubContext,
            UltravoxService ultravoxService,
            IConfiguration configuration,
            LlamadaService llamadaService,
            SellerCallDbContext db)
        {
            _hubContext       = hubContext;
            _callHubContext   = callHubContext;
            _ultravoxService  = ultravoxService;
            _configuration    = configuration;
            _llamadaService   = llamadaService;
            _db = db;

        }

        // GET api/llamada/listcalls
        [HttpGet("listcalls")]
        public async Task<IActionResult> ListCalls()
        {
            Log.Information("Obteniendo lista de llamadas desde la base de datos.");
            await _hubContext.Clients.All.SendAsync("ReceiveLog", "Obteniendo lista de llamadas...");

            var llamadas = await _db.CallHistories
                .Where(c => c.UserId == 1) // usar el ID del usuario actual en el futuro
                .OrderByDescending(c => c.StartedAt)
                .ToListAsync();

            var filteredCalls = llamadas.Select(call => new FilteredCall
            {
                CallID       = call.CallId,
                Created      = call.StartedAt.ToString("o"),
                Jorned       = call.StartedAt.ToString("o"),
                Ended        = call.EndedAt?.ToString("o"),
                Endrason     = "", // puedes agregar esto si lo guardas
                Maxduration  = "", // puedes calcularlo con EndedAt - StartedAt
                SystemPrompt = "", // si decides guardar esto
                ShortSummary = call.ShortSummary,
                Summary      = call.Summary,
                TwilioSid    = null,
                TwilioFrom   = call.From,
                TwilioTo     = call.To,
                Direction    = call.Direction,
                UserId       = call.UserId
            }).ToList();

            foreach (var fc in filteredCalls)
            {
                string? sid = null;
                if (CallMapping.InboundMap.TryGetValue(fc.CallID!, out var inSid))
                    sid = inSid;
                else if (CallMapping.OutboundMap.TryGetValue(fc.CallID!, out var outSid))
                    sid = outSid;

                if (sid != null)
                {
                    var twCall = await CallResource.FetchAsync(sid);
                    fc.TwilioSid   = sid;
                    fc.TwilioFrom  = twCall.From;
                    fc.TwilioTo    = twCall.To;
                    fc.Direction   = twCall.Direction.ToString();
                }
            }

            await _hubContext.Clients.All.SendAsync("ReceiveLog", "Lista de llamadas obtenida y filtrada.");

            // Buscar llamadas que no tienen resumen ni fecha de finalización
                var llamadasIncompletas = filteredCalls
                    .Where(c => string.IsNullOrWhiteSpace(c.Summary) || string.IsNullOrWhiteSpace(c.Ended))
                    .ToList();

                if (llamadasIncompletas.Any())
                {
                    // Consultar a Ultravox para obtener datos actualizados
                    string result = await _ultravoxService.ListCallsAsync();
                    using JsonDocument jsonDoc = JsonDocument.Parse(result);

                    JsonElement arrayToProcess;
                    if (jsonDoc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                        arrayToProcess = results;
                    else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                        arrayToProcess = jsonDoc.RootElement;
                    else
                        arrayToProcess = default;

                    foreach (var call in llamadasIncompletas)
                    {
                        var callMatch = arrayToProcess.EnumerateArray()
                            .FirstOrDefault(x => x.GetProperty("callId").GetString() == call.CallID);

                        if (callMatch.ValueKind != JsonValueKind.Undefined)
                        {
                            var summary = callMatch.TryGetProperty("summary", out var s) && s.ValueKind != JsonValueKind.Null
                                ? s.GetString()
                                : null;

                            var shortSummary = callMatch.TryGetProperty("shortSummary", out var ss) && ss.ValueKind != JsonValueKind.Null
                                ? ss.GetString()
                                : null;

                            var endedAt = callMatch.TryGetProperty("ended", out var e) && e.ValueKind != JsonValueKind.Null
                                ? e.GetDateTime()
                                : (DateTime?)null;

                            var entity = await _db.CallHistories.FirstOrDefaultAsync(c => c.CallId == call.CallID);
                            if (entity != null)
                            {
                                entity.Summary = summary;
                                entity.ShortSummary = shortSummary;
                                entity.EndedAt = endedAt;
                                await _db.SaveChangesAsync();
                            }

                            call.Summary = summary;
                            call.ShortSummary = shortSummary;
                            call.Ended = endedAt?.ToString("o");
                        }
                    }
                }


            return Ok(filteredCalls);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveCalls()
        {
            var activeCalls = await _db.CallHistories
                .Where(c => c.UserId == 1 && c.IsActive)  // Filtra las llamadas activas para el usuario 1
                .ToListAsync();

            var activeCallIds = activeCalls.Select(call => call.CallId).ToList();

            return Ok(activeCallIds); // Devuelve solo los CallIds de las llamadas activas
        }


        //POST llamada finalizada para notificar con Signal R
        [HttpPost("ended")]
        public async Task<IActionResult> CallEnded([FromForm] TwilioEndCallback data)
        {
            var callSid = data.CallSid;
            Log.Information("Llamada finalizada desde Twilio. SID: {CallSid}", callSid);

            var callId = await _db.CallMappings
                .Where(x => x.CallSid == data.CallSid)
                .Select(x => x.CallId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(callId))
            {
                Log.Warning("No se encontró mapeo para CallSid: {CallSid}", data.CallSid);
                return Ok(); // evita error 500
            }

            // aquí ya puedes usar callId
            await _callHubContext.Clients.All.SendAsync("ReceiveCallStatus", callId, false);

             // Buscar la llamada en la base de datos y actualizarla
            var callHistory = await _db.CallHistories
                .FirstOrDefaultAsync(c => c.CallId == callId);

            if (callHistory != null)
            {
                // Marcar la llamada como inactiva y registrar la fecha de finalización
                callHistory.IsActive = false;
                callHistory.EndedAt = DateTime.UtcNow;

                // Obtener el resumen, resumen corto y fecha de finalización desde Ultravox
                var callDetails = await _ultravoxService.GetCallDetailsAsync(callId); // Aquí estamos usando el nuevo método

                // Actualizar los valores en la base de datos
                callHistory.Summary = callDetails.Summary;
                callHistory.ShortSummary = callDetails.ShortSummary;
                callHistory.EndedAt = callDetails.EndedAt;

                // Guardar los cambios en la base de datos
                await _db.SaveChangesAsync();

                // Enviar señal de llamada finalizada al frontend (SignalR)
                await _callHubContext.Clients.All.SendAsync("ReceiveCallStatus", callId, false);
                await _callHubContext.Clients.All.SendAsync("ReceiveCallInfo", callHistory);
            }

            return Ok();
        }


        // POST api/llamada/AfterDial
        [HttpPost("AfterDial")]
            public async Task<IActionResult> AfterDial()
            {
                // 1) Genera el MP3 con tu UltravoxService:
                var prompt = _configuration["Ultravox:SystemPrompt"] ?? throw new Exception("Falta SystemPrompt en configuración");
                var voiceId = _configuration["Ultravox:VoiceId"] ?? throw new Exception("Falta VoiceId en configuración");

                var welcomeUrl = await _ultravoxService.SynthesizeSpeechUrlAsync(prompt, voiceId);

                var warningUrl = await _ultravoxService.SynthesizeSpeechUrlAsync(
                    "La llamada se cortará en breve, gracias por tu paciencia.",
                    voiceId
                );                

                // 2) Monta el TwiML:
                var response = new VoiceResponse();
                response.Play(new Uri(welcomeUrl));      // ← aquí
                response.Pause(length: 8);               // espera 8s
                response.Play(new Uri(warningUrl));      // ← y aquí
                response.Hangup();               // cuelga la llamada

                return Content(response.ToString(), "application/xml");
            }


        // POST api/llamada/callto
       [HttpPost("callto")]
        public async Task<IActionResult> CallTo([FromBody] CreateCallRequest req)
        {
             try
             {
            if (string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest("El número destino es obligatorio.");

            await _llamadaService.EjecutarLlamadaAsync(
                userId: 1, // reemplázalo con el ID real si luego hay autenticación
                phone: req.Phone
            );

            await _llamadaService.GuardarLlamadaAutomaticaSiCorresponde(
                userId: 1,
                callId: Guid.NewGuid().ToString(),
                phone: req.Phone,
                direction: "outbound-api"
            );



            return Ok("Llamada iniciada.");

        }
        catch (Exception ex)
        {
                // guardar en log file (logs/callia-*.txt) la excepción completa
                Log.Error(ex, "Error en POST /api/llamada/callto con phone={Phone}", req?.Phone);
                // También aparecerá en journalctl
                return StatusCode(500, "Ocurrió un error interno. Revisa los logs.");
            }}
    }

    public class CreateCallRequest
    {
        public string Phone { get; set; } = null!;
    }

    public class FilteredCall
    {
        public string? CallID       { get; set; }
        public string? Created      { get; set; }
        public string? Jorned       { get; set; }
        public string? Ended        { get; set; }
        public string? Endrason     { get; set; }
        public string? Maxduration  { get; set; }
        public string? SystemPrompt { get; set; }
        public string? ShortSummary { get; set; }
        public string? Summary      { get; set; }
        public string? TwilioSid   { get; set; }
        public string? TwilioFrom  { get; set; }
        public string? TwilioTo    { get; set; }
        public string? Direction   { get; set; }
        public int? UserId { get; set; } 
    }
    // Calse de datos llamada finalizada
    public class TwilioEndCallback
    {
        [FromForm(Name = "CallSid")]
        public string CallSid { get; set; } = null!;
    }

}
