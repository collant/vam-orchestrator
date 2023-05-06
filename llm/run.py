"""

Usage:
python client.py
"""
import argparse

import socket
from json_socket import recv_json, send_json
import torch
from transformers import AutoTokenizer, AutoModelForCausalLM, AutoModel

import chromadb
from chromadb.utils import embedding_functions


secret = "fast-92813dlkcvoiej5w98lxclpj239slk"
host = 'localhost'
port = 8000

# embedding_function = embedding_functions.SentenceTransformerEmbeddingFunction(model_name="all-MiniLM-L6-v2")
# embedding_function = embedding_functions.InstructorEmbeddingFunction(model_name="hkunlp/instructor-large", device="cuda")
embedding_function = embedding_functions.InstructorEmbeddingFunction(model_name="hkunlp/instructor-xl")#, device="cuda")

# setup Chroma in-memory, for easy prototyping. Can add persistence easily!
client = chromadb.Client()

def getItem(collectionName, items, query):
    print("query: " + query)
    #print(items)
    collection = client.get_or_create_collection(collectionName, embedding_function = embedding_function)
    count = collection.count()
    if count == 0:
        print("Adding items to a new collection")
        collection.add(
            documents=items, # we handle tokenization, embedding, and indexing automatically. You can skip that and add your own embeddings as well
            ids=items, # unique for each doc
        )
    results = collection.query(
        query_texts=[query],
        n_results=1,
    )
    return results["documents"][0][0]

print("LLM is going to run")


def load_model(model_name, device, num_gpus, load_8bit=False, debug=False):
    if device == "cpu":
        kwargs = {}
    elif device == "cuda":
        kwargs = {"torch_dtype": torch.float16}
        if load_8bit:
            if num_gpus != "auto" and int(num_gpus) != 1:
                print("8-bit weights are not supported on multiple GPUs. Revert to use one GPU.")
            kwargs.update({"load_in_8bit": True, "device_map": "auto"})
        else:
            if num_gpus == "auto":
                kwargs["device_map"] = "auto"
            else:
                num_gpus = int(num_gpus)
                if num_gpus != 1:
                    kwargs.update({
                        "device_map": "auto",
                        "max_memory": {i: "13GiB" for i in range(num_gpus)},
                    })
    else:
        raise ValueError(f"Invalid device: {device}")

    if "chatglm" in model_name:
        tokenizer = AutoTokenizer.from_pretrained(model_name, trust_remote_code=True)
        model = AutoModel.from_pretrained(model_name, trust_remote_code=True).half().cuda()
    else:
        tokenizer = AutoTokenizer.from_pretrained(model_name, use_fast=False)
        model = AutoModelForCausalLM.from_pretrained(model_name,
            low_cpu_mem_usage=True, **kwargs)

    # calling model.cuda() mess up weights if loading 8-bit weights
    if device == "cuda" and num_gpus == 1 and not load_8bit:
        model.to("cuda")
    elif device == "mps":
        model.to("mps")

    if debug:
        print(model)

    tokenizer.truncation_side='left'
    return model, tokenizer


