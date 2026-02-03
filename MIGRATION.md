# Migração: Web Interface → Aplicação Nativa (Windows)

Este documento descreve a proposta de migração da interface web para uma aplicação nativa Windows (`.exe`).

## Motivação
- Melhor integração com o Windows (tray, autostart, serviço de impressão) e menor dependência de browsers.
- Possibilidade de operar sem scripts `.bat` externos.

## Passos recomendados
1. Prototipar uma aplicação WPF com funcionalidades principais (Tray, AutoPrint, Config). ✅
2. Verificar comportamento de impressão e comunicação serial (substituir usos de scripts `.bat`).
3. Criar instalador (Inno Setup / MSIX) e instruções para autostart.
4. Testes e rollout em ambiente controlado; manter a web interface como fallback enquanto não houver aprovação para remoção.

**Atenção:** Não remover a Web Interface do repositório sem autorização explícita do desenvolvedor principal; inicialmente apenas marcar como deprecated e documentar a migração.
