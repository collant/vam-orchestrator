import net from 'net';
import {sendJSON, receiveJSONs} from './utils.js'


const secret = "fast-92813dlkcvoiej5w98lxclpj239slk"
const host = 'localhost'
const browserPort = 8080
const orchestratorPort = 8000
const webSocketPort = 8081


const socketMapping = {}
let speechSocket = undefined
let llmSocket = undefined
const server = net.createServer((socket) => {
    console.log('New client connected');

    receiveJSONs(socket, (json) => {
        if (socket === speechSocket) {
            var clientSockets = undefined
            if (json.bucket && json.bucket.secret) {
                clientSockets = socketMapping[json.bucket.secret]
            }
            if (clientSockets !== undefined) {
                if (json.type === 'wav') {
                    const data = {
                        type: 'wav',
                        payload: {message: json.payload.text, wav: json.payload.wav},
                    }

                    var wsSocket = clientSockets.ws
                    if (wsSocket) {
                        wsSocket.send(JSON.stringify(data))
                    }

                    var vamSocket = clientSockets.vam
                    if (vamSocket) {
                        sendJSON(JSON.stringify(json.payload), vamSocket);
                    }
                } else if (json.type === 'text') {
                    console.log('received text from speech worker: ', json.payload)
                    var uiState = clientSockets.uiState;
                    if (uiState === undefined) {
                        uiState = getInitialUiState();
                        clientSockets.uiState = uiState;
                    }

                    var uiMessage = {
                        type: 'message',
                        payload:{me: true, message: json.payload}
                    }
                    
                    uiState.history.push(uiMessage)

                    if (json.bucket && json.bucket.secret && socketMapping[json.bucket.secret] && socketMapping[json.bucket.secret].ws) {
                        socketMapping[json.bucket.secret].ws.send(JSON.stringify(uiMessage))
                    }

                    chatAPI(uiState, json.bucket.secret);
                    actionsAPI(uiState, json.bucket.secret);
                }
            }
            return;
        }
        if (socket === llmSocket) {
            var receivedMessage = json.message
            if (!json.bucket) {
                return
            }
            if (json.bucket.choices) {
                try{
                    var choices = JSON.parse(json.message)
                    if (choices.length) {
                        var actionChoices = {
                            type: 'multi_choice',
                            payload: {choices, inProgress: json.inProgress, message: json.message, me: true}
                        }
                        if (json.bucket && json.bucket.secret && socketMapping[json.bucket.secret] && socketMapping[json.bucket.secret].ws) {
                            socketMapping[json.bucket.secret].ws.send(JSON.stringify(actionChoices))
                        }
                    }
                } catch(e) {}
                return
            }

            if (json.bucket.type === 'action') {
                if (json.inProgress) {
                    return;
                }
                try{
                    var action = json.message
                    
                    if (action) {
                        if (json.bucket && json.bucket.secret) {
                            var clientSockets = socketMapping[json.bucket.secret]
                            if (clientSockets) {
                                var vamSocket = clientSockets.vam
                                if (vamSocket) {
                                    var vamAction = {action: action}
                                    sendJSON(JSON.stringify(vamAction), vamSocket);
                                }
                            }
                        }
                        
                        var actionPayload = {
                            type: 'action',
                            payload: {inProgress: json.inProgress, message: action}
                        }
                        if (json.bucket && json.bucket.secret && socketMapping[json.bucket.secret] && socketMapping[json.bucket.secret].ws) {
                            //socketMapping[json.bucket.secret].ws.send(JSON.stringify(actionPayload))
                        }
                    } else {
                        console.log('Did not receive action', json)
                    }
                } catch(e) {
                    console.log('Cannot parse: ', e, json)
                }
                return
            }
            
            if (!json.inProgress) {
                choicesAPI(socketMapping[json.bucket.secret].uiState, receivedMessage,json.bucket.secret);
                var speakerId = socketMapping[json.bucket.secret].speakerId
                if (!speakerId) {
                    speakerId = "p243"
                }
                var speech = requestSpeech(receivedMessage, speakerId, json.bucket);
                if (speech) {
                    return;
                }
            }
            var actionChoices = {
                type: 'message',
                payload: {message: receivedMessage, inProgress: json.inProgress}
            }
            if (json.bucket && json.bucket.secret && socketMapping[json.bucket.secret] && socketMapping[json.bucket.secret].ws) {
                socketMapping[json.bucket.secret].ws.send(JSON.stringify(actionChoices))
            }
            return;
        }

        if (json.type === 'wav') {
            if (!socketMapping[json.secret]) {
                socketMapping[json.secret] = {}
            }
            var config = json.config
            console.log('config', config)
            if (config) {
                var actions = config.actions
                if (actions) {
                    socketMapping[json.secret].actions = actions
                }
                var speakerId = config.speakerId
                if (speakerId) {
                    socketMapping[json.secret].speakerId = speakerId
                }
                var triggers = config.triggers
                if (triggers) {
                    socketMapping[json.secret].triggers = triggers
                }
            }
            socketMapping[json.secret].vam = socket
            requestText(json.payload, {secret: json.secret})
            return;
        }
        

        if (json.type === 'handshake' && json.payload === 'vam' && json.secret !== undefined) {
            if (!socketMapping[json.secret]) {
                socketMapping[json.secret] = {}
            }
            socketMapping[json.secret].vam = socket
            console.log('New VAM client handshaked: ', json.secret);
            return;
        }

        if (json.type === 'handshake' && json.payload === 'speech' && json.secret === secret) {
            speechSocket = socket;
            console.log('Speech worker handshaked');
            return;
        }
        if (json.type === 'handshake' && json.payload === 'llm' && json.secret === secret) {
            llmSocket = socket;
            console.log('LLM worker handshaked');
            return;
        }
        console.log("Unknown worker", json)
    })
    socket.on('end', () => {
        console.log('client disconnected');
    });
    socket.on('error', (error)=> {
        console.error(error);
    })
});

