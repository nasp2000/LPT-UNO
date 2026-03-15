/*
 * Emulador de Impressora Paralela LPT (DB25) para Arduino Uno
 * LPT-UNO Firmware
 * 
 * Este código transforma o Arduino Uno num emulador de impressora paralela
 * que recebe dados pela interface paralela e os envia via USB Serial para o PC.
 * 
 * PINAGEM DO ARDUINO UNO:
 * ========================
 * Sinais de Controle:
 *   STROBE (DB25 pino 1)   -> Arduino Digital 2 (INT0 - Interrupt)
 *   ACK    (DB25 pino 10)  -> Arduino Digital 11 (Output)
 *   BUSY   (DB25 pino 11)  -> Arduino Digital 12 (Output)
 *   SELECT (DB25 pino 13)  -> Arduino Digital 13 (Output + LED)
 * 
 * Dados Paralelos (8 bits):
 *   D0 (DB25 pino 2)  -> Arduino Digital 3
 *   D1 (DB25 pino 3)  -> Arduino Digital 4
 *   D2 (DB25 pino 4)  -> Arduino Digital 5
 *   D3 (DB25 pino 5)  -> Arduino Digital 6
 *   D4 (DB25 pino 6)  -> Arduino Digital 7
 *   D5 (DB25 pino 7)  -> Arduino Digital 8
 *   D6 (DB25 pino 8)  -> Arduino Digital 9
 *   D7 (DB25 pino 9)  -> Arduino Digital 10
 * 
 * GND: DB25 pinos 18-25 -> Arduino GND
 * 
 * PROTOCOLO:
 * 1. PC coloca dados nos pinos D0-D7
 * 2. PC ativa STROBE (nível baixo)
 * 3. Arduino lê os dados
 * 4. Arduino ativa BUSY (nível alto)
 * 5. Arduino processa/envia dados via USB Serial
 * 6. Arduino ativa ACK (pulso baixo)
 * 7. Arduino desativa BUSY (nível baixo)
 */

// ===========================
// INFORMAÇÕES DE VERSÃO
// ===========================
#define FIRMWARE_VERSION "1.1"
#define BUILD_DATE __DATE__
#define BUILD_TIME __TIME__

// ===========================
// WIFI SUPPORT (UNO R4 WIFI)
// ===========================
#if defined(ARDUINO_UNOR4_WIFI)
#include "WiFiS3.h"
#include <EEPROM.h>
#include <WiFiUdp.h>
#include <ArduinoMDNS.h>

// Credentials storage
struct Config {
  char ssid[33];
  char pass[64];
  byte valid;  // 0xAA indicates valid config
} wifiConfig;

int status = WL_IDLE_STATUS;
bool wifiConnected = false;

// HTTP server para web interface via streaming bruto (porta 80)
WiFiServer webServer(80);
WiFiClient streamClient;
bool streamClientConnected = false;

// Discovery
WiFiUDP udp;
unsigned long lastBroadcast = 0;

// mDNS — acessível como lpt-uno.local
WiFiUDP mdnsUdp;
MDNS mdns(mdnsUdp);

// Reconexão automática WiFi (state machine não-bloqueante)
unsigned long lastWifiCheck    = 0;
bool          wifiAttempting   = false;
unsigned long wifiAttemptStart = 0;
const unsigned long WIFI_CONNECT_ATTEMPT_TIMEOUT_MS = 12000UL;
const unsigned long WIFI_RETRY_INTERVAL_MS          = 3000UL;
bool          _wifiServicesStarted = false;   // impede webServer/udp.begin() repetido
unsigned long _lastKeepaliveMs     = 0;       // keepalive TCP no stream (0x00 a cada 5s)
#endif

// Buffer de receção de comandos Serial (não-bloqueante)
String serialCmdBuffer = "";

#if defined(ARDUINO_UNOR4_WIFI)
void loadConfig() {
  EEPROM.get(0, wifiConfig);
}

