# Lot 13.3: audio analysis, disk storage, startup guard, ffmpeg in the image, runtime evidence

- Date: 2026-11-18
- Branch: `feature/lot_13`, base commit `6ff374d` (13.2 evidence)
- Design source: decisions A1 to A8 of the 13.3 brief (13.A, 13.C, 13.D, 13.E, 13.F), inventory `docs/review/lot13/13.0/inventory.md`, 13.2 evidence.
- Scope: audio services, startup guard, ffmpeg in the image. No route, no controller, no hub change, no migration, no change to ConversationService (13.4).
- The processor (`FfmpegVoiceAudioProcessor`) and the storage (`DiskVoiceMessageStorage`) are registered but not called by anything yet. The processor itself is proven end to end in 13.4, through the routes, in the image `mentora-api:lot13-3-test`, with the audio test files of P6.

## 1. Decisions as implemented

| Decision | Implementation |
|---|---|
| A1 `AudioOptions` (section `Audio`, env `Audio__*`) | `Mentora.Core/Options/AudioOptions.cs`: defaults `FfmpegPath="ffmpeg"`, `FfprobePath="ffprobe"`, `StoragePath=""`, `MaxConcurrentDecodes=2`, `DecodeTimeoutSeconds=10`; `Validate(bool isDevelopment)` `:42` (ranges 1..4 and 1..60 in every environment; outside Development StoragePath required, then `Path.IsPathFullyQualified`). Validator `Mentora.Infrastructure/Services/Audio/AudioOptionsValidator.cs` (same shape as `MuxOptionsValidator`). Registered `Mentora.API/Program.cs:286-289` (`AddOptions().Bind().ValidateOnStart()` + `IValidateOptions`), `.Value` read in the [AUDIO] block `Program.cs:529-542`, before `MigrateAsync` `Program.cs:562`. |
| A2 `IAudioAvailability` / `AudioAvailability` | `Mentora.Core/Interfaces/IAudioAvailability.cs`; `Mentora.Infrastructure/Services/Audio/AudioAvailability.cs`: `ProbeAsync` `:42`, tool probe `-hide_banner -version` with a 5 s linked timeout and exit code 0 `:65`, storage probe (set, fully qualified, exists, create then delete `.write-test-<guid>`) `:84`. `FailureMessage` lists the failing parts with their setting names, no path value. Program.cs [AUDIO] block right after [MUX]: probe, log line `:537`, refusal outside Development `:541`. |
| A3 `AudioNotConfiguredException` → 503 | `Mentora.Core/Exceptions/AudioNotConfiguredException.cs` (message `AUDIO_NOT_CONFIGURED`); `Mentora.API/Middleware/GlobalExceptionMiddleware.cs:68-72`, enveloped, same shape as the MUX_NOT_CONFIGURED catch, placed before the `InvalidOperationException` catch. |
| A4 `AudioAnalysisResult` | `Mentora.Core/Messaging/AudioAnalysisResult.cs`: enum `AudioAnalysisOutcome` `:4` (Ok, UnsupportedFormat, TooShort, TooLong, Undecodable), record `:30` with factories `Ok(...)` and `Failed(outcome)` (non Ok carries no content type, duration or waveform; `Failed(Ok)` throws). |
| A5 `IVoiceAudioProcessor` / `FfmpegVoiceAudioProcessor` | `Mentora.Core/Interfaces/IVoiceAudioProcessor.cs`; `Mentora.Infrastructure/Services/Audio/FfmpegVoiceAudioProcessor.cs`: `AnalyzeAsync` `:56` (throws `AudioNotConfiguredException` when not available); magic bytes `:149` (ADTS `byte0==0xFF && (byte1&0xF6)==0xF0`, M4A bytes 4..7 `ftyp`, else UnsupportedFormat with no process); `SemaphoreSlim(MaxConcurrentDecodes)` acquired with `WaitAsync(ct)` `:68`, then one linked timeout of `DecodeTimeoutSeconds` covering ffprobe and ffmpeg; ffprobe arguments `:99-100`, rules `:176` (first audio stream codec `aac`; format `aac` for ADTS, list containing `mp4` or `m4a` for M4A; non zero exit, capped or unparsable output = Undecodable); ffmpeg arguments `:121` (`-t 121`, `s16le` on `pipe:1`, stdout cap 121 x 8000 x 2 = 1,936,000 bytes); duration = samples x 1000 / 8000; waveform `:220` (48 slices, RMS, `Math.Round(rms/max*100, AwayFromZero)`, clamped); timeout `:77` (process tree killed by the runner, Undecodable, warning line). Log lines `:259` (Ok) and `:267` (not Ok, with exit code and the first 200 characters of stderr, line breaks replaced by spaces). |
| A5 hardening and convention (reviewer request, after the runtime proofs) | Both processes now get input options before the input file: `-protocol_whitelist file` and a demuxer forced from the magic bytes (`-f aac` for ADTS, `-f mov` for M4A, `FfmpegVoiceAudioProcessor.cs:94`), so user content is never sniffed into another demuxer and only local files are read; ffprobe `-v error -protocol_whitelist file -f <aac\|mov> -show_entries ... -of json <file>` (`:99-100`), ffmpeg `-v error -nostdin -protocol_whitelist file -f <aac\|mov> -i <file> -map 0:a:0 ...` (`:121`); the format_name check is unchanged, and the `switch` expression in `ProbeMatches` is replaced by an if chain (project rule). Build after the change: 0 errors, same 19 CS1573. P1 to P7 ran before this change; they do not call the processor, and the processor is proven end to end in 13.4, through the routes, in the image, with these arguments. |
| A5 process handling | `Mentora.Infrastructure/Services/Audio/AudioProcessRunner.cs` (internal): `RunAsync` `:43`, `UseShellExecute=false`, `ArgumentList` only, stdout and stderr redirected, stderr read concurrently and kept to 4096 characters (still drained beyond), stdout read into a capped buffer (beyond the cap: reading stops and the tree is killed), cancellation registration kills the tree (`Kill(entireProcessTree: true)`), `finally` kill, `using` dispose in all paths; a missing executable returns "not started" instead of throwing. |
| A6 `IVoiceMessageStorage` / `DiskVoiceMessageStorage` | `Mentora.Core/Interfaces/IVoiceMessageStorage.cs`; `Mentora.Infrastructure/Services/Audio/DiskVoiceMessageStorage.cs`: `RelativePathFor` `:25` (`{conversationId:D}/{messageId:D}.aac`), `CreateIncomingFilePath` `:29` (`<root>/.incoming/<guid>.upload`, directory created on demand), `CommitAsync` `:37` (`File.Move(overwrite: false)` after creating the conversation directory), `OpenRead` `:49` (FileShare.Read, async, sequential), `Exists` `:54`, `Delete` `:57` and `DeleteIncoming` `:71` (best effort, warning without the root path), path check `EnsureInside` `:97` (`Path.GetFullPath`, must start with root + separator, else `InvalidOperationException`; ordinal on Linux, ordinal ignore case on Windows). Every member except `RelativePathFor` throws `AudioNotConfiguredException` when `StorageOk` is false. |
| A7 DI | `Mentora.API/Program.cs:286-292`: options, validator, `AudioAvailability`, `FfmpegVoiceAudioProcessor`, `DiskVoiceMessageStorage`, all singletons. |
| A8 Dockerfile | `Mentora.API/Dockerfile:2` (base stage only): `RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*`. Nothing else changed. |

