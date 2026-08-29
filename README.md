# CS2 AI Translator

Real-time Windows desktop translator for CS2 voice communications.

## Safety / integration approach

The app is intentionally external to CS2. It does **not** inject DLLs, read or modify CS2 process memory, automate gameplay, or attempt to bypass VAC / Trusted Mode. MVP capture uses normal Windows WASAPI loopback audio.

## Current MVP

- WPF desktop app
- Windows WASAPI loopback capture
- 3-second audio chunking
- local Whisper speech-to-text (`Whisper.net`)
- automatic speech-language detection
- pluggable translation interface
- CS2 terminology normalization
- always-on-top translation overlay
- STT and end-to-end latency display

> Translation backend is currently a safe passthrough/CS2-normalization implementation. The next milestone plugs in a real selectable translator (local or API) without changing the UI/audio pipeline.

## Requirements

- Windows 11
- Visual Studio 2022 or newer with .NET desktop development workload
- .NET 10 SDK
- Microsoft Visual C++ Redistributable x64

## Run

1. Clone the repository.
2. Open `CS2AITranslator.sln`.
3. Create a `models` folder in the repository root.
4. Put a compatible Whisper ggml model there, e.g. `models/ggml-base.bin`.
5. Restore NuGet packages and build the solution.
6. Start `CS2AITranslator.App`.
7. Start CS2, press **Start translator**, and make sure CS2 voice is routed to the Windows playback device being captured.

## Architecture

- `CS2AITranslator.Core` — contracts, models, CS2 glossary
- `CS2AITranslator.Infrastructure` — WASAPI capture, Whisper STT, translation implementations
- `CS2AITranslator.App` — WPF settings window and overlay

## Roadmap

### MVP 1.1
- Silero/WebRTC-style VAD instead of fixed 3-second chunks
- real translation backend and language selector
- audio device selector
- process-specific CS2 audio capture where supported
- Whisper model manager/download UI
- settings persistence

### MVP 2
- microphone speech translation
- TTS
- virtual microphone output for translated push-to-talk

### MVP 3
- external screen capture + OCR for text chat
- translated chat overlay

No game-process injection is planned.
