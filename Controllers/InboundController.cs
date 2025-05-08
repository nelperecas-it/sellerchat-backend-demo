using System.Text;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SCIABackendDemo.Services;
using Microsoft.AspNetCore.SignalR;
using SCIABackendDemo.Hubs;
using SCIABackendDemo.Data;
using SCIABackendDemo.Models;
using Microsoft.EntityFrameworkCore;


namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InboundController : ControllerBase
    {
        private readonly UltravoxService _ultravoxService;
        private readonly IHubContext<CallHub> _callHubContext;
        private readonly LlamadaService _llamadaService;

        private readonly SellerCallDbContext _db;


        public InboundController(
            UltravoxService ultravoxService,
            IHubContext<CallHub> callHubContext,
            LlamadaService llamadaService,
            SellerCallDbContext db)
        {
            _ultravoxService = ultravoxService;
            _callHubContext  = callHubContext;
            _llamadaService = llamadaService;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Inbound()
        {
            // 1) Registrar la petición de Twilio
            Log.Information("Webhook Twilio inbound hit. Params: {@Form}", Request.Form);

            // 1.5) Extrae el CallSid de Twilio
            var form    = await Request.ReadFormAsync();
            var callSid = form["CallSid"].ToString();

            // 2) Crear llamada entrante en Ultravox y obtener joinUrl
            var (callId, joinUrl) = await _ultravoxService.CreateIncomingSipCallAsync();
            Log.Information("Received joinUrl from Ultravox: {Url}", joinUrl);

            // 3) Guarda asociación inbound
            _db.CallMappings.Add(new CallMappingEntry
            {
                CallId = callId,
                CallSid = callSid,
                Direction = "inbound-api"
            });
            await _db.SaveChangesAsync();


            

            await _llamadaService.GuardarHistorialLlamada(
                userId: 1, // Usuario fijo por ahora
                callId: callId,
                startedAt: DateTime.UtcNow,
                endedAt: null,
                resumen: null,
                resumenCorto: null,
                from: form["From"].ToString(),
                to: form["To"].ToString(),
                direction: "inbound-api",
                isActive: true  // La llamada está activa
            );

            var callHistory = await _db.CallHistories
            .FirstOrDefaultAsync(c => c.CallId == callId);
                if (callHistory != null)
                {
                    // Enviar la información completa de la llamada al frontend
                    await _llamadaService.SendCallInfoToFrontend(callHistory);
                }

           await _llamadaService.GuardarLlamadaAutomaticaSiCorresponde(
                userId: 1,
                callId: callId,
                phone: form["From"].ToString(),
                direction: "inbound-api"
            );





            // 3) Responder TwiML apuntando al joinUrl
            var twiml = new StringBuilder();
            twiml.AppendLine("<Response>");
            twiml.AppendLine($"  <Dial action=\"https://scia-back.nelperecas.com/api/llamada/ended\" method=\"POST\">");
            twiml.AppendLine($"    <Sip>{joinUrl}</Sip>");
            twiml.AppendLine("  </Dial>");
            twiml.AppendLine("</Response>");

            return Content(twiml.ToString(), "application/xml");
        }
    }
}
