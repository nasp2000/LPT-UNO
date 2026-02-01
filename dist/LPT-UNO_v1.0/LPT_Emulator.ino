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
#define FIRMWARE_VERSION "1.0"
#define BUILD_DATE __DATE__
#define BUILD_TIME __TIME__

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
}

// ===========================
// LOOP PRINCIPAL
// ===========================
void loop() {
  // Verificar se há dados no buffer para enviar via USB
  if (bufferReadIndex != bufferWriteIndex) {
    // Enviar dados via Serial
    Serial.write(dataBuffer[bufferReadIndex]);
    
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

// Função para enviar identificação única
void printIdentification() {
  Serial.println("DEVICE:LPT-UNO");
  Serial.println("TYPE:PARALLEL_EMULATOR");
  Serial.print("VERSION:");
  Serial.println(FIRMWARE_VERSION);
}

// ===========================
// COMANDOS SERIAL (OPCIONAL)
// ===========================
// Você pode adicionar comandos via Serial para controlar o emulador
// Exemplo: enviar 'R' para reset, 'S' para stats, etc.
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