void saveConfig(String s, String p) {
  memset(wifiConfig.ssid, 0, 33);
  memset(wifiConfig.pass, 0, 64);
  s.toCharArray(wifiConfig.ssid, 33);
  p.toCharArray(wifiConfig.pass, 64);
  wifiConfig.valid = 0xAA;
  EEPROM.put(0, wifiConfig);
}

void startWifiConnectAttempt() {
  WiFi.disconnect();
  delay(60);
  WiFi.begin(wifiConfig.ssid, wifiConfig.pass);
  wifiAttempting   = true;
  wifiAttemptStart = millis();
}

// Buffer de stream HTTP bruto — sem overhead SSE/texto.
// O browser lê bytes raw com fetch().body.getReader(), o que é muito mais leve.
#define STREAM_BUF_CAP 2048
#define STREAM_FLUSH_THRESHOLD ((STREAM_BUF_CAP * 3) / 4)  // flush preventivo a ~75%
static uint8_t _streamBuf[STREAM_BUF_CAP];
static int     _streamBufLen = 0;
unsigned long  lastStreamFlush = 0;

void sendStreamData(byte b) {
  if (!streamClientConnected || !streamClient.connected()) {
    streamClientConnected = false;
    _streamBufLen = 0;
    return;
  }
  if (_streamBufLen >= STREAM_BUF_CAP - 1) flushStream();
  if (_streamBufLen < STREAM_BUF_CAP) {
    _streamBuf[_streamBufLen++] = b;
  }
  if (_streamBufLen >= STREAM_FLUSH_THRESHOLD) flushStream();
}

void flushStream() {
  if (_streamBufLen == 0 || !streamClientConnected || !streamClient.connected()) {
    if (_streamBufLen > 0) _streamBufLen = 0;
    return;
  }
  streamClient.write(_streamBuf, _streamBufLen);
  _streamBufLen = 0;
  lastStreamFlush = millis();
}

// ── Estado do cliente HTTP pendente ─────────────────────────────────────────
// O request é acumulado ao longo de várias iterações do loop (não-bloqueante).
// Resolve o problema anterior: timeout de 5µs/5ms era insuficiente para o
// WiFiS3 entregar os headers completos → GET /events nunca era reconhecido
// → EventSource fechava imediatamente → ciclo de retry infinito.
WiFiClient    _httpClient;
String        _httpReq     = "";
unsigned long _httpReqT    = 0;
bool          _httpPending = false;

bool _httpHasRequestLine(const String& req) {
  return req.indexOf("\r\n") >= 0 || req.indexOf('\n') >= 0;
}

bool _httpIsRecognizedPath(const String& req) {
  return req.indexOf("GET /ping") >= 0
      || req.indexOf("GET /stream") >= 0
      || req.indexOf("GET /status") >= 0
      || req.indexOf("GET /ip") >= 0;
}

// Despacha o request HTTP completo (ou com timeout)
void _dispatchHttp(WiFiClient& c, const String& req) {
  if (req.indexOf("GET /ping") >= 0) {
    c.println("HTTP/1.1 200 OK");
    c.println("Content-Type: application/json");
    c.println("Access-Control-Allow-Origin: *");
    c.println("Connection: close");
    c.println();
    IPAddress ip = WiFi.localIP();
    c.print("{\"d\":\"LPT-UNO\",\"ip\":\"");
    c.print(ip);
    c.print("\"}");
    c.stop();
  } else if (req.indexOf("GET /stream") >= 0) {
    c.println("HTTP/1.1 200 OK");
    c.println("Content-Type: application/octet-stream");
    c.println("Cache-Control: no-cache");
    c.println("Access-Control-Allow-Origin: *");
    c.println("Connection: keep-alive");
    c.println();
    c.flush();  // forçar envio imediato dos headers ao browser
    if (streamClient && streamClient.connected()) streamClient.stop();
    streamClient = c;
    streamClientConnected = true;
  } else if (req.indexOf("GET /status") >= 0 || req.indexOf("GET /ip") >= 0) {
    IPAddress ip = WiFi.localIP();
    String s = "{\"version\":\"" + String(FIRMWARE_VERSION) + "\",\"ip\":\""
               + String(ip[0])+"."+String(ip[1])+"."+String(ip[2])+"."+String(ip[3]) + "\"}";
    c.println("HTTP/1.1 200 OK");
    c.println("Content-Type: application/json");
    c.println("Access-Control-Allow-Origin: *");
    c.println("Connection: close");
    c.println();
    c.print(s);
    c.stop();
  } else {
    c.println("HTTP/1.1 200 OK");
    c.println("Content-Type: text/plain");
    c.println("Access-Control-Allow-Origin: *");
    c.println("Connection: close");
    c.println();
    IPAddress ip = WiFi.localIP();
    c.print("LPT-UNO v"); c.println(FIRMWARE_VERSION);
    c.print("STREAM: http://"); c.print(ip); c.println("/stream");
    c.stop();
  }
}

