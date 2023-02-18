
# VAM Orchestrator

This is a node server that acts as an orchestrator between VAM, TTS server, STT server and LLM server (OpenAPI for the moment).

![image](https://user-images.githubusercontent.com/125187079/219703449-7448f4cb-449d-4481-8f07-8a947c3c57e1.png)


# Getting started

You need docker and node installed first.

You need to run three servers: node server, TTS server and STT server. So prepare three terminals for those servers.

## Node server

For the moment, we are going to use OpenAI Completion API, you need to get your OpenAI API KEY.

inside the root folder of this repository, you should run
```
npm install
node server.js YOUR_OPEN_AI_API_KEY
```

To use with Twitch:
```
node server.js YOUR_OPEN_AI_API_KEY TWITCH_USERNAME TWITCH_OAUTH_TOKEN TWITCH_CHANNEL 
```

## TTS server
``` 
docker run --rm -it -p 5002:5002 --gpus all -v /c/vosk:/root/.local/share/ --entrypoint /bin/bash ghcr.io/coqui-ai/tts:v0.11.1
python3 TTS/server/server.py --model_name tts_models/en/vctk/vits --use_cuda true
```
if you are having issues with the second command, please remove `--use_cuda true` from the command and try again.

Github repository: https://github.com/coqui-ai/TTS

## STT server
```
docker run --rm --name=sepia-stt -p 20741:20741 -it sepia/stt-server:dynamic_v1.0.0_amd64
```
Github repository: https://github.com/SEPIA-Framework/sepia-stt-server

# Discord server
https://discord.gg/uDWBGSxX

# Patreon
https://patreon.com/TwinWin

# Features
- Hold the converstation history.
- Send and receive voice to VAM.
- Receive triggers (events) from VAM.
- Send actions to VAM.
- Story generation inside VAM.
- Use OpenAPI for completion.
- Use Coqui tts for speech generation.
- Use Sepia stt for speech recognition.

# TODO
- Use an open source completion server.
- Better lipsync plugin.
- Host a paid solution for non technical users.
- Use an embeddings cache.
- Use stories providers.
- Voice muxer with background noise.

# Contributors guide
- No refactoring.
- Open an issue before a PR.

# Nice to have
- Multiple actors.
- Use embeddings for history.
- Support multiple languages.

