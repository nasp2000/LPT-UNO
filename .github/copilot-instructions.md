# Instruções para GitHub Copilot - Projeto LPT-UNO

## 📋 Regras Gerais do Projeto

### 1. Versionamento e Build Date

**IMPORTANTE**: Sempre que modificar o código do projeto, você DEVE atualizar as informações de versão:

#### No Código Arduino (`LPT_Emulator.ino`):
```cpp
#define FIRMWARE_VERSION "X.Y"  // Atualizar conforme mudanças
#define BUILD_DATE __DATE__     // Automaticamente atualizado na compilação
#define BUILD_TIME __TIME__     // Automaticamente atualizado na compilação
```

**Regras de versionamento:**
- **Major (X.0)**: Mudanças estruturais, quebra de compatibilidade, novos recursos principais
- **Minor (X.Y)**: Novos recursos, melhorias, correções significativas
- **Patch**: Pequenas correções de bugs (opcional, usar X.Y.Z se necessário)

**Quando atualizar:**
- ✅ Adição de novos recursos
- ✅ Correção de bugs importantes
- ✅ Mudanças no protocolo de comunicação
- ✅ Otimizações significativas
- ✅ Mudanças na pinagem ou configuração de hardware

#### No Interface Web (`web_interface.html`):
```html
<p id="firmwareInfo" style="...">Web Interface v1.0 | Build: YYYY-MM-DD</p>
```

**Atualizar a data no formato ISO (YYYY-MM-DD)** sempre que modificar o HTML.

### 2. Histórico de Versões

Manter registro no arquivo `CHANGELOG.md` (criar se não existir):

```markdown
## [X.Y] - YYYY-MM-DD
### Added
- Nova funcionalidade X
### Changed
- Modificação Y
### Fixed
- Correção do bug Z
```

### 3. Encoding e Caracteres Especiais

**SEMPRE** usar **UTF-8** para garantir suporte a caracteres portugueses:
- ✅ `ç, á, é, í, ó, ú, ã, õ, â, ê, ô`
- ✅ Pontuação: `«», …, –, —`

Verificar encoding em:
- Arduino Serial: `TextDecoderStream('utf-8')`
- Arquivos salvos: `type: 'text/plain; charset=utf-8'`
- HTML meta: `<meta charset="UTF-8">`

### 4. Estrutura de Código

#### Arduino (.ino):
```cpp
// 1. Cabeçalho com versão
// 2. Definições de versão (#define)
// 3. Definições de pinos
// 4. Configurações de timing
// 5. Buffer e variáveis
// 6. setup()
// 7. loop()
// 8. Interrupções (ISR)
// 9. Funções auxiliares
// 10. Comandos serial
```

#### HTML:
```html
<!-- 1. Head com meta UTF-8 -->
<!-- 2. Estilos CSS completos -->
<!-- 3. Header com versão visível -->
<!-- 4. Controles (2 barras) -->
<!-- 5. Área de output -->
<!-- 6. Footer com nome LPT-UNO -->
<!-- 7. Scripts JavaScript -->
```

### 5. Comentários e Documentação

- **Português**: Usar português em comentários de código e mensagens ao usuário
- **Inglês**: Nomes de variáveis e funções em inglês (padrão de programação)
- **Documentar**: Toda função complexa deve ter comentário explicativo

### 6. Comandos Serial Disponíveis

| Comando | Ação | Resposta |
|---------|------|----------|
| `R` ou `r` | Reset do buffer | "Buffer reset" |
| `S` ou `s` | Estatísticas | "Buffer usage: X/256" |
| `V` ou `v` | Informações de versão | Versão e build date |
| `?` | Ajuda | Lista de comandos |

**Ao adicionar novos comandos:**
1. Documentar na tabela acima
2. Adicionar no `switch` do `serialEvent()`
3. Atualizar mensagem de ajuda (`case '?'`)
4. Atualizar README.md

### 7. Convenções de Nomenclatura

- **Pinos**: `PIN_NOME` (ex: `PIN_STROBE`)
- **Constantes**: `NOME_CONSTANTE` (ex: `BUFFER_SIZE`)
- **Funções**: `nomeDescritivo()` (ex: `handleStrobe()`)
- **Variáveis globais**: `nomeVariavel` (ex: `bufferWriteIndex`)
- **Variáveis voláteis**: `volatile` quando usadas em ISR

### 8. Testes Antes de Commit

Verificar sempre:
- [ ] Código compila sem erros
- [ ] Interface HTML abre sem erros de console
- [ ] Caracteres portugueses funcionam (testar: São Paulo, ação, çedilha)
- [ ] Auto-save funciona após 10s de inatividade
- [ ] Auto-print envia para impressora
- [ ] Versão e build date exibidos corretamente
- [ ] Todos os botões funcionam

### 9. Pinagem - NÃO MODIFICAR sem Avisar

```
DB25 Pin  → Arduino Pin → Função
1         → 10          → STROBE (INT)
2-9       → 2-9         → D0-D7 (Dados)
10        → 11          → ACK
11        → 12          → BUSY
13        → 13          → SELECT
18-25     → GND         → Terra
```

### 10. Performance e Otimização

- **Buffer**: 256 bytes (ajustar se necessário para aplicações específicas)
- **Baud Rate**: 115200 (máximo do Arduino Uno)
- **Timing ACK**: ~5µs (padrão IEEE 1284)
- **Timing BUSY**: ~10µs (processamento)
- **Inatividade**: 10s (configurável se solicitado)

### 11. Compatibilidade

- **Arduino**: Uno R3 (ATmega328P)
- **Navegadores**: Chrome, Edge, Opera (Web Serial API)
- **OS**: Windows, Linux, macOS
- **Impressoras**: Qualquer impressora padrão instalada no sistema

### 12. Segurança

- ⚠️ **Níveis lógicos**: 5V TTL (Arduino Uno)
- ⚠️ **Não usar** Arduino 3.3V sem conversor de nível
- ⚠️ **Cabos curtos**: < 2 metros para evitar ruído
- ⚠️ **GND comum**: Todos os pinos GND (18-25) conectados

---

## 🤖 Prompt Rápido para Copilot

Quando trabalhar neste projeto, lembre-se:
```
Projeto: LPT-UNO - Emulador Impressora Paralela
Versão atual: 1.0
Encoding: UTF-8 (caracteres portugueses)
Atualizar: versão + build date em TODA modificação
Idioma: Português (comentários e UI) | Inglês (código)
```

---

**Última atualização**: 2026-01-25
**Versão deste documento**: 1.0