// Trata ligações HTTP — completamente não-bloqueante.
// IMPORTANTE: não aceita novas ligações enquanto stream activo — o WiFiS3
// devolve o próprio socket stream em webServer.available(), o que levaria
// o código a tratá-lo como novo pedido e após 500ms chamar c.stop(),
// matando a ligação HTTP streaming.
void handleWebClient() {
  // Enquanto stream activo e saudável: não aceitar novas ligações
  if (streamClientConnected) {
    if (!streamClient.connected()) {
      streamClientConnected = false;
      _streamBufLen = 0;
      _httpPending = false;
    }
    return;
  }

  // Aceitar novo cliente apenas se não há pedido em curso
  if (!_httpPending) {
    WiFiClient nc = webServer.available();
    if (!nc) return;
    _httpClient  = nc;
    _httpReq     = "";
    _httpReqT    = millis();
    _httpPending = true;
  }

  // Ler apenas o que já está no buffer (nunca esperar)
  while (_httpPending && _httpClient.connected()
         && _httpClient.available() && _httpReq.length() < 1024) {
    _httpReq += (char)_httpClient.read();
    if (_httpReq.endsWith("\r\n\r\n") || (_httpHasRequestLine(_httpReq) && _httpIsRecognizedPath(_httpReq))) {
      _httpPending = false;
      _dispatchHttp(_httpClient, _httpReq);
      return;
    }
  }

  // Timeout 500ms ou cliente desligou: despachar com o que foi recebido
  if (_httpPending && (!_httpClient.connected() || millis() - _httpReqT > 500UL)) {
    _httpPending = false;
    _dispatchHttp(_httpClient, _httpReq);
  }
}
#endif

// ===========================
// DEFINIÇÃO DE PINOS
// ===========================

// Pinos de controle (Input do PC)
const int PIN_STROBE = 2;  // Sinal de dados prontos (active low) - INT0

// Pinos de dados (D0-D7)
const int DATA_PINS[] = { 3, 4, 5, 6, 7, 8, 9, 10 };
const int DATA_PIN_COUNT = 8;

// Pinos de status (Output para o PC)
const int PIN_ACK = 11;     // Acknowledge (active low pulse)
const int PIN_BUSY = 12;    // Impressora ocupada (active high)
const int PIN_SELECT = 13;  // Impressora selecionada (active high)

// ===========================
// CONFIGURAÇÃO DE TIMING
// ===========================
// ACK mínimo IEEE 1284 = 0.5µs; 2µs é suficiente e seguro
// BUSY_DELAY = tempo de processamento simulado antes do ACK
// Ambos reduzidos para 2µs: ISR total ~4-6µs → suporta até HIPER_RAPIDO (gap=10µs)
// Profile de velocidades (teste):
//   LENTO:        dataSettle=10µs  strobe=50µs  gap=200µs  ✓
//   NORMAL:       dataSettle= 5µs  strobe=20µs  gap=100µs  ✓
//   RAPIDO:       dataSettle= 2µs  strobe= 8µs  gap= 30µs  ✓
//   SUPER_RAPIDO: dataSettle= 1µs  strobe= 5µs  gap= 15µs  ✓
//   HIPER_RAPIDO: dataSettle= 1µs  strobe= 3µs  gap= 10µs  ✓
const unsigned long ACK_PULSE_MICROS = 2;    // Pulso ACK: 2µs (mínimo seguro)
const unsigned long BUSY_DELAY_MICROS = 2;   // Processamento: 2µs

