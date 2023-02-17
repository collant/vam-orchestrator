using UnityEngine;
using static UnityEngine.UI.Button;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System;
using System.Linq;
using SimpleJSON;
using System.Collections.Generic;

namespace TwinWin
{
public class Imposter : MVRScript
{

    protected TcpClient client;
    protected NetworkStream stream;

    string defaultPrompt = "The beauty and the beast";
    string microphoneDeviceSystemDefault = "System Default";
    public override void Init()
    {
        connect();

        List<string> microphoneDeviceNames = new List<string>{microphoneDeviceSystemDefault};
        foreach (string deviceName in Microphone.devices) {
            microphoneDeviceNames.Add(deviceName);
        }
        _microphoneDeviceChooser = new JSONStorableStringChooser("MicrophoneDevice", microphoneDeviceNames, microphoneDeviceSystemDefault, "Microphone Device");
        RegisterStringChooser(_microphoneDeviceChooser);
        CreatePopup(_microphoneDeviceChooser);

        List<String> colliderChoices = new List<string>(){ "p243", "p244", "p245", "p246", "p247", "p248", "p249", "p250", "p251", "p252", "p253"};

        _speakerIdChooser = new JSONStorableStringChooser("SpeakerID", colliderChoices, "p243", "Speaker ID");
        RegisterStringChooser(_speakerIdChooser);
        CreatePopup(_speakerIdChooser);

        _sendToLLMJSON = new JSONStorableBool("Send to LLM",  false, (bool active)=> {
            if (active) {
                _redirectToPromptToggle.SetVal(false);
            }
        });

        RegisterBool(_sendToLLMJSON);
        CreateToggle(_sendToLLMJSON);


        _newTokensLLM = new JSONStorableFloat("Tokens", 50, 1, 500, true, true);
        RegisterFloat(_newTokensLLM);
        CreateSlider(_newTokensLLM);

        _lastConversationsLLM = new JSONStorableFloat("Send last N conversations to LLM", 5, 1, 100, true, true);
        RegisterFloat(_lastConversationsLLM);
        CreateSlider(_lastConversationsLLM);

        
        UIDynamicButton uIDynamicButton1 = CreateButton("Reconnect");
        ButtonClickedEvent connectButtonClickedEvent = new ButtonClickedEvent();
        connectButtonClickedEvent.AddListener(()=> {
            connect();
        });
        uIDynamicButton1.button.onClick = connectButtonClickedEvent;
        
        _sendActions = new JSONStorableBool("Send Actions",  true);
        RegisterBool(_sendActions);
        CreateToggle(_sendActions);
        MotionAnimationMaster motionAnimationMaster = (MotionAnimationMaster) GetAtomById("CoreControl").GetStorableByID("MotionAnimationMaster");
        _actionLoader = new ActionLoader(motionAnimationMaster);
        List<string> allActions = _actionLoader.getActionNames();
        string listOfActions = "Available actions:\n";

        if (allActions.Count > 0) {
            listOfActions += allActions.Aggregate((acc, x) => acc + "\n" + x);
        }

        JSONStorableString jSONStorable = new JSONStorableString("actionsTextField", listOfActions);
        CreateTextField(jSONStorable);

        
        _sendTriggers = new JSONStorableBool("Send Triggers",  true, (bool active)=> {
            if (!active) {
                clearAllTriggers();
            } else {
                addAllTriggers();
            }
        });
        RegisterBool(_sendTriggers);
        CreateToggle(_sendTriggers);

        _triggersTextField = new JSONStorableString("triggersTextField", "Available Triggers:");
        CreateTextField(_triggersTextField);

        _pushToTalkToggle = new JSONStorableBool("Push To Talk",  true);
        RegisterBool(_pushToTalkToggle);
        CreateToggle(_pushToTalkToggle, true);

        _echoBack = new JSONStorableBool("Echo back",  true);
        RegisterBool(_echoBack);
        CreateToggle(_echoBack, true);
        _recognizedTextTextField = new JSONStorableString("echoBackTextField", "Recognized text:");
        CreateTextField(_recognizedTextTextField, true);
        
        CreateSpacer(true);

        _redirectToPromptToggle = new JSONStorableBool("Redirect Voice To Story",  false, (bool active)=> {
            if (active) {
                _sendToLLMJSON.SetVal(false);
            }
        });
        RegisterBool(_redirectToPromptToggle);
        CreateToggle(_redirectToPromptToggle, true);

        _promptStorableString = new JSONStorableString("promptyTextField", "Story:");
        RegisterString(_promptStorableString);
        CreateTextField(_promptStorableString, true);

        promptJSONArray.Add(defaultPrompt);
        refreshPromptUI();
        UIDynamicButton removeLastLine = CreateButton("Remove last line in Story", true);
        ButtonClickedEvent removeLastLineButtonClickedEvent = new ButtonClickedEvent();
        removeLastLineButtonClickedEvent.AddListener(()=> {
            removePromptLastLine();
        });
        removeLastLine.button.onClick = removeLastLineButtonClickedEvent;

        _conversationHistory = new JSONStorableString("conversationHistoryTextField", "Conversation History:");
        CreateTextField(_conversationHistory, true);

        StartCoroutine(getConfigWithCorouting(3));
    }

