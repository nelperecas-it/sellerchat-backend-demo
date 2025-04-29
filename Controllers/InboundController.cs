using System.Text;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SCIABackendDemo.Services;

namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InboundController : ControllerBase
    {
        private readonly UltravoxService _ultravoxService;

        public InboundController(UltravoxService ultravoxService)
        {
            _ultravoxService = ultravoxService;
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
            CallMapping.InboundMap[callId] = callSid;

            // 3) Responder TwiML apuntando al joinUrl
            var twiml = new StringBuilder();
            twiml.AppendLine("<Response>");
            twiml.AppendLine("  <Dial>");
            twiml.AppendLine($"    <Sip>{joinUrl}</Sip>");
            twiml.AppendLine("  </Dial>");
            twiml.AppendLine("</Response>");

            return Content(twiml.ToString(), "application/xml");
        }
    }
}
