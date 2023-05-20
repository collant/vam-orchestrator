# VAM Orchestrator

This is a node server that acts as an orchestrator between VAM, Speech and LLM server.


# Getting started

You need docker, conda and node (14+, tested with 20+) installed first.

VAM Orchestrator runs five discrete services - the central node server, a node server for Speech, a TTS and STT service and of course the LLM service. If you choose to do this manually, you'll need to prepare five terminals for those servers.

Tested working on RTX 3080 and 2x RTX 2080ti with the --load-8bit and/or --num-gpus 2 (please raise an issue to share configs and GPUs that work for you)

### Architecture

If running manually, note that the central node server must be running first. Killing that service will end the LLM service automatically; and the Speech (and related) services won't start until they can connect.

By default it will run on all interfaces (0.0.0.0), meaning anyone on your local network can access it if they know your LAN IP and the ports. If you have port-forwarding enabled on your router and it points to your local IP, then be mindful anyone on the internet can access it too.

![Architecture](https://user-images.githubusercontent.com/125187079/236630803-6cd90873-734e-4bea-926b-98e117481c67.png)

## Step One: Auto-start services

Clone the repository, or download and unzip the .zip.

Copy the .env.example to .env and make any modifications you require.
```bash
cp .env.example .env
```

Then simply run:
```bash
docker-compose up
```

Finally, follow the Install LLM step beneath the manual installation steps

## Alternative Step One: Manually start services

Note the main Node server must be running first.

### Node server

Inside the root folder of this repository, you can run
```bash
npm install
node server.js
```

### TTS server

#### GPU version
```bash
docker run --rm -it -p 5002:5002 --gpus all -v /c/vosk:/root/.local/share/ --entrypoint /bin/bash ghcr.io/coqui-ai/tts:v0.11.1
python3 TTS/server/server.py --model_name tts_models/en/vctk/vits --use_cuda true
```

#### CPU version
```bash
docker run --rm -it -p 5002:5002 --entrypoint /bin/bash ghcr.io/coqui-ai/tts-cpu:v0.11.1
python3 TTS/server/server.py --model_name tts_models/en/vctk/vits
```

Github repository: https://github.com/coqui-ai/TTS

### STT server
```bash
docker run --rm --name=sepia-stt -p 20741:20741 -it sepia/stt-server:dynamic_v1.0.0_amd64
```
Github repository: https://github.com/SEPIA-Framework/sepia-stt-server

### Speech server
```bash
node speech.js
```

## Step Two: Install LLM server

First install conda: https://www.anaconda.com/download/

### While in the base directory, create a new conda environment

```bash
conda create --name imposter
conda activate imposter
```

### Install Pytorch & requirements

Windows/Linux:

```bash
conda install pytorch torchvision torchaudio pytorch-cuda=11.7 -c pytorch -c nvidia
```
Then:
```bash
cd LLM
pip install -r requirements.txt
```

If you have any errors, please create a new conda environment and install Pytorch following this page: https://pytorch.org/get-started/locally/

### Running

```bash
python run.py
```
Remember: you MUST be in the `imposter` conda context, and in the LLM directory, as a large number of packages required by the LLM are installed 'contextually' in the conda environment we created earlier. You can switch the conda context anytime with `conda activate imposter`. If your terminal does not show something like `(imposter)` at the start, and instead shows nothing or something like `(base)`, you are not in the right conda environment.

### Optional: LLM customisations
At the point of `python run.py`, you can add additional params to suit your needs. For example:

Use `--load-8bit` if you get CUDA memory errors (out of memory), this will try to load an 8bit variant of the model. Will be less impressive.
```bash
python run.py --load-8bit
```

Use --num-gpus (some number) if you want to maintain the quality and have multiple GPUs available:
```bash
python run.py --num-gpus 2
```

Use --device cpu if you have enough RAM but your GPU can't support even the --load-8bit, or you want to maintain quality. This will be extremely slow.
```bash
python run.py --device cpu
```

Use --model-name some/huggingfacemodel to change the model! Note, you may have to edit the `tokenizer = blahblah` line in server.js based on what tokenizer the huggingface model page recommends (especially if errors or strange behaviour)
```bash
python run.py --model-name EleutherAI/gpt-neox-20b
```

Use --max-new-tokens (with a default of 256) to increase how many tokens are available (and thus how long a conversation can go for)
```bash
python run.py --max-new-tokens 1024
```

Use --temperature (with a default of 0.7) to increase or decrease the randomness/stability
```bash
python run.py --temperature 0.5
```

Most of the above params can be added together, eg python run.py --temperature 0.5 --max-new-tokens 1024 --model-name EleutherAI/gpt-neox-20b

## Other changes
### Male or Other Voices
You can sample different voices at `http://{your_lan_ip}:5002`. Take note of the `p###` number. You can set the default voice in .env but should make changes to the actual VAM plugin itself to change the selectable options.

# Discord server
https://discord.gg/GqN3STn8U8

# Patreon
https://patreon.com/TwinWin

# Features
- Hold the converstation history.
- Send and receive voice to VAM.
- Receive triggers (events) from VAM.
- Send actions to VAM.
- Story generation inside VAM.
- Use open source LLM for completion.
- Use Coqui tts for speech generation.
- Use Sepia stt for speech recognition.

# TODO
- Use stories providers.
- Voice muxer with background noise, laughs, ...

# Contributors guide
- No refactoring.
- Open an issue before a PR.

# Nice to have
- Multiple actors.
- Use embeddings for history.
- Support multiple languages.