    private IEnumerator getConfigWithCorouting(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        // SuperController.LogMessage($"WaitAndPrint: {Time.time}");
        getConfig();
    }

    private IEnumerator startRecordingWithCorouting(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        startRecording();
    }
    JSONArray promptJSONArray = new JSONArray();

    private void removePromptLastLine() {
        if (promptJSONArray.Count > 0) {
            promptJSONArray.Remove(promptJSONArray.Count - 1);
        }
        refreshPromptUI();
    }
    private void refreshPromptUI() {
        string prompt = jSONArrayToUIString(promptJSONArray);
        _promptStorableString.SetVal(prompt);
    }

    private string jSONArrayToUIString(JSONArray jSONArray) {
        string resultString = "Story:";
        for (int i = 0; i < jSONArray.Count; i++) {
            string element = jSONArray[i];
            resultString+= $"\n{element}";
        }
        return resultString;
    }
    private void onMessageReceived(string message) {
        // SuperController.LogMessage($"message: {message}");
        JSONNode jSONNode = JSON.Parse(message);
        string transcript = jSONNode["text"];
        if (transcript != null) {
            _recognizedTextTextField.SetVal($"Recognized text:\n{transcript}");
            if (_redirectToPromptToggle.val) {
                promptJSONArray.Add(transcript);
                refreshPromptUI();
            }
        }
        string actionName = jSONNode["action"];
        if (actionName != null && !actionName.Equals("")) {
            try {
                _actionLoader.trigger(actionName);
            } catch (Exception e) {
                SuperController.LogMessage($"Exception: {e.Message}");
            }
        }

        var config = jSONNode["config"];
        if (config != null) {
            // SuperController.LogMessage($"config: {config}");
            JSONArray historyJSON = config["history"].AsArray;
            string historyString = "Conversation History:";
            if (historyJSON != null) {
                for (int i = 0; i < historyJSON.Count; i++) {
                    JSONNode historyElement = historyJSON[i];
                    string actor = historyElement["actor"];
                    string text = historyElement["text"];
                    historyString+= $"\n{actor}: {text}";
                }
            }
            _conversationHistory.SetVal(historyString);
        }
        string type = jSONNode["type"];
        if (type != null && type.Equals("wav")) {
            int sampleRate = jSONNode["sampleRate"].AsInt;
            int bitDepth = jSONNode["bitDepth"].AsInt;//TODO support 32 bitDepth
            int channels = jSONNode["channels"].AsInt;
            string payloadBase64 = jSONNode["payload"];
            byte[] byteArray = Convert.FromBase64String(payloadBase64);
            float[] floatArray = new float[byteArray.Length / 2]; // create an empty float array
            for (int i = 0; i < byteArray.Length; i += 2) {
                byte[] tempBytes = {byteArray[i], byteArray[i+1]}; // get the two bytes for each float
                short value = BitConverter.ToInt16(tempBytes, 0); // convert the bytes to a float and add it to the float array
                float valueFloat = value  / (float) 32768;
                floatArray[i / 2] = valueFloat;
            }
             
            AudioClip receivedAudioClip = AudioClip.Create("nodeJS", floatArray.Length, channels, sampleRate, false);
            receivedAudioClip.SetData(floatArray, 0);
            // receivedAudioClip.LoadAudioData();
            JSONStorable headAudio = containingAtom.GetStorableByID("HeadAudioSource");
            NamedAudioClip namedAudioClip = new NamedAudioClip
            {
                sourceClip = receivedAudioClip,
                displayName = "recorded"
            };
            headAudio.CallAction("PlayNow", namedAudioClip);
            // SuperController.LogMessage("Playing audio");
        }
    }