## 2. Files changed

- New: `Mentora.Core/Options/AudioOptions.cs`, `Mentora.Core/Exceptions/AudioNotConfiguredException.cs`, `Mentora.Core/Interfaces/IAudioAvailability.cs`, `Mentora.Core/Interfaces/IVoiceAudioProcessor.cs`, `Mentora.Core/Interfaces/IVoiceMessageStorage.cs`, `Mentora.Core/Messaging/AudioAnalysisResult.cs`.
- New: `Mentora.Infrastructure/Services/Audio/AudioOptionsValidator.cs`, `AudioProcessRunner.cs`, `AudioAvailability.cs`, `FfmpegVoiceAudioProcessor.cs`, `DiskVoiceMessageStorage.cs`.
- Changed: `Mentora.API/Program.cs` (one `using`, DI block, [AUDIO] startup block), `Mentora.API/Middleware/GlobalExceptionMiddleware.cs` (one catch), `Mentora.API/Dockerfile` (one line in the base stage).
- New: `docs/review/lot13/13.3/runtime-evidence.md` (this file).
- Not changed: appsettings*.json, `.dockerignore`, compose files, nginx, controllers, hub, ConversationService, migrations, NuGet packages.

## 3. Build

`dotnet build --no-incremental`: 0 errors, 19 warnings, all `CS1573` in `ProgramSessionResponse.cs`, `LastPerformedResponse.cs`, `ProgramExerciseActualRequest.cs` and the two `*ConversationMessagesController.cs` files. Same count as 13.2: no new warning.

