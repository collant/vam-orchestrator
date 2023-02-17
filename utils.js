
let streaming = false;
let buffer = [];
let headerLength = 0;
const receiveJSONs = (socket, onJSON) => {
    socket.on('data', (data) => {
        buffer.push(...data);
        if (!streaming) {
            var headerBytes = buffer.slice(0, 4);
            buffer = buffer.slice(4);
            headerLength = byteArrayToInt(headerBytes);
            streaming = true;
        }
        if (buffer.length >= headerLength) {
            var bytes = buffer.slice(0, headerLength);
            buffer = buffer.slice(headerLength);
            var json = JSON.parse(Buffer.from(bytes).toString());
            onJSON(json);
            streaming = false;
        } else {
            // console.log({bufferLength: buffer.length})
        }
    });
}

const resetBuffer = () => {
    streaming = false;
    buffer = [];
}

const byteArrayToInt = (byteArray) => (byteArray[0] << 24) | (byteArray[1] << 16) | (byteArray[2] << 8) | byteArray[3];
const intToBytes = (uint32) => [uint32 >>> 24, (uint32 >> 16) & 0xFF, (uint32 >> 8) & 0xFF, uint32 & 0xFF];


const sendJSON = (jsonString, socket) => {
    var bytes = Buffer.from(jsonString);
    var length = bytes.length;
    var headerBytes = intToBytes(length);
    socket.write(Buffer.from(headerBytes));
    socket.write(bytes);
}



export {receiveJSONs, sendJSON, resetBuffer};