    ActionLoader _actionLoader;
    JSONArray _triggerList;
    private JSONStorableBool _sendToLLMJSON;
    private JSONStorableBool _pushToTalkToggle;
    private JSONStorableBool _echoBack;
    private JSONStorableBool _sendActions;
    private JSONStorableString _triggersTextField;
    private JSONStorableBool _sendTriggers;
    private JSONStorableFloat _newTokensLLM;
    private JSONStorableFloat _lastConversationsLLM;
    private JSONStorableStringChooser _speakerIdChooser;
    private JSONStorableStringChooser _microphoneDeviceChooser;
    private JSONStorableBool _redirectToPromptToggle;
    private JSONStorableString _promptStorableString;
    private JSONStorableString _conversationHistory;
    private JSONStorableString _recognizedTextTextField;
    private JSONNode getConfig() {
        JSONNode confingJSON = JSONNode.Parse("{}");
        
        confingJSON["sendActions"] = _sendActions.val ? "true" : "false";
        confingJSON["sendTriggers"] = _sendTriggers.val ? "true" : "false";

        if (_sendTriggers.val) {
            confingJSON["triggers"] = getTriggerListJSON();
        }

        if (_sendActions.val) {
            List<string> allActions = _actionLoader.getActionNames();
            JSONArray allActionsArray = new JSONArray();
            foreach(string actionName in allActions) {
                allActionsArray.Add(actionName);
            }
            confingJSON["actions"] = allActionsArray;
        }
        
        confingJSON["sendToLLM"] = _sendToLLMJSON.val ? "true" : "false";
        confingJSON["echoBack"] = _echoBack.val ? "true" : "false";
        confingJSON["newTokensLLM"] = _newTokensLLM.val.ToString();
        confingJSON["speakerId"] = _speakerIdChooser.val.ToString();
        confingJSON["lastConversationsLLM"] = _lastConversationsLLM.val.ToString();
        confingJSON["prompt"] = promptJSONArray;
        confingJSON["redirectToPrompt"] = _redirectToPromptToggle.val ? "true" : "false";
        confingJSON["pushToTalk"] = _pushToTalkToggle.val ? "true" : "false";

        // SuperController.LogMessage($"confingJSON: {confingJSON}");
        return confingJSON;
    }
    private JSONArray getTriggerListJSON() {
        if (_triggerList == null) {
            addAllTriggers();
        }
        return _triggerList;
    }

    List<String> getTriggerNames() {
        Atom person = GetContainingAtom();
        List<string> actionNames = person.GetStorableIDs();
        List<String> stringTriggerList = new List<String>();
        foreach (string triggerName in actionNames) {
            var storable = person.GetStorableByID(triggerName);
            if (storable.GetType() == typeof(CollisionTrigger)) {
                stringTriggerList.Add(triggerName);
            }
        }
        return stringTriggerList;
    }

