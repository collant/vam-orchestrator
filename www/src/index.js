import { createRoot } from "react-dom/client";
import Hello from "./Hello";

const host = pubhost || 'localhost'
const webSocketPort = pubport || '8081'

const dispatcher = {
  dispatch: undefined
}
const onMessageReceived = (message) => {
  var dispatch = dispatcher.dispatch;
  try {
    var action = JSON.parse(message);
    if (dispatch !== undefined) {
      dispatch(action)
    }
  }catch (e) {
    console.error(e);
  }
    
}
const sendMessage = (message) => {
  var action = {
    type: 'message',
    payload: message,
    secret: session
  }
  ws.send(JSON.stringify(action));
}

const container = document.getElementById("root");
const root = createRoot(container);
root.render(<Hello dispatcher={dispatcher} send={sendMessage} />);


const ws = new WebSocket('ws://' + host + ':' + webSocketPort);

ws.onerror = console.error;

function uuidv4() {
  return ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g, c =>
    (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
  );
}
const getSecret = function() {
  var url = window.location.href
  var splitted = url.split("=")
  if (splitted.length > 1) {
    return splitted[1]
  }
  return uuidv4()
}
var session = getSecret()
ws.onopen = function open() {
  // ws.send('something');
  console.log('connected to server');
  const handShake = {
    type: 'handshake',
    payload: 'browser',
    secret: session
  }
  ws.send(JSON.stringify(handShake))
};

ws.onmessage = function message(wsMessage) {
  onMessageReceived(wsMessage.data);
};
