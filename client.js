//This is just used for debugging
import net from 'net';

import {receiveJSONs} from './utils.js'

const client = new net.Socket();

client.connect(8000, 'localhost', () => {
  console.log('connected to server');

  receiveJSONs(client, (json) => {
    console.log('received a json file', json.type)
  })

});

// client.on('data', (data) => {
//   // console.log(`received data: ${data.toString()}`);
//   // client.end();
// });

client.on('close', () => {
  console.log('connection closed');
});