    void addAllTriggers() {
        _triggerList = new JSONArray();

        List<String> stringTriggerList = getTriggerNames();
        foreach (string triggerName in stringTriggerList) {
            addTrigger(triggerName);
            _triggerList.Add(triggerName);
        }
        refreshTriggerNamesUI(stringTriggerList);
    }
    void refreshTriggerNamesUI(List<String> stringTriggerList) {
        string listOfTriggers = "Available Triggers:\n";
        if (stringTriggerList != null && stringTriggerList.Count > 0) {
            listOfTriggers+= stringTriggerList.Aggregate((acc, x) => acc + "\n" + x);
        }
        _triggersTextField.SetVal(listOfTriggers);
    }
    void clearAllTriggers() {
        _triggerList = new JSONArray();
        List<string> stringTriggerList = getTriggerNames();
        foreach (string triggerName in stringTriggerList) {
            clearTriggers(triggerName);
        }
        refreshTriggerNamesUI(null);
    }
    void clearTriggers(string triggerName) {
        Atom person = GetContainingAtom();
        CollisionTrigger trig = person.GetStorableByID(triggerName) as CollisionTrigger;
        JSONClass trigClass = trig.trigger.GetJSON();
        JSONArray trigArray = trigClass["startActions"].AsArray;
        for (int i = 0;i < trigArray.Count;i++) {
            if (trigArray[i]["name"].Value == triggerName) {
                trigArray.Remove(i);
            }
        }
        trig.trigger.RestoreFromJSON(trigClass);
    }
    float _timerStarted = 0;
    private void addTrigger(string triggerName) {
        clearTriggers(triggerName);

        string targetName = "Imposter_" + triggerName;

        JSONStorableAction manualTrigger = new JSONStorableAction(targetName, () =>{
            if (Time.time - _timerStarted > 5) {
                onTrigger(triggerName);
                _timerStarted = Time.time;
            }
        });
        RegisterAction(manualTrigger);

        Atom person = GetContainingAtom();
        CollisionTrigger trig = person.GetStorableByID(triggerName) as CollisionTrigger;
        if(trig != null) {
            trig.enabled = true;
            TriggerActionDiscrete startTrigger;
            startTrigger=trig.trigger.CreateDiscreteActionStartInternal();
            startTrigger.name = targetName;
            startTrigger.receiverAtom = person;
            startTrigger.receiver = GetPluginStorableById(person, "Imposter");;
            startTrigger.receiverTargetName = targetName;
        } else {
            SuperController.LogMessage($"Could not find trigger: {triggerName}");
        }
    }

    void onTrigger(string triggerName) {
        if (!_sendTriggers.val) {
            SuperController.LogMessage($"Lost Trigger: {triggerName}");
            return;
        }
        // SuperController.LogMessage($"Trigger: {triggerName}");

        JSONNode triggerMapping = JSON.Parse("{}");
        triggerMapping["DeeperVaginaTrigger"] = "Vagina very deep";
        triggerMapping["DeepVaginaTrigger"] = "Vagina deeply";
        triggerMapping["LabiaTrigger"] = "Labia";
        triggerMapping["LipTrigger"] = "Lips";
        triggerMapping["lNippleTrigger"] = "Left Nipple";
        triggerMapping["rNippleTrigger"] = "Right Nipple";
        triggerMapping["MouthTrigger"] = "Mouth";
        triggerMapping["ThroatTrigger"] = "Throat";
        triggerMapping["VaginaTrigger"] = "Vagina";
        
        JSONNode jSONNode = JSON.Parse("{}");
        jSONNode.Add("type", "trigger");
        string triggerSimpleName = triggerMapping[triggerName];
        if (triggerSimpleName != null) {
            jSONNode.Add("triggerName", triggerSimpleName);
        } else {
            jSONNode.Add("triggerName", triggerName);
        }

        JSONNode config = getConfig();
        jSONNode.Add("config", config);

        sendJSON(jSONNode.ToString());
    }

    // Function taken from VAMDeluxe's code :)
    static JSONStorable GetPluginStorableById(Atom atom, string id){
        string storableIdName = atom.GetStorableIDs().FirstOrDefault((string storeId) => {
            if (string.IsNullOrEmpty(storeId)){
                    return false;
            }
            return storeId.Contains(id);
        });
        if (storableIdName == null){
                return null;
        }
        return atom.GetStorableByID(storableIdName);
    }

