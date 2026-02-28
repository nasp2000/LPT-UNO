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
  
  // Credentials storage
  struct Config {
    char ssid[33];
    char pass[64];
    byte valid; // 0xAA indicates valid config
  } wifiConfig;

  int status = WL_IDLE_STATUS;
  WiFiServer server(2323);             // Porta TCP para conexão (Padrão: 2323)
  WiFiClient wifiClient;
  bool wifiConnected = false;

  // HTTP server for web interface SSE (port 80)
  WiFiServer webServer(80);
  WiFiClient sseClient;
  bool sseClientConnected = false;
  
  // Discovery
  WiFiUDP udp;
  unsigned long lastBroadcast = 0;
  
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

  // Envia byte via SSE para a web interface
  void sendSSEData(byte b) {
    if (!sseClientConnected || !sseClient.connected()) {
      sseClientConnected = false;
      return;
    }
    sseClient.print("data:");
    if (b >= 0x20 && b < 0x7F) {
      sseClient.write(b);
    } else {
      sseClient.print("\\x");
      if (b < 0x10) sseClient.print("0");
      sseClient.print(b, HEX);
    }
    sseClient.print("\n\n");
  }

  // Trata ligações HTTP na porta 80 (Server-Sent Events)
  void handleWebClient() {
    WiFiClient client = webServer.available();
    if (!client) return;
    String req = "";
    unsigned long t = millis();
    while (client.connected() && (millis() - t) < 500) {
      if (client.available()) {
        req += (char)client.read();
        if (req.endsWith("\r\n\r\n")) break;
      }
    }
    if (req.indexOf("GET /events") >= 0) {
      client.println("HTTP/1.1 200 OK");
      client.println("Content-Type: text/event-stream");
      client.println("Cache-Control: no-cache");
      client.println("Access-Control-Allow-Origin: *");
      client.println("Connection: keep-alive");
      client.println();
      client.print(": connected\n\n");
      if (sseClient && sseClient.connected()) sseClient.stop();
      sseClient = client;
      sseClientConnected = true;
      Serial.println("[WiFi] Web interface SSE conectada");
    } else if (req.indexOf("GET /status") >= 0 || req.indexOf("GET /ip") >= 0) {
      IPAddress ip = WiFi.localIP();
      String ipStr = String(ip[0])+"."+String(ip[1])+"."+String(ip[2])+"."+String(ip[3]);
      String json = "{\"version\":\"" + String(FIRMWARE_VERSION) + "\",\"ip\":\"" + ipStr + "\"}";
      client.println("HTTP/1.1 200 OK");
      client.println("Content-Type: application/json");
      client.println("Access-Control-Allow-Origin: *");
      client.println("Connection: close");
      client.println();
      client.print(json);
      client.stop();
    } else {
      client.println("HTTP/1.1 200 OK");
      client.println("Content-Type: text/plain");
      client.println("Access-Control-Allow-Origin: *");
      client.println("Connection: close");
      client.println();
      IPAddress ip = WiFi.localIP();
      client.print("LPT-UNO v"); client.println(FIRMWARE_VERSION);
      client.print("SSE: http://"); client.print(ip); client.println("/events");
      client.stop();
    }
  }
#endif

// ===========================
// DEFINIÇÃO DE PINOS
// ===========================

// Pinos de controle (Input do PC)
const int PIN_STROBE = 2;   // Sinal de dados prontos (active low) - INT0

// Pinos de dados (D0-D7)
const int DATA_PINS[] = {3, 4, 5, 6, 7, 8, 9, 10};
const int DATA_PIN_COUNT = 8;

// Pinos de status (Output para o PC)
const int PIN_ACK = 11;     // Acknowledge (active low pulse)
const int PIN_BUSY = 12;    // Impressora ocupada (active high)
const int PIN_SELECT = 13;  // Impressora selecionada (active high)

// ===========================
// CONFIGURAÇÃO DE TIMING
// ===========================
const unsigned long ACK_PULSE_MICROS = 5;      // Duração do pulso ACK (5µs)
const unsigned long BUSY_DELAY_MICROS = 10;    // Tempo de processamento (10µs)

// ===========================
// BUFFER DE DADOS
// ===========================
const int BUFFER_SIZE = 256;
volatile byte dataBuffer[BUFFER_SIZE];
volatile int bufferWriteIndex = 0;
volatile int bufferReadIndex = 0;
volatile bool dataAvailable = false;

// ===========================
// VARIÁVEIS DE ESTADO
// ===========================
volatile unsigned long lastStrobeTime = 0;
const unsigned long DEBOUNCE_MICROS = 2;  // Debounce de 2µs
volatile unsigned long strobeCounter = 0;  // Contador de interrupções (debug)

// ===========================
// SETUP
// ===========================
void setup() {
  // Inicializar comunicação serial USB (velocidade alta)
  Serial.begin(115200);
  while (!Serial) {
    ; // Aguardar porta serial conectar
  }
  
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
  digitalWrite(PIN_ACK, HIGH);      // ACK em repouso (high)
  digitalWrite(PIN_BUSY, LOW);      // Não ocupado
  digitalWrite(PIN_SELECT, HIGH);   // Selecionado e pronto
  
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
        Serial.print("Attempting to connect to SSID: ");
        Serial.println(wifiConfig.ssid);
        
        // Tentar conectar (não bloqueante para não impedir operação USB se falhar)
        // Mas para setup inicial, vamos tentar algumas vezes
        int attempts = 0;
        status = WiFi.begin(wifiConfig.ssid, wifiConfig.pass);
        
        // Wait 10 seconds for connection:
        delay(10000);
        status = WiFi.status();
        
        if (status == WL_CONNECTED) {
          wifiConnected = true;
          server.begin();
          webServer.begin();  // HTTP/SSE para web interface (porta 80)
          udp.begin(2324); // Start UDP on port 2324
          printWifiStatus();
        } else {
          Serial.println("\nWiFi connection failed. Check credentials via Serial Command.");
        }
      } else {
        Serial.println("No WiFi config found. Configure via Serial: CMD:WIFI:SSID:PASS");
      }
    }
  #endif
}

