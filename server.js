import net from 'net';
import wav from 'wav';
import websocket from 'ws';
import fetch from 'node-fetch'
import {sendJSON, receiveJSONs, resetBuffer} from './utils.js'

const server = net.createServer((socket) => {
    console.log('New client connected');

    let config = { prompt: ['The beauty and the beast'], sendActions: 'true', actions: ['Look to me', 'Look away'], newTokensLLM: '50', lastConversationsLLM: '5'};
    const history = [
        {actor: botName, text: 'How can I be of help'},
        {actor: humanName, text: 'I want some love in my life.'},
        {actor: botName, text: 'I can help with that'}
    ]
    resetBuffer();

    receiveJSONs(socket, (json) => {
        config = json.config;
        console.log(config)
        if (json.type === 'wav') {
            handlePayload(json.payload, (transcript) => {
                handleTranscript(transcript, 'wav', socket, history, config)
            })
        }
        if (json.type === 'trigger') {
            let trigger = json.triggerName;
            handleTranscript("User has touched your " + trigger, 'trigger', socket, history, config)
        }
    })
    socket.on('end', () => {
        console.log('client disconnected');
    });
    socket.on('error', (error)=> {
        console.error(error);
    })
});

server.listen(8000, () => {
    console.log('server bound');
});


const handleTranscript = (transcript, type, socket, history, config) => {
    let transcriptNotEmpty = transcript != '' ? transcript : "Sorry, I didn't get that";
    if (config.sendToLLM == 'true') {
        console.log('sending to LLM');
        getCompletion(transcriptNotEmpty, type, history, config).then(completion => {
            getVoice(completion.text, config.speakerId, socket, completion.action, history);
        })
    }
    if (config.echoBack == 'true') {
        console.log('echoing back');
        getVoice(transcriptNotEmpty, config.speakerId, socket, undefined, history);
    }
    if (config.echoBack == 'false' && config.sendToLLM == 'false') {
        var json = {
            type: 'text',
            text: transcriptNotEmpty,
            config: {
                history
            }
        }
        if (socket) {
            const jsonString = JSON.stringify(json);
            sendJSON(jsonString, socket);
        }
    }
}

const handlePayload = (wavPayload, onTranscriptReady) => {
    const options = {
        channels: 1,
        sampleRate: 44100,
        bitDepth: 16
    }
    const data = Buffer.from(wavPayload, 'base64');
    
    // You can save the wav into a file by uncommenting the next 3 lines
    // const fileWriter = new wav.FileWriter(fileName, options);
    // fileWriter.write(data);
    // fileWriter.end();

    const writer = new wav.Writer(options);
    let buffer = [];
    writer.on('data', (chunk) => {
        var arr = Array.prototype.slice.call(chunk, 0)
        buffer = buffer.concat(arr);
    });

    writer.on('end', () => {
        transcribe(buffer, onTranscriptReady);
    });
    
    writer.write(data);
    writer.end();
}

const getVoice = (text, speakerId, socket, action, history) => {
    let url = "http://localhost:5002/api/tts?text=" + text + "&speaker_id=" + speakerId + "&style_wav="

    console.log('getting voice for: ', text);
    return new Promise((resolve, reject) => {
        fetch(url, {
        method: 'GET'
        })
        .then(response => response.arrayBuffer())
        .then(arrayBuffer => {
            const fileBuffer = Buffer.from(arrayBuffer);

            var reader = new wav.Reader();

            var buffer = [];
            var header = {};
            reader.on('format', function (format) {
                header = format;
            });
            
            reader.on('data', (chunk)=> {
                var arr = Array.prototype.slice.call(chunk, 0)
                buffer = buffer.concat(arr);
            });
            reader.on('end', () => {
                var json = {
                    payload: Buffer.from(buffer).toString('base64'),
                    type: 'wav',
                    sampleRate: header.sampleRate,
                    bitDepth: header.bitDepth,
                    channels: header.channels,
                    text,
                    action: action || "",
                    config: {
                        history
                    }
                }
                if (socket) {
                    const jsonString = JSON.stringify(json);
                    sendJSON(jsonString, socket);
                }
            });            
            reader.write(fileBuffer);
            reader.end();
            return resolve(buffer.length)
        })
        .catch(error => reject(error));
    });

}