// ===========================
// BUFFER DE DADOS
// ===========================
#if defined(ARDUINO_UNOR4_WIFI)
const int BUFFER_SIZE = 8192;   // R4 WiFi: margem real para bursts longos via SSE/WiFi
const int DRAIN_BATCH_SIZE = 384;
const int BUFFER_BUSY_HIGH_WATER = (BUFFER_SIZE * 3) / 4;  // 75%
const int BUFFER_BUSY_LOW_WATER  = BUFFER_SIZE / 2;        // 50% (histerese)
#else
const int BUFFER_SIZE = 256;
const int DRAIN_BATCH_SIZE = 32;
const int BUFFER_BUSY_HIGH_WATER = BUFFER_SIZE / 2;        // 50%
const int BUFFER_BUSY_LOW_WATER  = BUFFER_SIZE / 4;        // 25% (histerese)
#endif
volatile byte dataBuffer[BUFFER_SIZE];
volatile int bufferWriteIndex = 0;
volatile int bufferReadIndex = 0;
volatile bool dataAvailable = false;
volatile unsigned long bufferOverflowCount = 0;
volatile bool flowControlBusy = false;

// ===========================
// VARIÁVEIS DE ESTADO
// ===========================
volatile unsigned long lastStrobeTime = 0;
const unsigned long DEBOUNCE_MICROS = 2;   // Debounce de 2µs
volatile unsigned long strobeCounter = 0;  // Contador de interrupções (debug)

// ===========================
// SETUP
// ===========================
void setup() {
  // Inicializar comunicação serial USB (velocidade alta)
  Serial.begin(115200);
  // Aguardar Serial até 3s — permite arrancar sem PC ligado via USB
  { unsigned long _t = millis(); while (!Serial && (millis() - _t) < 3000) {} }

  // Configurar pinos de dados como INPUT
  for (int i = 0; i < DATA_PIN_COUNT; i++) {
    pinMode(DATA_PINS[i], INPUT);
  }

  // Configurar pinos de controle
  pinMode(PIN_STROBE, INPUT_PULLUP);  // STROBE é active low

  // Configurar pinos de status como OUTPUT
  pinMode(PIN_ACK, OUTPUT);
  pinMode(PIN_BUSY, OUTPUT);
  pinMode(PIN_SELECT, OUTPUT);

  // Estado inicial dos sinais
  digitalWrite(PIN_ACK, HIGH);     // ACK em repouso (high)
  digitalWrite(PIN_BUSY, LOW);     // Não ocupado
  digitalWrite(PIN_SELECT, HIGH);  // Selecionado e pronto

  // Configurar interrupção no pino STROBE (falling edge)
  attachInterrupt(digitalPinToInterrupt(PIN_STROBE), handleStrobe, FALLING);

  // Mensagem de inicialização
  Serial.println("===================================");
  Serial.println("LPT-UNO Emulator");
  Serial.print("Firmware: v");
  Serial.println(FIRMWARE_VERSION);
  Serial.print("Build: ");
  Serial.print(BUILD_DATE);
  Serial.print(" ");
  Serial.println(BUILD_TIME);
  Serial.println("===================================");
  Serial.println("Ready - Waiting for parallel data...");

#if defined(ARDUINO_UNOR4_WIFI)
  // Inicialização do WiFi (apenas R4 WiFi)
  if (WiFi.status() == WL_NO_MODULE) {
    Serial.println("Communication with WiFi module failed!");
  } else {
    // Firmware check disabled by default to avoid spurious messages for users.
    // To enable and require a minimum WiFi firmware version, define
    // WIFI_FIRMWARE_LATEST_VERSION and set WIFI_FIRMWARE_CHECK to 1 at the top
    // of this file. Example:
    //   #define WIFI_FIRMWARE_CHECK 1
    //   #define WIFI_FIRMWARE_LATEST_VERSION "1.0.0"
    // Then uncomment the lines below.
    // String fv = WiFi.firmwareVersion();
    // if (fv < WIFI_FIRMWARE_LATEST_VERSION) {
    //   Serial.println("Please upgrade the firmware");
    // }

    // Load credentials
    loadConfig();

    if (wifiConfig.valid == 0xAA) {
      Serial.print("Connecting to SSID: ");
      Serial.println(wifiConfig.ssid);

      // Ligar ao WiFi — polling não-bloqueante (máx. 12s)
      // ⚠️ Drena o buffer durante a espera para não perder dados paralelos recebidos
      //    neste periodo (a ISR está activa mas o loop() ainda não iniciou)
      startWifiConnectAttempt();
      {
        unsigned long _wStart = millis();
        while (WiFi.status() != WL_CONNECTED && (millis() - _wStart) < WIFI_CONNECT_ATTEMPT_TIMEOUT_MS) {
          delay(200);
          // Esvaziar buffer circular durante a espera
          while (bufferReadIndex != bufferWriteIndex) {
            Serial.write(dataBuffer[bufferReadIndex]);
            bufferReadIndex = (bufferReadIndex + 1) % BUFFER_SIZE;
          }
        }
      }
      status = WiFi.status();

      if (status == WL_CONNECTED) {
        wifiConnected = true;
        webServer.begin();  // HTTP streaming para web interface (porta 80)
        udp.begin(2324);    // Start UDP on port 2324
        mdns.begin(WiFi.localIP(), "lpt-uno");  // mDNS: lpt-uno.local
        _wifiServicesStarted = true;  // marcar para evitar begin() duplo no loop
        printWifiStatus();
      } else {
        Serial.println("WiFi: falha na ligacao inicial. Auto-reconecta em background.");
        // Iniciar contagem de reconexão a partir de agora (não de millis()=0)
        // para que a primeira retentativa ocorra em 10s e não em 10s+boot
        lastWifiCheck = millis();
      }
    } else {
      Serial.println("No WiFi config. Use: CMD:WIFI:SSID:PASS");
    }
  }
#endif
}

