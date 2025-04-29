using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SCIABackendDemo.Configuration;
using Serilog;


namespace SCIABackendDemo.Services
{
    public class UltravoxService
    {
        private readonly HttpClient _httpClient;
        private readonly string     _apiKey;
        private readonly string     _voiceId;
        private readonly PromptService _promptService;

        public UltravoxService(HttpClient httpClient,
                               IOptions<UltravoxOptions> opts,
                               PromptService promptService)
        {
            _httpClient    = httpClient;
            _apiKey        = opts.Value.ApiKey;
            _voiceId       = opts.Value.VoiceId;
            _promptService = promptService;
        }

        public async Task<string> ListCallsAsync()
        {
            //Link a donde se haran peticiones
            const string url = "https://api.ultravox.ai/api/calls";
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        // Volver MP3 un texto para primera vo<
        public async Task<string> SynthesizeSpeechUrlAsync(string text, string voiceId)
        {
            var payload = new
            {
                text = text,
                voice = voiceId,
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/tts/synthesize")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var resp = await _httpClient.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            // Suponemos que la respuesta es { "url": "https://..." }
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var url = doc.RootElement.GetProperty("url").GetString();
            return url!;
        }
        // Método para crear una llamada SIP entrante en Ultravox
        public async Task<(string callId, string joinUrl)> CreateIncomingSipCallAsync()
        {
            // URL del endpoint de creación de llamadas
            const string url = "https://api.ultravox.ai/api/calls";

            // Creamos el payload de la solicitud
            var payload = new
            {
                systemPrompt = _promptService.CurrentPrompt, // Instrucción actual para la IA
                languageHint = "es",                        // Sugerencia de idioma (Español)
                voice        = _voiceId,                    // Voz configurada
                maxDuration  = "300s",                      // Máxima duración de la llamada
                medium = new { sip = new { incoming = new { } } } // Medio: SIP entrante
            };

            // Serializamos el payload a JSON
            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Configuramos encabezados de autenticación
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);

            // Hacemos la solicitud POST para crear la llamada
            var resp = await _httpClient.PostAsync(url, content);
            resp.EnsureSuccessStatusCode();

            // Leemos el cuerpo de la respuesta
            var body = await resp.Content.ReadAsStringAsync();

            // Registramos en los logs la respuesta de la API
            Log.Information("Ultravox CreateIncomingSipCallAsync response: {Body}", body);

            // Parseamos el JSON de respuesta
            using var doc = JsonDocument.Parse(body);

            // Extraemos el callId y el joinUrl desde la respuesta
            var callId  = doc.RootElement.GetProperty("callId").GetString()!;
            var joinUrl = doc.RootElement.GetProperty("joinUrl").GetString()!;

            // Devolvemos ambos valores como tupla
            return (callId, joinUrl);
        }
    }
}