    private void connect() {
        buffer = new byte[0];
        streaming = false;
        if (client != null && client.Connected) {
            // SuperController.LogMessage("Closing current connection");
            stream.Close();
            client.Close();
        }
        client = new TcpClient();
        client.BeginConnect("localhost", 8000, (IAsyncResult ar) => {
            if (client.Connected) {
                // SuperController.LogMessage("Connected");
                stream = client.GetStream();
                ReceiveMessages();
            } else {
                SuperController.LogMessage("Could not connect");
            }
        }, null);
    }

    private AudioClip microphoneClip;
    private int microphoneFrequency = 44100;
    bool leftTriggerDown = false;
    bool rightTriggerDown = false;
    bool mouseDown = false;
    void Update()
    {
        if (_pushToTalkToggle.val) {
            if (!mouseDown && Input.GetMouseButtonDown(1)) {
                StartCoroutine(startRecordingWithCorouting(0.0f));
                mouseDown = true;
            }
            if (mouseDown && Input.GetMouseButtonUp(1)) {
                stopRecordingAndSend();
                mouseDown = false;
            }
            float rightTriggerVal = GetRightTriggerVal();
            if (!rightTriggerDown && rightTriggerVal == 1.0f) {
                StartCoroutine(startRecordingWithCorouting(0.0f));
                rightTriggerDown = true;
            }
            if (rightTriggerDown && rightTriggerVal == 0.0f) {
                stopRecordingAndSend();
                rightTriggerDown = false;
            }
            float leftTriggerVal = GetLeftTriggerVal();
            if (!leftTriggerDown && leftTriggerVal == 1.0f) {
                StartCoroutine(startRecordingWithCorouting(0.0f));
                leftTriggerDown = true;
            }
            if (leftTriggerDown && leftTriggerVal == 0.0f) {
                stopRecordingAndSend();
                leftTriggerDown = false;
            }
        }
    }
    
    private string getMicrophoneDeviceName() {
        string name = _microphoneDeviceChooser.val;
        if (name != microphoneDeviceSystemDefault) {
            return name;
        }
        return null;
    }
    private void startRecording() {
        if (!client.Connected) {
            SuperController.LogError("Not connected");
        }
        microphoneClip = Microphone.Start(getMicrophoneDeviceName(), false, recordingMaxSeconds, microphoneFrequency);
    }