// ===========================
// LOOP PRINCIPAL
// ===========================
void loop() {
  #if defined(ARDUINO_UNOR4_WIFI)
    // Gerenciar conexão WiFi
    if (wifiConnected) {
      WiFiClient newClient = server.available();
      if (newClient) {
        if (!wifiClient || !wifiClient.connected()) {
          wifiClient = newClient;
          Serial.println("New WiFi client connected");
          wifiClient.println("LPT-UNO Connected");
        } else {
           newClient.stop();
        }
      }
      
      // UDP Discovery Broadcast every 5 seconds
      if (millis() - lastBroadcast > 5000) {
        lastBroadcast = millis();
        // Broadcast IP to port 2324
        udp.beginPacket(IPAddress(255, 255, 255, 255), 2324);
        udp.write("LPT-UNO-R4");
        udp.endPacket();
      }
      // Tratar ligações HTTP/SSE (web interface)
      handleWebClient();
    }
  #endif
  
  // Check for Serial Commands (Config)
  if (Serial.available()) {
    String input = Serial.readStringUntil('\n');
    input.trim();
    if (input.length() > 0) {
       // Legacy single char commands
       if (input.length() == 1) {
          switch(input[0]) {
             case 'R': case 'r': resetBuffer(); Serial.println("Buffer reset"); break;
             case 'S': case 's': printStats(); break;
             case 'V': case 'v': printVersion(); break;
             case 'I': case 'i': printIdentification(); break;
          }
       }
       #if defined(ARDUINO_UNOR4_WIFI)
       // CMD:WIFI:SSID:PASS
       else if (input.startsWith("CMD:WIFI:")) {
          int firstColon = input.indexOf(':');
          int secondColon = input.indexOf(':', firstColon + 1);
          int thirdColon = input.indexOf(':', secondColon + 1);
          
          if (secondColon > 0 && thirdColon > 0) {
            String newSSID = input.substring(secondColon + 1, thirdColon);
            String newPass = input.substring(thirdColon + 1);
            Serial.print("Saving WiFi config: ");
            Serial.println(newSSID);
            saveConfig(newSSID, newPass);
            Serial.println("Config Saved. Restarting...");
            delay(100);
            NVIC_SystemReset();
          } else {
             Serial.println("Error: Invalid Format. Use CMD:WIFI:SSID:PASS");
          }
       }
       #endif
    }
  }

  // Verificar se há dados no buffer para enviar via USB
  if (bufferReadIndex != bufferWriteIndex) {
    // Enviar dados via Serial
    Serial.write(dataBuffer[bufferReadIndex]);

    #if defined(ARDUINO_UNOR4_WIFI)
      // Enviar também via WiFi se conectado
      if (wifiConnected && wifiClient && wifiClient.connected()) {
        wifiClient.write(dataBuffer[bufferReadIndex]);
      }
      // Enviar também via SSE (web interface WiFi)
      if (wifiConnected && sseClientConnected) {
        sendSSEData(dataBuffer[bufferReadIndex]);
      }
    #endif
    
    // Avançar índice de leitura (circular)
    bufferReadIndex = (bufferReadIndex + 1) % BUFFER_SIZE;
  }
  
  // Pequeno delay para não sobrecarregar o loop
  // (a maior parte do trabalho é feita por interrupção)
  delayMicroseconds(10);
}

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
  
  // Ler os 8 bits de dados dos pinos
  byte dataByte = 0;
  for (int i = 0; i < DATA_PIN_COUNT; i++) {
    if (digitalRead(DATA_PINS[i]) == HIGH) {
      dataByte |= (1 << i);
    }
  }
  
  // Armazenar no buffer circular
  dataBuffer[bufferWriteIndex] = dataByte;
  bufferWriteIndex = (bufferWriteIndex + 1) % BUFFER_SIZE;
  
  // Pequeno delay de processamento
  delayMicroseconds(BUSY_DELAY_MICROS);
  
  // Enviar pulso ACK (active low - pulso para baixo)
  digitalWrite(PIN_ACK, LOW);
  delayMicroseconds(ACK_PULSE_MICROS);
  digitalWrite(PIN_ACK, HIGH);
  
  // Liberar sinal BUSY
  digitalWrite(PIN_BUSY, LOW);
}

// ===========================
// FUNÇÕES AUXILIARES
// ===========================

// Função para resetar o buffer (pode ser chamada por comando serial)
void resetBuffer() {
  noInterrupts();
  bufferWriteIndex = 0;
  bufferReadIndex = 0;
  strobeCounter = 0;  // Resetar contador também
  interrupts();
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
  Serial.println("TCP 2323: app desktop");
  Serial.print("Web Interface SSE: http://");
  Serial.print(ip);
  Serial.println("/events");
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