// ===========================
// LOOP PRINCIPAL
// ===========================
void loop() {
#if defined(ARDUINO_UNOR4_WIFI)
  {
    unsigned long _now = millis();

    // ── Throttle: tarefas WiFi no máximo 1× por 2ms ───────────────────
    // Sem este guard, o WiFi consome CPU em cada iteração do loop e
    // atrasa o caminho crítico de dados (buffer → Serial.write)
    static unsigned long _lastWifiMs = 0;
    if (_now - _lastWifiMs >= 2UL) {
      _lastWifiMs = _now;

      // ── Detectar queda de ligação ──────────────────────────────────────
      if (wifiConnected && WiFi.status() != WL_CONNECTED) {
        wifiConnected      = false;
        streamClientConnected = false;
        wifiAttempting     = false;
        WiFi.disconnect();
        lastWifiCheck      = _now;
      }

      // ── Iniciar tentativa de reconexão (cada 10s) ──────────────────────
      if (wifiConfig.valid == 0xAA && !wifiConnected && !wifiAttempting
          && (_now - lastWifiCheck > WIFI_RETRY_INTERVAL_MS)) {
        startWifiConnectAttempt();
      }

      // ── Verificar resultado da tentativa ──────────────────────────────
      if (wifiAttempting) {
        if (WiFi.status() == WL_CONNECTED) {
          wifiConnected  = true;
          wifiAttempting = false;
          // webServer e udp são iniciados apenas uma vez (sockets sobrevivem ao reconnect WiFi)
          if (!_wifiServicesStarted) {
            webServer.begin();
            udp.begin(2324);
            _wifiServicesStarted = true;
          }
          mdns.begin(WiFi.localIP(), "lpt-uno");  // re-announce mDNS no novo IP
          printWifiStatus();
        } else if (_now - wifiAttemptStart > WIFI_CONNECT_ATTEMPT_TIMEOUT_MS) {
          wifiAttempting = false;
          lastWifiCheck  = _now;
        }
      }

      // ── Serviços quando ligado ─────────────────────────────────────────
      if (wifiConnected) {
        // UDP Discovery Broadcast every 2 seconds
        if (_now - lastBroadcast > 2000) {
          lastBroadcast = _now;
          udp.beginPacket(IPAddress(255, 255, 255, 255), 2324);
          udp.write("LPT-UNO-R4");
          udp.endPacket();
        }
        
        // Verificar saúde do stream a cada iteração (cada 2ms)
        // e handleWebClient() recusa novas ligações para sempre
        if (streamClientConnected && !streamClient.connected()) {
          streamClientConnected = false;
          _streamBufLen = 0;
        }

        // Flush stream só quando vale a pena: buffer grande ou janela curta.
        if (_streamBufLen >= 768 || (_streamBufLen > 0 && (_now - lastStreamFlush > 8))) {
          flushStream();
        }

        // ── Keepalive TCP: envia 0x00 a cada 5s quando stream activo e sem dados ───
        // Evita que o router/WiFiS3 feche o socket TCP por idle e que o browser
        // fique preso num read() infinito sem receber FIN.
        // O browser filtra 0x00 (não aparece no output nem nos dados capturados).
        if (streamClientConnected && (_now - _lastKeepaliveMs > 5000UL)) {
          _lastKeepaliveMs = _now;
          // Envio directo (fora do buffer) para garantir entrega imediata
          if (streamClient.connected()) {
            streamClient.write((uint8_t)0x00);
            streamClient.flush();
          }
        }

        // mDNS e HTTP — não-bloqueantes
        mdns.run();
        handleWebClient();
      }
    } // fim throttle 2ms
  }
#endif

  // Check for Serial Commands (Config) - não-bloqueante (char a char)
  // Guardar com Serial: sem USB ligado, Serial.available() é sempre 0 mas
  // chamar em loop desnecessário gasta ciclos — a guarda é gratuita
  while (Serial && Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      // Linha completa recebida
      serialCmdBuffer.trim();
      if (serialCmdBuffer.length() > 0) {
        String input = serialCmdBuffer;
        serialCmdBuffer = "";  // limpar buffer
        // Legacy single char commands
        if (input.length() == 1) {
          switch (input[0]) {
            case 'R':
            case 'r':
              resetBuffer();
              Serial.println("Buffer reset");
              break;
            case 'S':
            case 's': printStats(); break;
            case 'V':
            case 'v': printVersion(); break;
            case 'I':
            case 'i': printIdentification(); break;
          }
        }
#if defined(ARDUINO_UNOR4_WIFI)
        // CMD:WIFI:SSID:PASS
        else if (input.startsWith("CMD:WIFI:")) {
          // Remover prefixo "CMD:WIFI:"
          String rest = input.substring(9);  // tudo depois de "CMD:WIFI:"
          int colonPos = rest.indexOf(':');
          if (colonPos > 0) {
            String newSSID = rest.substring(0, colonPos);
            String newPass = rest.substring(colonPos + 1);
            newSSID.trim();
            newPass.trim();
            Serial.print("OK:WIFI:SAVING:");
            Serial.println(newSSID);
            saveConfig(newSSID, newPass);
            Serial.println("OK:WIFI:SAVED");
            delay(200);
            Serial.println("OK:RESTARTING");
            delay(200);
            NVIC_SystemReset();
          } else {
            Serial.println("ERR:WIFI:FORMAT");
          }
        }
#endif
      } else {
        serialCmdBuffer = "";  // descartar linha vazia
      }
    } else {
      // Acumular caracteres no buffer (ignorar overflow)
      if (serialCmdBuffer.length() < 128) {
        serialCmdBuffer += c;
      }
    }
  }

  // Drenar buffer circular em batches maiores no R4 WiFi.
  {
    int _cnt = 0;
    while (bufferReadIndex != bufferWriteIndex && _cnt < DRAIN_BATCH_SIZE) {
      byte b = dataBuffer[bufferReadIndex];
      bufferReadIndex = (bufferReadIndex + 1) % BUFFER_SIZE;
      if (Serial) Serial.write(b);
#if defined(ARDUINO_UNOR4_WIFI)
      if (wifiConnected && streamClientConnected) sendStreamData(b);
#endif
      _cnt++;
    }
  }

  // Libertar BUSY apenas quando o buffer já saiu da zona de risco.
  // Isto introduz backpressure real no protocolo Centronics e evita bursts
  // acima da capacidade instantânea do caminho WiFi/browser.
  if (flowControlBusy) {
    noInterrupts();
    int bufferUsed = (bufferWriteIndex - bufferReadIndex + BUFFER_SIZE) % BUFFER_SIZE;
    if (bufferUsed <= BUFFER_BUSY_LOW_WATER) {
      flowControlBusy = false;
      interrupts();
      digitalWrite(PIN_BUSY, LOW);
    } else {
      interrupts();
    }
  }
      // Flush stream feito no bloco WiFi (2ms) — não aqui

}
// fim loop()