## 4. Runtime proofs on the PC

Shape of the commands: helper scripts in `$env:TEMP\mentora-lot13-133\` (outside the repo, deleted at the end), each run in its own `powershell.exe` so the environment changes stay in the child. The API is started with `cmd /c "dotnet run ... > log 2>&1"`, `Email__Provider=Logging` in the child, every `Audio__*` variable removed first. Printed lines only those matching `\[AUDIO\]|\[CORS\]|\[MUX\]|\[EMAIL\] routing|Applying migration|No migrations|Application started|Unhandled exception|fail:|crit:` and never `OTP|EMAIL-LOG`. Stop by `taskkill /T /F`, then port 5243 checked.

Preflight: clean tree, branch `feature/lot_13`, top `6ff374d`, `4206937`, `bbafcc5`; `EmailProvider=Logging TestRecipients=True AdminEmails=True AdminJwtLength=64`; port 5243 free; Docker server 29.2.1; `ffmpeg` not found on the PC.

### P1 Development start (launch profile http, no Audio__*)

```
[run-api] case=p1 started=True exit=0x00000001
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=0 localhost=True
[CORS] debugOrigins=0
[MUX] configured=False corsOrigin=none apiBase=api.mux.com
[AUDIO] configured=False ffmpeg=False ffprobe=False storage=False maxConcurrent=2 timeoutSeconds=10
No migrations were applied. The database is already up to date.
Application started. Press Ctrl+C to shut down.
[run-api] port5243Free=True
```

Exit `0x00000001` is the forced stop by taskkill.

### P2 Production refusals

`dotnet run --project Mentora.API --no-build --no-launch-profile`, `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://localhost:5243`, dummy connection string (port 1), random 48 byte base64 `Jwt__Secret` generated in the script, `AdminAuth__*` and `EmailRouting__TestRecipients` inherited, throwaway `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret` (no spaces), `Mux__CorsOrigin=https://admin.mentorapp.fr`, throwaway `LiveKit__ApiKey` and `LiveKit__ApiSecret`, `Cors__AllowedOrigins=https://admin.mentorapp.fr`.

Each case: exit code `0xE0434352` (unhandled exception), no `Applying migration` line, the `[EMAIL] routing`, `[CORS]` and `[MUX]` lines before the refusal, port 5243 free afterwards.