    private void stopRecordingAndSend() {
        if (!client.Connected) {
            SuperController.LogError("Not connected");
        }
        int position = Microphone.GetPosition(getMicrophoneDeviceName());
        Microphone.End(getMicrophoneDeviceName());

        var samples = new float[microphoneClip.samples * microphoneClip.channels];
        microphoneClip.GetData(samples, 0);

        byte[] bytesData = new byte[position * 2];
        int rescaleFactor = 32768; //to convert float to Int16

        for (int i = 0; i < position; i++) {
            short value = (short) (samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(value);
            byteArr.CopyTo(bytesData, i * 2);
        }

        string base64String = Convert.ToBase64String(bytesData);
        JSONNode jSONNode = JSON.Parse("{}");
        jSONNode.Add("type", "wav");
        jSONNode.Add("payload", base64String);
        JSONNode config = getConfig();
        jSONNode.Add("config", config);

        sendJSON(jSONNode.ToString());
    }

    int recordingMaxSeconds = 60;
    
    JSONNode inputInfoPersist = JSON.Parse("{}");

    byte[] buffer = new byte[0];
    bool streaming = false;
    int headerLength = 0;
    private void ReceiveMessages()
    {
        while (true)
        {
            if (stream.DataAvailable)
            {
                // Read the data from the stream
                byte[] container = new byte[client.ReceiveBufferSize];
                // SuperController.LogMessage("client.ReceiveBufferSize: " + container.Length);
                int bytesRead = stream.Read(container, 0, container.Length);
                byte[] data = new byte[bytesRead];
                Array.Copy(container, 0, data, 0, bytesRead);//use circular reference instead
                buffer = buffer.Concat(data).ToArray();
                if (!streaming) {
                    streaming = true;
                    byte[] headerBytes = buffer.Take(4).ToArray();
                    headerLength = (headerBytes[0] << 24) | (headerBytes[1] << 16) | (headerBytes[2] << 8) | headerBytes[3];
                    byte[] newArray = new byte[buffer.Length - 4];
                    Array.Copy(buffer, 4, newArray, 0, newArray.Length);//use circular reference instead
                    buffer = newArray;
                }
                if (buffer.Length >= headerLength) {
                    byte[] bytes = buffer.Take(headerLength).ToArray();
                    byte[] newArray = new byte[buffer.Length - headerLength];
                    Array.Copy(buffer, headerLength, newArray, 0, newArray.Length);//use circular reference instead
                    buffer = newArray;
                    string message = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                    streaming = false;
                    onMessageReceived(message);
                } else {
                    // SuperController.LogMessage("Chunking with buffer.length: " + buffer.Length);
                }
            }
        }
    }

    private void sendJSON(string jsonString)
    {
        if (!client.Connected) {
            SuperController.LogError("Not connected");
            return;
        }
        // Send a message to the server
        byte[] data = Encoding.UTF8.GetBytes(jsonString);
        int headerLength = data.Length;
        int uint32 = data.Length;//TODO check unsigned
        byte[] headerBytes = new byte[] { (byte)(uint32 >> 24), (byte)(uint32 >> 16), (byte)(uint32 >> 8), (byte)uint32 };
        
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(data, 0, data.Length);
    }

    private void OnDestroy()
    {
        Microphone.End(null);
        stream.Close();
        client.Close();
    }

    public class ActionLoader {
        private readonly MotionAnimationMaster _motionAnimationMaster;
        public ActionLoader(MotionAnimationMaster motionAnimationMaster) {
            _motionAnimationMaster = motionAnimationMaster;
        }
        public List<string> getActionNames() {
            List<string> actionNames = new List<string>();
            List<JSONClass> actionJSONs = getActionJSONs();
            foreach (JSONClass actionJSON in actionJSONs) {
                string actionName = actionJSON["name"];
                if (actionName != null && !actionName.Equals("")) {
                    actionNames.Add(actionName);
                }
            }
            
            return actionNames;
        }
        public List<JSONClass> getActionJSONs() {
            JSONArray triggersJSON = _motionAnimationMaster.GetJSON()["triggers"].AsArray;
            List<JSONClass> allActions = new List<JSONClass>();
            foreach (JSONNode triggerJSON in triggersJSON) {
                JSONArray startActions = triggerJSON["startActions"].AsArray;
                JSONArray transitionActions = triggerJSON["transitionActions"].AsArray;
                JSONArray endActions = triggerJSON["endActions"].AsArray;
                foreach (JSONNode actionJSON in startActions) {
                    allActions.Add(actionJSON.AsObject);
                }
                foreach (JSONNode actionJSON in transitionActions) {
                    allActions.Add(actionJSON.AsObject);
                }
                foreach (JSONNode actionJSON in endActions) {
                    allActions.Add(actionJSON.AsObject);
                }
            }
            return allActions;
        }
        public void trigger(string actionName) {
            List<JSONClass> actionJSONs = getActionJSONs();
            foreach (JSONNode actionJSON in actionJSONs) {
                string actionNameInList = actionJSON["name"];
                if (actionNameInList != null && actionNameInList.Equals(actionName)) {
                    // SuperController.LogMessage($"Triggering: {actionName}");
                    TriggerActionDiscrete triggerActionDiscrete = new TriggerActionDiscrete();
                    triggerActionDiscrete.RestoreFromJSON(actionJSON.AsObject);
                    triggerActionDiscrete.Trigger();
                }
            }
        }
    }
}
}