// ===========================
// INTERRUPÇÃO - STROBE
// ===========================
void handleStrobe() {
  // Incrementar contador (debug)
  strobeCounter++;

  // Debounce simples
  unsigned long currentTime = micros();
  if (currentTime - lastStrobeTime < DEBOUNCE_MICROS) {
    return;
  }
  lastStrobeTime = currentTime;

  // Sinalizar que estamos ocupados
  digitalWrite(PIN_BUSY, HIGH);

  // Ler os 8 bits de dados dos pinos — acesso directo ao registo (máxima velocidade)
  byte dataByte = 0;
#if defined(__AVR_ATmega328P__)
  // ATmega328P @ 16MHz: D3-D7 = PIND[3:7], D8-D10 = PINB[0:2]  (~125ns, 2 instruções)
  dataByte = ((PIND >> 3) & 0x1F) | ((PINB & 0x07) << 5);
#elif defined(ARDUINO_UNOR4_WIFI)
  // RA4M1 @ 48MHz: acesso directo aos registos PIDR — ~3 ciclos vs ~8µs com digitalRead
  // Mapeamento CORRECTO (Arduino UNO R4 WiFi schematic):
  //   D3=P105 → PORT1 bit 5 → LPT D0
  //   D4=P106 → PORT1 bit 6 → LPT D1
  //   D5=P107 → PORT1 bit 7 → LPT D2
  //   D6=P111 → PORT1 bit11 → LPT D3
  //   D7=P112 → PORT1 bit12 → LPT D4
  //   D8=P304 → PORT3 bit 4 → LPT D5
  //   D9=P303 → PORT3 bit 3 → LPT D6
  //  D10=P103 → PORT1 bit 3 → LPT D7
  // NOTA: D2=P104=PORT1 bit 4 é o STROBE — NÃO incluído aqui
  {
    uint32_t p1 = R_PORT1->PIDR;
    uint32_t p3 = R_PORT3->PIDR;
    dataByte = (uint8_t)(
      ((p1 >> 5) & 0x07)         |  // bits 5,6,7  (P105,P106,P107) → D0,D1,D2
      (((p1 >> 11) & 0x03) << 3) |  // bits 11,12  (P111,P112)      → D3,D4
      (((p3 >> 4) & 0x01) << 5)  |  // bit 4       (P304)           → D5
      (((p3 >> 3) & 0x01) << 6)  |  // bit 3       (P303)           → D6
      (((p1 >> 3) & 0x01) << 7)     // bit 3       (P103)           → D7
    );
  }
#else
  // Outros boards: digitalRead genérico
  for (int i = 0; i < DATA_PIN_COUNT; i++) {
    if (digitalRead(DATA_PINS[i]) == HIGH) {
      dataByte |= (1 << i);
    }
  }
#endif

  // Armazenar no buffer circular sem sobrescrever dados ainda não lidos.
  // O comportamento anterior fazia overwrite silencioso e causava saltos de
  // linhas inteiras quando o loop WiFi não drenava a tempo.
  int nextWriteIndex = (bufferWriteIndex + 1) % BUFFER_SIZE;
  if (nextWriteIndex != bufferReadIndex) {
    dataBuffer[bufferWriteIndex] = dataByte;
    bufferWriteIndex = nextWriteIndex;
    int bufferUsedAfterWrite = (bufferWriteIndex - bufferReadIndex + BUFFER_SIZE) % BUFFER_SIZE;
    if (bufferUsedAfterWrite >= BUFFER_BUSY_HIGH_WATER) {
      flowControlBusy = true;
    }
  } else {
    bufferOverflowCount++;
    flowControlBusy = true;
  }

  // Pequeno delay de processamento
  delayMicroseconds(BUSY_DELAY_MICROS);

  // Enviar pulso ACK (active low - pulso para baixo)
  digitalWrite(PIN_ACK, LOW);
  delayMicroseconds(ACK_PULSE_MICROS);
  digitalWrite(PIN_ACK, HIGH);

  // Liberar BUSY apenas se não estivermos a aplicar backpressure por buffer alto.
  if (!flowControlBusy) {
    digitalWrite(PIN_BUSY, LOW);
  }
}