| Case | Setting | Refusal line |
|---|---|---|
| a | `Audio__StoragePath` unset | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Audio:StoragePath must be set as an environment variable outside Development.` |
| b | `Audio__StoragePath=media` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Audio:StoragePath must be an absolute path.` |
| c | `Audio__MaxConcurrentDecodes=9` (StoragePath unset) | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Audio:MaxConcurrentDecodes must be between 1 and 4.` |
| d | `Audio__StoragePath` = an existing scratch folder | `[AUDIO] configured=False ffmpeg=False ffprobe=False storage=True maxConcurrent=2 timeoutSeconds=10` then `Unhandled exception. System.InvalidOperationException: Audio is not available: ffmpeg did not run (Audio:FfmpegPath); ffprobe did not run (Audio:FfprobePath).` |

## 5. Docker proofs

### P3 build

`docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot13-3-test .`: exit 0.

| Image | Size (docker image ls) |
|---|---|
| `mentora-api:lot12-v2` (most recent previous) | 356MB |
| `mentora-api:lot13-3-test` | 942MB |

ffmpeg layer (`docker history`): `429MB RUN /bin/sh -c apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf` (truncated). The base is `aspnet:8.0` with `ASPNET_VERSION=8.0.34`, Debian bookworm. The layer pulls the full Debian ffmpeg dependency set (codecs and libraries); the image grows by about 586MB as listed (the 429MB layer plus the newer base and app layers, as reported by the local image store).

### P4 tools in the image

```
ffmpeg exit=0 :: ffmpeg version 5.1.9-0+deb12u1 Copyright (c) 2000-2026 the FFmpeg developers
ffprobe exit=0 :: ffprobe version 5.1.9-0+deb12u1 Copyright (c) 2007-2026 the FFmpeg developers
```

### P5 startup in the image (Production, dummy connection string, `Audio__StoragePath=/app/media`)

Shape: `docker run --rm --name <name> --platform linux/amd64 -e NAME ... -v <scratch>\media:/app/media[:ro] mentora-api:lot13-3-test`. Every value is set in the helper script environment and passed as `-e NAME` without a value (docker reads it from the environment), so no value is on the command line. Same throwaway values as P2, plus `Email__Provider=Logging`. Stopped after 90 s if still running (not needed: both exited on their own).

(a) read write mount:

```
[p5a] mount=rw envVars=15 exit=139
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=0
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
Unhandled exception. Npgsql.NpgsqlException (0x80004005): Failed to connect to 127.0.0.1:1
[p5a] containerGone=True
[p5a] filesLeftInMedia=0
```

The probe passes before the migrations; the start then fails on the dummy database (expected). The write test file was deleted (0 file left in the mount).

(b) read only mount:

```
[p5b] mount=ro envVars=15 exit=139
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=0
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[AUDIO] configured=False ffmpeg=True ffprobe=True storage=False maxConcurrent=2 timeoutSeconds=10
Unhandled exception. System.InvalidOperationException: Audio is not available: storage is not set, not absolute, missing or not writable (Audio:StoragePath).
[p5b] containerGone=True
[p5b] filesLeftInMedia=0
```

No migration attempted (the refusal is thrown at `Program.cs:541`, before `MigrateAsync` `:562`).

### Extra check: exit code 139 comparison (reviewer request)

The previous image `mentora-api:lot12-v2` was run with exactly the P5 (a) settings, without the Audio variable and without a mount (`--rm`, 90 s limit, not reached):

```
[p5cmp] mount=rw envVars=15 exit=139
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
Unhandled exception. Npgsql.NpgsqlException (0x80004005): Failed to connect to 127.0.0.1:1
```

(`mount=rw` is a label of the helper script; no `-v` was passed for this run. The lot12-v2 image predates `Cors__DebugOrigins`, hence no `debugOrigins` line.)

| Run | Image | Exit code |
|---|---|---|
| P5 (a) | `mentora-api:lot13-3-test` | 139 |
| P5 (b) | `mentora-api:lot13-3-test` | 139 |
| comparison | `mentora-api:lot12-v2` | 139 |

Last 15 lines of each output (stdout and stderr are captured separately), lines matching `Password|Host=|Username=|Jwt|Secret|Token` removed:

- P5 (a): stdout 44 lines, last 15 = EF `warn: ... Model.Validation[20601]` sentinel warnings (0 removed). stderr 24 lines, 12 of the last 15 removed, the 3 left are `at Program.<Main>$(String[] args) in /src/Mentora.API/Program.cs:line 562`, `... line 575`, `at Program.<Main>(String[] args)`.
- P5 (b): stdout 12 lines (the info lines up to `[AUDIO] configured=False ...`, 1 removed), stderr 3 lines (0 removed): the `Unhandled exception. System.InvalidOperationException: Audio is not available: ...` line, `at Program.<Main>$(String[] args) in /src/Mentora.API/Program.cs:line 541`, `at Program.<Main>(String[] args)`.
- comparison: stdout 40 lines, same EF sentinel warnings (0 removed); stderr 24 lines, 12 of the last 15 removed, the 3 left are `... Program.cs:line 528`, `... line 541`, `at Program.<Main>(String[] args)`.
- Keyword counts over the whole output of each run: `SIGSEGV|Segmentation` 0, `createdump` 0, `.so`, `libcoreclr` or `libSystem.Native` 0, `Unhandled exception` 1.
- The removed counts are larger than the sensitive content: the filter is case insensitive, so Npgsql and EF stack frames containing `CancellationToken` match `Token`, and `[CORS] origins=1 localhost=False` matches `Host=`. No secret appears in the outputs; nothing was printed from the removed lines.

Conclusion: the .NET unhandled exception text and its stack trace are printed in full before the process ends, nothing mentions a segmentation fault, createdump or a native library, and the same exit code 139 already occurs with `mentora-api:lot12-v2`, before ffmpeg was added. Exit code 139 on a startup failure is therefore not caused by the ffmpeg layer or by 13.3. No code was changed for this check.

### P6 audio test files (kept for 13.4)

Generated with the image's ffmpeg (`docker run --rm --platform linux/amd64 -v "$env:TEMP\mentora-lot13-audio:/out" --entrypoint ffmpeg mentora-api:lot13-3-test -hide_banner -v error -y ...`), mono 44100 Hz, AAC 64k unless stated; each generation exit 0. `fake-adts.aac` and `big-6mb.aac` written by PowerShell (random bytes, first two bytes `0xFF 0xF1`). Folder: `$env:TEMP\mentora-lot13-audio\` (never in the repo).

| File | Bytes | Under 5 MB | ffprobe (image) `codec_name` / `format_name,duration` |
|---|---|---|---|
| `tone-3s.aac` (ADTS, sine 440 Hz 3 s) | 25395 | yes | `aac` / `aac,2.982910` |
| `ramp-4s.aac` (ADTS, amplitude 0 to 1 over 4 s) | 34133 | yes | `aac` / `aac,4.099260` |
| `tone-3s.m4a` (M4A, `-movflags +faststart -f ipod`) | 25809 | yes | `aac` / `"mov,mp4,m4a,3gp,3g2,mj2",3.000000` |
| `tone-3s.wav` (WAV, `pcm_s16le`) | 264678 | yes | `pcm_s16le` / `wav,3.000000` |
| `short-0.2s.aac` (ADTS, 0.2 s) | 2053 | yes | `aac` / `aac,0.232194` |
| `long-125s.aac` (ADTS, 125 s, 24k) | 417059 | yes | `aac` / `aac,130.443037` |
| `silence-2s.aac` (ADTS, `anullsrc`, 2 s) | 985 | yes | `aac` / `aac,2.010717` |
| `fake-adts.aac` (random, 64 KB) | 65536 | yes | not probed (by design) |
| `big-6mb.aac` (random, 6 MB) | 6291456 | no (by design) | not probed (by design) |

Note: for raw ADTS, ffprobe estimates `duration` from the bit rate (`long-125s.aac` shows 130.44 s for a 125 s source, `short-0.2s.aac` 0.23 s). This is why the processor measures the duration from the decoded PCM sample count, not from ffprobe.

### P7 cleanup

The scratch folder `$env:TEMP\mentora-lot13-133\` (helper scripts, logs, the empty media folders) is deleted at the end (Test-Path False, see the final report). Kept: `$env:TEMP\mentora-lot13-audio\` (9 files) and the image `mentora-api:lot13-3-test` for 13.4.

## 6. Differences from the expected results

1. Container exit code 139 (not the Windows `0xE0434352`) on the P5 startup failures. The reviewer's extra check shows the same 139 with `mentora-api:lot12-v2`: it predates 13.3 and ffmpeg. The unhandled exception text is printed in full before the exit.
2. The image grows from 356MB to 942MB; the ffmpeg layer alone is 429MB (Debian `ffmpeg` with its dependencies, even with `--no-install-recommends`). Accepted as designed in A8; a slimmer install would be a separate decision.
3. P2 (c) was run with `Audio__StoragePath` unset: the range check comes first in `AudioOptions.Validate`, so the out of range refusal is printed (as expected), not the StoragePath one.
4. ffprobe durations of raw ADTS files are estimates (P6 note); no impact since the processor uses the decoded sample count.
