# SellerCall IA Backend Demo

## 📘 Descripción General

**SellerCall IA Backend Demo** es el sistema de backend encargado de gestionar las llamadas entrantes y salientes usando la integración entre **Twilio** y **Ultravox**.  
Este servicio permite conectar a clientes reales con un asistente virtual de voz inteligente, manejar prompts dinámicos y ofrecer una experiencia conversacional flexible.

Actualmente, este backend se integra con un frontend (el cual tiene su propio README) y se está desplegando en una **VPS Ubuntu 27** como un proceso de pruebas.

---

## 🧪 Tecnologías Utilizadas

- **ASP.NET Core 9.0**
- **SignalR** para comunicación en tiempo real con el frontend
- **Twilio SDK** para gestión de llamadas telefónicas
- **Ultravox API** para generación de llamadas SIP y servicios TTS (texto a voz)
- **Serilog** para logging estructurado en consola y archivos
- **CORS** configurado para permitir conexión desde frontend autorizado

---

## 📂 Estructura del Proyecto

| Carpeta/Archivo | Descripción |
|:----------------|:------------|
| `Controllers/` | Controladores de API principales (`InboundController`, `LlamadaController`, `PromptController`). |
| `Services/` | Servicios de conexión externa (`UltravoxService`, `PromptService`). |
| `Configuration/` | Configuración de opciones (`UltravoxOptions`). |
| `Hubs/` | Comunicación en tiempo real vía SignalR (`LogHub`). |
| `Program.cs` | Configuración principal del servidor, servicios, middleware y endpoints. |
| `appsettings.json` | Configuraciones de API Keys y parámetros de Twilio y Ultravox. |

---

## 🔌 Endpoints Disponibles

### 📞 Gestión de llamadas

### **POST** `/api/inbound`

Recibe llamadas entrantes desde Twilio, crea llamada en Ultravox.

**Ejemplo de uso:**

> Twilio hace un webhook `POST` enviando datos de la llamada entrante.

**Respuesta (XML):**
```xml
<Response>
  <Dial>
    <Sip>sip:abc123@sip.ultravox.ai</Sip>
  </Dial>
</Response>
```

---

### **POST** `/api/llamada/callto`

Inicia una llamada saliente conectada a Ultravox.

**Request ejemplo:**
```json
{
  "Phone": "+573001234567"
}
```

**Respuesta ejemplo:**
```json
{
  "callId": "d35082c5-4f47-4eaa-a06f-28c982123456",
  "callSid": "CA1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6"
}
```

---

### **GET** `/api/llamada/listcalls`

Lista las llamadas registradas en Ultravox y Twilio.

**Request ejemplo:**
```http
GET /api/llamada/listcalls
```

**Respuesta ejemplo:**
```json
[
  {
    "callID": "d35082c5-4f47-4eaa-a06f-28c982123456",
    "created": "2024-04-27T12:00:00Z",
    "jorned": "2024-04-27T12:01:00Z",
    "ended": "2024-04-27T12:30:00Z",
    "endrason": "completed",
    "maxduration": "1800s",
    "systemPrompt": "Eres SellerCall...",
    "twilioSid": "CA1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6",
    "twilioFrom": "+13512082523",
    "twilioTo": "+573001234567",
    "direction": "outbound-api"
  }
]
```

---

### **POST** `/api/llamada/AfterDial`

Reproduce un mensaje grabado y cuelga la llamada.

**Request ejemplo:**
```http
POST /api/llamada/AfterDial
```

**Respuesta (XML):**
```xml
<Response>
  <Play>https://url-bienvenida.mp3</Play>
  <Pause length="8"/>
  <Play>https://url-aviso.mp3</Play>
  <Hangup/>
</Response>
```

---

### 🛠 Gestión del Prompt del Asistente

### **GET** `/api/prompt`

Obtiene el prompt actual del asistente.

**Request ejemplo:**
```http
GET /api/prompt
```

**Respuesta ejemplo:**
```json
{
  "systemPrompt": "Eres SellerCall, un asistente virtual especializado en atención de llamadas..."
}
```

---

### **POST** `/api/prompt`

Actualiza el prompt que usa el asistente.

**Request ejemplo:**
```json
{
  "systemPrompt": "Nuevo comportamiento del asistente para captar clientes potenciales."
}
```

**Respuesta:**
```http
204 No Content
```

---

## ⚙️ Configuración Inicial

El archivo `appsettings.json` debe contener las siguientes configuraciones:

```json
{
  "Ultravox": {
    "ApiKey": "TU_API_KEY_DE_ULTRAVOX",
    "VoiceId": "TU_VOICE_ID_DE_ULTRAVOX",
    "SystemPrompt": "Mensaje inicial del asistente."
  },
  "Twilio": {
    "AccountSid": "TU_ACCOUNT_SID_DE_TWILIO",
    "AuthToken": "TU_AUTH_TOKEN_DE_TWILIO",
    "PhoneNumber": "NÚMERO_DE_TWILIO",
    "TwiMLBinSid": "SID_DEL_TWIML_BIN"
  }
}
```

**Nota:** En ambiente de producción es recomendable proteger las claves sensibles mediante variables de entorno.

---

## 🚀 Cómo Correr el Proyecto

1. Clonar el repositorio:
   ```bash
   git clone https://github.com/tu-repositorio/sellercall-ia-backend-demo.git
   cd sellercall-ia-backend-demo
   ```

2. Restaurar paquetes y compilar:
   ```bash
   dotnet restore
   dotnet build
   ```

3. Correr el servidor:
   ```bash
   dotnet run
   ```

El backend quedará corriendo normalmente en `http://localhost:5000` o el puerto configurado.

---

## 📈 Flujo General de Llamadas

**Entrante:**
1. Cliente llama a número de Twilio.
2. Twilio envía webhook a `/api/inbound`.
3. Backend genera `joinUrl` en Ultravox.
4. Twilio conecta la llamada a Ultravox vía SIP.
5. Ultravox atiende usando IA de voz.

**Saliente:**
1. Desde el frontend se solicita `/api/llamada/callto` con el número destino.
2. Backend crea llamada SIP en Ultravox.
3. Twilio realiza la llamada al cliente y conecta a Ultravox.
4. Ultravox maneja la conversación.

---

## 📎 Notas Finales

- SellerCall IA Backend Demo permite una integración robusta de servicios de comunicación inteligente mediante IA de voz.
- La arquitectura es modular y escalable para soportar futuras integraciones o servicios adicionales.
- Se recomienda siempre validar que las API Keys de Twilio y Ultravox estén activas y configuradas correctamente en ambientes de pruebas o producción.

---