// ===========================
// FUNÇÕES AUXILIARES
// ===========================

// Função para resetar o buffer (pode ser chamada por comando serial)
void resetBuffer() {
  noInterrupts();
  bufferWriteIndex = 0;
  bufferReadIndex = 0;
  bufferOverflowCount = 0;
  flowControlBusy = false;
  strobeCounter = 0;  // Resetar contador também
  interrupts();
  digitalWrite(PIN_BUSY, LOW);
}

// Função para obter estatísticas (chamada por comando serial)
void printStats() {
  int bufferUsed = (bufferWriteIndex - bufferReadIndex + BUFFER_SIZE) % BUFFER_SIZE;
  Serial.print("Buffer usage: ");
  Serial.print(bufferUsed);
  Serial.print("/");
  Serial.println(BUFFER_SIZE);
  Serial.print("STROBE interrupts: ");
  Serial.println(strobeCounter);
  Serial.print("Buffer overflows: ");
  Serial.println(bufferOverflowCount);
  Serial.print("Flow control BUSY: ");
  Serial.println(flowControlBusy ? "ON" : "OFF");
}

// Função para exibir informações de versão
void printVersion() {
  Serial.println("===================================");
  Serial.println("LPT-UNO Emulator");
  Serial.print("Firmware: v");
  Serial.println(FIRMWARE_VERSION);
  Serial.print("Build: ");
  Serial.print(BUILD_DATE);
  Serial.print(" ");
  Serial.println(BUILD_TIME);
  Serial.println("===================================");
}

