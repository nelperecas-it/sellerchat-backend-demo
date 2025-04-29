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


namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LlamadaController : ControllerBase
    {
        private readonly IHubContext<LogHub> _hubContext;
        private readonly UltravoxService      _ultravoxService;
        private readonly IConfiguration       _configuration;

        public LlamadaController(
            IHubContext<LogHub> hubContext,
            UltravoxService ultravoxService,
            IConfiguration configuration)
        {
            _hubContext       = hubContext;
            _ultravoxService  = ultravoxService;
            _configuration    = configuration;
        }

        // GET api/llamada/listcalls
        [HttpGet("listcalls")]
        public async Task<IActionResult> ListCalls()
        {
            Log.Information("Obteniendo lista de llamadas desde Ultravox.");
            await _hubContext.Clients.All.SendAsync("ReceiveLog", "Obteniendo lista de llamadas...");

            string result = await _ultravoxService.ListCallsAsync();
            Log.Information("Respuesta de Ultravox: {Result}", result);

            using JsonDocument jsonDoc = JsonDocument.Parse(result);
            JsonElement arrayToProcess;

            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object &&
                jsonDoc.RootElement.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array)
            {
                arrayToProcess = results;
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                arrayToProcess = jsonDoc.RootElement;
            }
            else
            {
                Log.Error("La respuesta de Ultravox no contiene un arreglo en 'results'. Recibido: {Json}", jsonDoc.RootElement);
                return BadRequest("Formato inesperado de datos.");
            }

            var filteredCalls = new List<FilteredCall>();
            foreach (var callElement in arrayToProcess.EnumerateArray())
            {
                filteredCalls.Add(new FilteredCall
                {
                    CallID       = callElement.GetProperty("callId").GetString(),
                    Created      = callElement.GetProperty("created").GetString(),
                    Jorned       = callElement.GetProperty("joined").GetString(),
                    Ended        = callElement.GetProperty("ended").GetString(),
                    Endrason     = callElement.GetProperty("endReason").GetString(),
                    Maxduration  = callElement.GetProperty("maxDuration").GetString(),
                    SystemPrompt = callElement.GetProperty("systemPrompt").GetString(),
                    ShortSummary = callElement.TryGetProperty("shortSummary", out var ss) && ss.ValueKind != JsonValueKind.Null
                                     ? ss.GetString()
                                     : null,
                    Summary      = callElement.TryGetProperty("summary",    out var s)  && s .ValueKind != JsonValueKind.Null
                                     ? s .GetString()
                                     : null
                });

            }
            foreach (var fc in filteredCalls)
            {
                // 1) Busca el callSid en inbound o outbound
                string? sid = null;
                if (CallMapping.InboundMap.TryGetValue(fc.CallID!, out var inSid))
                    sid = inSid;
                else if (CallMapping.OutboundMap.TryGetValue(fc.CallID!, out var outSid))
                    sid = outSid;

                // 2) Si lo encontramos, pide a Twilio los números
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
            return Ok(filteredCalls);
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

            var (callId, joinUrl) = await _ultravoxService.CreateIncomingSipCallAsync();

            // 1) Construye el XML de TwiML con interpolación de cadenas
            var twimlXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <Response>
                <Dial callerId=""{_configuration["Twilio:PhoneNumber"]}"">
                    <Sip>{joinUrl}</Sip>
                </Dial>
                </Response>";

            // 2) Envuelve el string en el tipo correcto
            var twiml = new Twilio.Types.Twiml(twimlXml);

            // 3) Lanza la llamada
            var call = await CallResource.CreateAsync(
                to:   new PhoneNumber(req.Phone),
                from: new PhoneNumber(_configuration["Twilio:PhoneNumber"]),
                twiml: twiml
            );

            // Guarda asociación outbound
            CallMapping.OutboundMap[callId] = call.Sid;

            return Ok(new { callId, callSid = call.Sid });
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

    }
}