server.listen(orchestratorPort, host,() => {
    console.log('server bound');
});

const requestText = (wavPayload, bucket) => {
    if (speechSocket === undefined) {
        return false;
    }
    const data = {
        type: 'wav',
        payload: wavPayload,
        bucket
    }
    sendJSON(JSON.stringify(data), speechSocket);
    return true
}

const requestSpeech = (text, speakerId, bucket) => {
        if (speechSocket === undefined) {
            return false;
        }
        const data = {
            type: 'text',
            payload: text,
            speakerId: speakerId,
            tempId: 42,
            bucket
        }
        sendJSON(JSON.stringify(data), speechSocket);
        return true;
}

const getInitialUiState = () => {
    return {
        history: [],
        context: tempPayload.payload.choices[2].content
    }
}

const getConvPrompt = (uiState) => {
    var prompt = uiState.context;
    if (uiState.history.length > 0 && uiState.history[uiState.history.length - 1].payload.message !== "") {
      const context = uiState.context;
      prompt = uiState.history.reduce((cum, val)=> {
          if (val.type === 'multi_choice' || val.type === "scene" || val.payload.message === "" || val.type === 'action') {
              return cum;
          }
          if (!cum.endsWith("###Emily:") && !cum.endsWith("###Emily: ") && !val.payload.me) {
            cum+= "###Emily: "
          }
          if (!cum.endsWith("###Jack:") && !cum.endsWith("###Jack: ") && val.payload.me) {
            cum+= "###Jack: "
          }
          cum+= val.payload.message
          return cum;
      }, context);
      if (!prompt.endsWith("###Emily:")) {
          prompt = prompt + "###Emily:"
      }
    }
    return prompt;
}

const actionsAPI = (uiState, secret) => {
    var currentSocket = socketMapping[secret];
    var actions
    if (currentSocket) {
        actions = currentSocket.actions || ["LookToMe", "LookAway", "CuteFace", "HappyFace","GrindingFace", "AngryFace", "SuspiciousFace", "SadFace"]
    }
    var prompt = uiState.history[uiState.history.length - 1].payload.message;
    console.log('actionsAPI prompt:' + prompt + '.')
    const llmConfig = {
        prompt,
        bucket: {
            secret,
            type: 'action',
            startWith: '{"answer": "'
        },
        stop: ["#", "\n"],
        stopAt: ["}"],
        limit: 0,
        actions,
    }
    
    if (llmSocket !== undefined) {
        sendJSON(JSON.stringify(llmConfig), llmSocket);
    }
}