#if defined(ARDUINO_UNOR4_WIFI)
void printWifiStatus() {
  Serial.print("SSID: ");
  Serial.println(WiFi.SSID());
  IPAddress ip = WiFi.localIP();
  Serial.print("IP: ");
  Serial.println(ip);
  Serial.print("Web Interface Stream: http://");
  Serial.print(ip);
  Serial.println("/stream");
}
#endif

// Função para enviar identificação única
void printIdentification() {
  Serial.println("DEVICE:LPT-UNO");
  Serial.println("TYPE:PARALLEL_EMULATOR");
  Serial.print("VERSION:");
  Serial.println(FIRMWARE_VERSION);
}

// ===========================
// COMANDOS SERIAL (LEGACY - MANTIDO EM LOOP AGORA)
// ===========================
// A função serialEvent é substituída pela verificação no loop para suportar Strings.
/*
void serialEvent() {
  while (Serial.available()) {
    char inChar = (char)Serial.read();
    switch (inChar) {
      case 'R':
      case 'r':
        resetBuffer();
        Serial.println("Buffer reset");
        break;
      case 'S':
      case 's':
        printStats();
        break;
      case 'V':
      case 'v':
        printVersion();
        break;
      case 'I':
      case 'i':
        printIdentification();
        break;
      case '?':
        Serial.println("Commands: R=Reset, S=Stats, V=Version, I=Identify, ?=Help");
        Serial.println("Stats (S) shows: buffer usage + STROBE interrupt count");
        break;
    }
  }
}
*/