@torch.inference_mode()
def generate_stream(model, tokenizer, params, device,
                    context_len=1450, stream_interval=4):
    prompt = params["prompt"]
    temperature = float(params.get("temperature", 1.0))
    max_new_tokens = int(params.get("max_new_tokens", 256))
    stop_str = params.get("stop", None)

    input_ids = tokenizer(prompt, truncation=True, max_length=context_len).input_ids
    output_ids = list(input_ids)

    input_len = len(input_ids)
    # print(f"input_len: {input_len}, prompt_len: {len(prompt)}")

    for i in range(max_new_tokens):
        if i == 0:
            out = model(
                torch.as_tensor([input_ids], device=device), use_cache=True)
            logits = out.logits
            past_key_values = out.past_key_values
        else:
            attention_mask = torch.ones(
                1, past_key_values[0][0].shape[-2] + 1, device=device)
            out = model(input_ids=torch.as_tensor([[token]], device=device),
                        use_cache=True,
                        attention_mask=attention_mask,
                        past_key_values=past_key_values)
            logits = out.logits
            past_key_values = out.past_key_values

        last_token_logits = logits[0][-1]

        if device == "mps":
            # Switch to CPU by avoiding some bugs in mps backend.
            last_token_logits = last_token_logits.float().to("cpu")

        # penalty_factor = 0.9
        # # Apply the repeat penalty
        # penalty_mask = torch.zeros_like(last_token_logits)
        # penalty_mask[output_ids] = 1
        # last_token_logits *= (1 - penalty_mask * (1 - penalty_factor))

        if temperature < 1e-4:
            token = int(torch.argmax(last_token_logits))
        else:
            probs = torch.softmax(last_token_logits / temperature, dim=-1)
            token = int(torch.multinomial(probs, num_samples=1))

        output_ids.append(token)

        if token == tokenizer.eos_token_id:
            stopped = True
        else:
            stopped = False

        if i % stream_interval == 0 or i == max_new_tokens - 1 or stopped:
            output = tokenizer.decode(output_ids[input_len:], skip_special_tokens=True)
            for str in stop_str:
                pos = output.find(str)
                if pos != -1:
                    output = output[:pos]
                    stopped = True
            # pos = output.find(".")
            # if pos != -1:
            #     output = output[:pos]
            #     stopped = True
            yield output

        if stopped:
            break

    del past_key_values


def main(args):
    model_name = args.model_name

    # Model
    model, tokenizer = load_model(args.model_name, args.device,
        args.num_gpus, args.load_8bit, args.debug)

    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client_socket.connect((host, port))

    def handle_json(llmConfig, stillInBuffer):
        # print(f"Received llmConfig: {llmConfig}")
        torch.cuda.empty_cache()
        prompt = llmConfig['prompt']
        
        stop = llmConfig['stop']
        stopAt = llmConfig['stopAt']
        limit = llmConfig['limit']
        
        bucket = llmConfig['bucket']

        if "choices" in bucket and bucket['choices'] == True and stillInBuffer:
            print("Avoiding choices")
            return
        
        if "type" in bucket and bucket['type'] == "action" and "actions" in llmConfig:
            secret = bucket['secret']
            try:
                item = getItem(secret, llmConfig['actions'], prompt)
                send_json(client_socket, {"message": item, "inProgress": False, "bucket": llmConfig['bucket']})
            except:
                print("error")
                print(llmConfig['actions'])
            return

        
        params = {
            "model": model_name,
            "prompt": prompt,
            "temperature": 0.7,
            "max_new_tokens": 256,
            "stop": stop,
        }

        message = ""
        for outputs in generate_stream(model, tokenizer, params, args.device):
            message = outputs.strip()
            if len(message) > limit:
                pointIndex = -1
                for str in stopAt:
                    pointIndex = message[limit:].find(str)
                    if pointIndex >= 0:
                        break
                if pointIndex >= 0:
                    message = message[:limit + pointIndex + 1]
                    break
            send_json(client_socket, {"message": message, "inProgress": True, "bucket": llmConfig['bucket']})
        send_json(client_socket, {"message": message, "inProgress": False, "bucket": llmConfig['bucket']})


    json_obj = {
        "type": "handshake",
        "payload": "llm",
        "secret": secret
    }
    send_json(client_socket, json_obj)

    recv_json(client_socket, handle_json)



if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-name", type=str, default="AlekseyKorshuk/vicuna-7b")
    parser.add_argument("--device", type=str, choices=["cpu", "cuda", "mps"], default="cuda")
    parser.add_argument("--num-gpus", type=str, default="1")
    parser.add_argument("--load-8bit", action="store_true",
        help="Use 8-bit quantization.")
    parser.add_argument("--conv-template", type=str, default="v1",
        help="Conversation prompt template.")
    parser.add_argument("--temperature", type=float, default=0.7)
    parser.add_argument("--max-new-tokens", type=int, default=256)
    parser.add_argument("--debug", action="store_true")
    args = parser.parse_args()
    main(args)
