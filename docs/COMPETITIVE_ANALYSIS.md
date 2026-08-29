# Competitive analysis — August 2026

The project should not be positioned as a generic desktop translator. That category already exists. The opportunity is a **CS2-first communications layer** optimized for short competitive callouts, low latency and setup simplicity.

## Products/projects reviewed

| Product | Incoming game voice | Overlay | Outgoing translated mic | Local STT | Game terminology | Text/chat |
| --- | --- | --- | --- | --- | --- | --- |
| Unitra AI | Yes | Yes | Product-dependent | Yes | Yes, including CS2 | Yes |
| SyncLingo | Yes | Yes | Yes | Whisper option | Generic gaming | Limited |
| Voxis Live | Yes | Primarily audio translation | Yes | VAD local; translation cloud | Generic | Transcript/history |
| Babelarc | Yes | Yes | Yes | Mixed | Gaming focused | Yes/OCR |
| VoxGo | Yes | Yes | No/limited | faster-whisper | Gaming | Limited |
| EchoBridge | Yes | Planned/yes | Future | Whisper | Generic gaming | No |
| CS2 Echo | No voice focus | Yes | Chat response | N/A | CS2 | Console chat translation |

## What this means for us

A simple `WASAPI -> Whisper -> translation -> overlay` application is not differentiated enough.

### Product direction

**CS2 AI Translator = CS2-first, local-first, sub-second-target communications assistant.**

Priorities:

1. Capture only `cs2.exe` whenever Windows supports process loopback.
2. Optimize for 0.3–1.0 s perceived subtitle latency rather than sentence-quality meeting translation.
3. Treat CS2 callouts as structured vocabulary, not ordinary prose.
4. Keep STT and VAD local by default.
5. Support two-way operation: teammate voice -> subtitles and own mic -> translated virtual microphone.
6. Add external text-chat translation without memory reading or DLL injection.
7. Make first-run setup automatic: model download, audio test, CS2 detection, overlay test, translation-provider test.
8. Measure every pipeline stage so regressions are visible.

## Features that can differentiate the project

### CS2 context engine

The translator should understand map-specific terms such as `Banana`, `Pit`, `Apps`, `Connector`, `Heaven`, `Hell`, `Ramp`, `Short`, `Long`, utility names, economy calls and weapon names. Future versions should maintain a profile per map and use aliases in Russian, Ukrainian, Polish, Czech/Slovak and English.

### Callout mode

Incoming speech in CS2 is usually not a grammatical sentence. A competitive preset should prefer:

- short segments;
- no explanatory text;
- canonical CS2 terminology;
- preservation of player counts and damage numbers;
- immediate partial output when confidence is sufficient.

### Graceful degradation

Order of preference:

1. `cs2.exe` process-specific loopback;
2. selected Windows render device loopback;
3. system default loopback.

Translation:

1. configured low-latency provider/local NMT;
2. alternative provider;
3. transcription-only mode rather than failing the entire app.

### Privacy boundary

No process injection, memory reading, network-packet interception, gameplay automation or anti-cheat bypass. Game integration is passive Windows audio/screen processing only.

## Near-term engineering milestones

- Process-specific CS2 capture.
- Model manager and first-run setup.
- VAD/speech segmentation.
- DeepL and OpenAI-compatible translation providers.
- Latency telemetry (capture/STT/translate/render).
- CS2 terminology profiles.
- Audio device diagnostics.
- Overlay history with fade and click-through mode.
- Microphone -> translation -> TTS -> VB-CABLE output.
- External OCR/chat pipeline.

## References reviewed

- Unitra AI / dedicated CS2 translator page
- SyncLingo
- VoxisLive/voxislive
- Babelarc
- zxbb1190/VoxGo_game_voice_trans
- yohandl17/echobridge
- LDzik/CS2-Echo
- MeckeDev/cs2-chat-translator

This document is product research, not a license to copy competitors' code. Components must be implemented independently and licenses must be reviewed before incorporating any third-party source.
