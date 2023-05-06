import json
import struct

def recv_json(socket, callback):
    buffer = bytearray()
    header_size = struct.calcsize("!I")
    header_received = False
    body_length = 0

    while True:
        try:
            data = socket.recv(4096)
            if not data:
                break

            buffer.extend(data)

            while True:
                if not header_received and len(buffer) >= header_size:
                    body_length = struct.unpack("!I", buffer[:header_size])[0]
                    buffer = buffer[header_size:]
                    header_received = True

                if header_received and len(buffer) >= body_length:
                    json_bytes = buffer[:body_length]
                    buffer = buffer[body_length:]
                    try:
                        json_str = json_bytes.decode("utf-8")
                        json_obj = json.loads(json_str)
                        stillInBuffer = len(buffer) > 0
                        callback(json_obj, stillInBuffer)
                    except:
                        print("error in json_socket")

                    header_received = False

                else:
                    break
        except:
            print("Error")

def send_json(socket, json_obj):
    json_str = json.dumps(json_obj)
    json_bytes = json_str.encode("utf-8")
    header = struct.pack("!I", len(json_bytes))
    socket.sendall(header + json_bytes)
