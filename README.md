# CS2 AI Translator

CS2-first real-time Windows translator for team voice communications.

## Product goal

Build a low-latency communications layer for Counter-Strike 2 rather than another generic desktop translator:

- teammate voice -> translated subtitles over the game;
- own microphone -> translated voice through a virtual microphone (next phase);
- CS2-specific terminology and map callouts;
- local-first speech recognition;
- no DLL injection, memory reading, packet interception or anti-cheat bypass.

See [`docs/COMPETITIVE_ANALYSIS.md`](docs/COMPETITIVE_ANALYSIS.md) for the 2026 competitor review and product direction.

## Current branch / MVP foundation

- .NET 10 + WPF
- NAudio 3 process-specific WASAPI capture for `cs2.exe` on Windows 10 build 19041+
- automatic fallback to normal Windows loopback capture
- 16 kHz / 16-bit / mono process capture
- short ~1.2 s CS2 chunks
- local Whisper.net speech-to-text
- Whisper model stays loaded in memory between chunks
- automatic Whisper model download on first start
- model choices: Tiny / Base / Small
- pluggable translation interface
- DeepL translation provider via `DEEPL_API_KEY`
- transcription/CS2-normalization fallback when no translation API is configured
- CS2 terminology normalization
- always-on-top overlay
- latency display for STT / translation / total pipeline
- low-latency backlog protection: stale chunks are dropped instead of queued
- Windows GitHub Actions build workflow

## Requirements

- Windows 10 version 2004 (build 19041) or newer; Windows 11 recommended
- Visual Studio 2022 or newer with **.NET desktop development** workload
- .NET 10 SDK
- Microsoft Visual C++ Redistributable x64

## Run

1. Clone the repository.
2. Open `CS2AITranslator.sln`.
3. Restore NuGet packages and build the solution.
4. Start CS2 if you want **CS2 only** capture.
5. Start `CS2AITranslator.App`.
6. Choose target language and Whisper model.
7. Press **Start translator**.
8. The selected Whisper model is downloaded automatically to `%LOCALAPPDATA%\CS2AITranslator\models` on first use.

If process-specific capture cannot be activated, the app automatically falls back to Windows output loopback.

## Real translation

Without an API key the application remains useful as a local transcription/CS2-normalization test mode.

For DeepL, set an environment variable before starting Visual Studio or the application:

```powershell
$env:DEEPL_API_KEY="your-key"
```

Then launch the application from that shell, or set the variable persistently in Windows and restart Visual Studio.

No API key is stored in the repository.

## Architecture

- `CS2AITranslator.Core` — contracts, result models and CS2 glossary
- `CS2AITranslator.Infrastructure` — WASAPI capture, CS2 process capture, model management, Whisper STT and translation providers
- `CS2AITranslator.App` — WPF control window and overlay

Pipeline:

```text
cs2.exe audio
    -> per-process WASAPI loopback
    -> 16 kHz mono speech segments
    -> local Whisper STT
    -> translation provider + CS2 terminology normalization
    -> click-through/always-on-top subtitle overlay
```

## Safety boundary

The project is intentionally external to CS2. It does **not**:

- inject DLLs;
- read or modify CS2 memory;
- intercept game network packets;
- automate aiming/movement/gameplay;
- attempt to bypass VAC or Trusted Mode.

## Roadmap

### MVP 1.1 — production-quality incoming comms

- Silero VAD integration and adaptive speech segmentation
- audio device selector + live level meter/test
- first-run setup wizard
- OpenAI-compatible/local NMT translation provider
- translation-provider health test
- persistent settings
- multi-line overlay history, fade and positioning
- richer CS2 terminology profiles by map/language
- structured latency telemetry and diagnostics export

### MVP 2 — two-way voice

- microphone capture
- push-to-translate hotkey
- low-latency TTS
- VB-CABLE / virtual microphone routing
- separate local-monitor and outgoing translated channels
- echo/feedback prevention

### MVP 3 — text chat

- external screen capture / OCR for chat
- translated chat overlay
- optional console-log based integration where supported and safe

### Later

- automatic active-map context
- per-language slang/callout aliases
- confidence-aware partial subtitles
- optional local translation model for fully offline mode
- packaged installer and auto-update

No game-process injection is planned.
