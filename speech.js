import net from 'net';
import wav from 'wav';
import websocket from 'ws';
import fetch from 'node-fetch'
import {sendJSON, receiveJSONs} from './utils.js'

const secret = "fast-92813dlkcvoiej5w98lxclpj239slk"
const host = 'localhost'
const port = 8000
const ttsHost = "localhost"
const ttsPort = 5002
const sttHost = "localhost"
const sttPort = 20741


const getText = (wavPayload, onTranscriptReady) => {
    const options = {
        channels: 1,
        sampleRate: 44100,
        bitDepth: 16
    }
    const data = Buffer.from(wavPayload, 'base64');
    
    // You can save the wav into a file by uncommenting the next 3 lines
    // const fileWriter = new wav.FileWriter("sample.wav", options);
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
const transcribe = (wavFileBytes, onTranscript) => {
    console.log('transcribing...');
    const ws = new websocket('ws://' + sttHost + ':' + sttPort);
    ws.on('open', function open() {
        let message = {"type":"welcome","data":{"samplerate":44100,"continuous":true,"language":"en-US","task":"","model":"vosk-model-small-en-us:assistant","optimizeFinalResult":true,"doDebug":false},"client_id":"any","access_token":"test1234","msg_id":1}
        ws.send(JSON.stringify(message));
        
        if (wavFileBytes) {
            ws.send(wavFileBytes)
        }
        // else {
        //     var readStream = fs.createReadStream("test.wav");
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
            console.log('transcript: ', json.transcript)
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

const getVoice = (text, speakerId) => {
    return new Promise((resolve, reject)=> {
        getWAV(text, speakerId)
        .then(fileBuffer => {
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
                var voice = {
                    payload: Buffer.from(buffer).toString('base64'),
                    type: 'wav',
                    sampleRate: header.sampleRate,
                    bitDepth: header.bitDepth,
                    channels: header.channels,
                    text,
                    wav: fileBuffer.toString('base64')
                }
                resolve(voice)
            });            
            reader.write(fileBuffer);
            reader.end();
        })
        .catch(console.error);
    });
}

const getWAV = (text, speakerId) => {
    return new Promise((resolve, reject)=> {
        let url = "http://" + ttsHost + ":" + ttsPort + "/api/tts?text=" + text + "&speaker_id=" + speakerId + "&style_wav="
        fetch(url, {method: 'GET'})
            .then(response => response.arrayBuffer())
            .then(arrayBuffer => {
                const buffer = Buffer.from(arrayBuffer);
                resolve(buffer)
            })
            .catch(reject);
    })
}

const client = new net.Socket();

client.connect(port, host, () => {
  console.log('connected to server');
  const handshake = {
    type: 'handshake',
    payload: 'speech',
    secret
  }
  sendJSON(JSON.stringify(handshake), client)

  receiveJSONs(client, (json) => {
    console.log('received a json file', json)
    if (json.type === 'text') {
        getVoice(json.payload, json.speakerId)
        .then((voice)=> {
            const voicePayload = {
                type: 'wav',
                payload: voice,
                secret,
                bucket: json.bucket
            }
            sendJSON(JSON.stringify(voicePayload), client)
        })
    } else if (json.type === 'wav') {
        getText(json.payload, (text)=> {
            const textPayload = {
                type: 'text',
                payload: text,
                secret,
                bucket: json.bucket
            }
            sendJSON(JSON.stringify(textPayload), client)
        })
    } else {
        console.log('Unknown payload type')
    }
  })

});

client.on('error', (error)=> {
  console.error(error);
})

client.on('close', () => {
  console.log('connection closed');
});