const choicesExamples = `A chat between Anna and John. Anna is a college girl, she is 18 years old, and she needs a lot of money. Anna will accept to have sex if John gives her 1000 dollars.
###John: Hey###Anna: Let's role play, you are a teacher and I am your naughty student.
These are two possible replies from John:
["I'm not a teacher, I'm John.",  "Sure, let's do it."]

###John: Sure, let's do it.###Anna: I am sitting in my place while you are teaching with the whole class.
These are two possible replies from John:
["I'm not teaching, I'm just here to flirt with you.", "Ok, let's start the class."]

###John: Ok, let's start the class.###Anna: Teacher, can I answer that tough question please?
These are two possible replies from John:
["Sure, go ahead.", "No, it's too difficult for you."]

###John: Sure, go ahead.###Anna: Oh, I don't know the answer, can you spank me for that?
These are two possible replies from John:
["I can't spank you, you're too old for that.", "Sure, you deserve it."]

###John: Sure, you deserve it.###Anna: Are you going to spank me on my ass?
These are two possible replies from John:
["Yes, on the spot.", "No, not here, let's go somewhere private."]

###John: No, not here, let's go somewhere private.###Anna: We are alone inside the closet now.
These are two possible replies from John:
["Ok, let's start with some caning.", "No, I don't want that, I'm scared.", "Ok, let's start with some spanking."]

`
const choicesAPI = (uiState, emilyResponse, secret) => {
    let prompt = choicesExamples + getConvPrompt(uiState) + emilyResponse + "\nThese are two possible replies from Jack:\n" 
    console.log('prompt:' + prompt + '.')
    const llmConfig = {
        prompt,
        bucket: {
            secret,
            choices: true
        },
        stop: ["#", "\n"],
        stopAt: ["}"],
        limit: 0
    }
    
    if (llmSocket !== undefined) {
        sendJSON(JSON.stringify(llmConfig), llmSocket);
    }
}

const chatAPI = (uiState, secret) => {
    let prompt = getConvPrompt(uiState)
    console.log('prompt:' + prompt + '.')
    const llmConfig = {
        prompt,
        bucket: {
            secret
        },
        stop: ["#", "\n"],
        stopAt: ["}"],
        limit: 0
    }
    
    if (llmSocket !== undefined) {
        sendJSON(JSON.stringify(llmConfig), llmSocket);
    }
}


import WebpackDevServer from 'webpack-dev-server';
import webpack from 'webpack';
import config from './www/webpack.config.js';

const compiler = webpack(config);

const webServer = new WebpackDevServer({
  hot: true,
}, compiler);

webServer.listen(browserPort, host, (err) => {
  if (err) {
    // console.error(err);
  } else {
    console.log('Server listening at http://' + host + ':' + browserPort);
  }
});

import { WebSocketServer } from 'ws';

const wss = new WebSocketServer({ port: webSocketPort, host: host });

wss.on('connection', function connection(ws) {
  console.log('New browser connected');
  ws.on('error', console.error);

  ws.on('message', function message(buffer) {
    var received = buffer.toString("utf-8");
    console.log('received: ', received);
    try {
        var json = JSON.parse(received);
        if (json.type === 'handshake' && json.payload === 'browser') {
            if (!socketMapping[json.secret]) {
                socketMapping[json.secret] = {}
                console.log('New browser hand shaked: ' + json.secret);
            }
            socketMapping[json.secret].ws = ws
        } else if (json.type === 'message') {
            var uiState = json.payload;
            console.log('received uiState', uiState);
            socketMapping[json.secret].uiState = uiState;
            chatAPI(uiState, json.secret)
            actionsAPI(uiState, json.secret)
        }
    } catch(e) {
        console.log(e)
    }
  });

  ws.send(JSON.stringify(tempPayload))
});
const tempPayload = {
    type: 'scene',
    payload: {
        message: 'Choose one scene',
        choices: [
            {
                title: 'Assistant',
                content: "A chat between a curious human and an artificial intelligence assistant named Emily. The assistant gives helpful, detailed, and polite answers to the human's questions.### Human: What is your name?"
            },
            {
                title: 'Dating',
                content: "A chat between Emily and Jack. Emily is 18 years old, and she is dating Jack.###Jack: I miss you.###Emily: I didn't see you in a while Jack!###Jack: Let's live together"
            },
            {
                title: 'Classroom',
                content: "A chat between Emily and Jack. Emily is a college girl, she is 18 years old, and she needs a lot of money. Jack is her teacher and she is his naughty student###Jack: The class ends, I won't hold you longer, let's go home now.###Emily: I feel so horny.###Jack: What was that!"
            },
            {
                title: 'Sugar daddy',
                content: "A chat between Emily and Jack. Emily is 18 years old, she is dating her sugar daddy Jack who is 40 years old. They haven't been together in a while. They are in a hotel room###Jack: I am glad you are here.###Emily: I need some money.###Jack: I am here to provide for you babe."
            },
            {
                title: 'Hero',
                content: "A chat between Emily and Jack. Emily is 18 years old. Jack just saved her from a wild animal in the jungle, they have to spend the night together inside a cabin. Emily is very attracted to Jack###Jack: How do you feel now?###Emily: I am scared.###Jack: Don't worry, I won't let anything bad happen to you."
            }
        ]
    }
  }