const transcribe = (wavFileBytes, onTranscript) => {
    console.log('transcribing...');
    const ws = new websocket('ws://localhost:20741');
    ws.on('open', function open() {
        let message = {"type":"welcome","data":{"samplerate":44100,"continuous":true,"language":"en-US","task":"","model":"vosk-model-small-en-us:assistant","optimizeFinalResult":true,"doDebug":false},"client_id":"any","access_token":"test1234","msg_id":1}
        ws.send(JSON.stringify(message));
        
        if (wavFileBytes) {
            ws.send(wavFileBytes)
        }
        // else {
        //     var readStream = fs.createReadStream(fileName);
        //     let buffer = []
        //     readStream.on('data', function (chunk) {
        //         var arr = Array.prototype.slice.call(chunk, 0)
        //         buffer = buffer.concat(arr);
        //     });
        //     readStream.on('end', function () {
        //         ws.send(buffer);
        //     });
        // }
    });
        
    ws.on('message', function incoming(buffer) {
        let data = buffer.toString();
        let json = JSON.parse(data);
        if (json.type === 'result') {
            if (onTranscript) {
                onTranscript(json.transcript);
            } else {
                console.error('No onTranscript function were provided')
            }
            ws.close();
        }
        if (json.type === 'error') {
            onTranscript(json.message);
            console.error(json.message);
        }
        
    });
        
    ws.on('close', function close() {
    });
    ws.on('error', (error)=> {
        onTranscript('Node server cannot connect to STT server')
        console.error(error);
    })
}

const botName = 'Bot';
const humanName = 'User';
const getCompletion = (text, type, history, config) => {
    if (type == 'trigger') {
        history.push({actor: '', text});
    } else {
        history.push({actor: humanName, text});
    }

    let prompt = 'Answer with only one valid json object that contains two properties, action and text, the text is a one liner answer';
    if (config.sendActions == 'true' && config.actions && config.actions.length > 0) {
        prompt += '\nAvailable actions: '  + JSON.stringify(config.actions);
    }
    prompt += '\nStory: ' + config.prompt.reduce((acc, val) => acc + '\n' + val, '');
    const memorySize = parseInt(config.lastConversationsLLM) || 0;
    let historyString = history.slice(-memorySize).reduce((acc, val) => {
        let conversation;
        if (val.actor != '') {
            conversation = acc + '\n' + val.actor + ': ' + val.text;
        } else {
            conversation = acc + '\n' + val.text;
        }
        return conversation;
    }, '');
    prompt+= '\nHistory:' + historyString + '\n\n'

    console.log('sending this prompt: \n', prompt)
    const apiKey = process.argv[2] || config.apiKey;
    return new Promise((resolve, reject) => {
      fetch('https://api.openai.com/v1/completions', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            model: "text-davinci-003",
            prompt,
            max_tokens: parseInt(config.newTokensLLM),
            temperature: 0.6,
            user: "First-user-2.3c00392c8a823b32989"
        })
      })
        .then(response => response.json())
        .then(result => {
            console.log(JSON.stringify(result, null, 2))
            try {
                let completion = result.choices[0].text;
                console.log('completion', completion)
                let parsed = JSON.parse(completion);
                // completion = completion.split('\n').join('').replace(/^\s+/g, '');
                history.push({actor: botName, text: parsed.text})
                //console.log('history: ', history);
                resolve(parsed);
            } catch(e) {
                if (result && result.error && result.error.message) {
                    let withError = {
                        action: "",
                        text: result.error.message,
                        error: "OpenAI error"
                    };
                    resolve(withError);
                } else {
                    let withError = {
                        action: "",
                        text: "Cannot parse the answer",
                        error: "cannot parse the answer"
                    };
                    resolve(withError);
                }
                
            }
        })
        .catch(error => reject(error));
    });
}

// getCompletion('What can you do for me?').then(completion => {
//     console.log('completion: ', JSON.stringify(completion));
// });