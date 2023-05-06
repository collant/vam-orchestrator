using UnityEngine;
using static UnityEngine.UI.Button;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System;
using System.Linq;
using SimpleJSON;
using System.Collections.Generic;
using UnityEngine.UI;

namespace TwinWin
{
public class Imposter : MVRScript
{

    protected TcpClient client;
    protected NetworkStream stream;

    string microphoneDeviceSystemDefault = "System Default";
    JSONStorableString _serverURL;

    string secret = "982lksjdfs823dsf98-239sdssd-329832";
    string host = "localhost";
    int browserPort = 8080;
    int orchestratorPort = 8000;

    string webPanelAtomID = "ImposterSubScene/Browser";

    public override void Init() {

        secret = Guid.NewGuid().ToString();
        Atom webPanel = GetAtomById(webPanelAtomID);
        VRWebBrowser vRWebBrowser = (VRWebBrowser)webPanel.GetStorableByID("BrowserGUI");
        vRWebBrowser.url = "";
        vRWebBrowser.url = "http://" + host + ":" + browserPort + "/?secret=" + secret;


        _serverURL = new JSONStorableString("ServerURL", host, (string serverURL)=> {
            connect();
        });
        RegisterString(_serverURL);
        CreateLabelInput("Server URL", _serverURL, false);


        UIDynamicButton uIDynamicReconnect = CreateButton("Reconnect");
        ButtonClickedEvent reconnectButtonClickedEvent = new ButtonClickedEvent();
        reconnectButtonClickedEvent.AddListener(()=> {
            connect();
        });
        uIDynamicReconnect.button.onClick = reconnectButtonClickedEvent;
        
        connect();

        List<string> microphoneDeviceNames = new List<string>{microphoneDeviceSystemDefault};
        foreach (string deviceName in Microphone.devices) {
            microphoneDeviceNames.Add(deviceName);
        }
        _microphoneDeviceChooser = new JSONStorableStringChooser("MicrophoneDevice", microphoneDeviceNames, microphoneDeviceSystemDefault, "Microphone Device");
        RegisterStringChooser(_microphoneDeviceChooser);
        CreatePopup(_microphoneDeviceChooser);

        List<string> colliderChoices = new List<string>(){ "p243", "p244", "p245", "p246", "p247", "p248", "p249", "p250", "p251", "p252", "p253"};

        _speakerIdChooser = new JSONStorableStringChooser("SpeakerID", colliderChoices, "p243", "Speaker ID");
        RegisterStringChooser(_speakerIdChooser);
        CreatePopup(_speakerIdChooser);

        
        _sendActions = new JSONStorableBool("Send Actions",  true);
        RegisterBool(_sendActions);
        CreateToggle(_sendActions);
        
        MotionAnimationMaster motionAnimationMaster = GetAtomById(webPanelAtomID).containingSubScene.motionAnimationMaster;
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
        
        CreateSpacer(true);

    }

    private IEnumerator handshake(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        // SuperController.LogMessage($"WaitAndPrint: {Time.time}");
                
        JSONNode jSONNode = JSON.Parse("{}");
        jSONNode.Add("type", "handshake");
        jSONNode.Add("payload", "vam");
        jSONNode.Add("secret", secret);
        JSONNode config = getConfig();
        jSONNode.Add("config", config);

        sendJSON(jSONNode.ToString());
    }

    private IEnumerator startRecordingWithCorouting(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        startRecording();
    }


    private IEnumerator triggerActionWithCoroutine(float waitTime, string actionName)
    {
        yield return new WaitForSeconds(waitTime);
        try {
            _actionLoader.trigger(actionName);
        } catch (Exception e) {
            SuperController.LogMessage($"Exception: {e.Message}");
        }
    }

    private void onMessageReceived(string message) {
        // SuperController.LogMessage($"message: {message}");
        JSONNode jSONNode = JSON.Parse(message);
        string transcript = jSONNode["text"];
        string actionName = jSONNode["action"];
        if (actionName != null && !actionName.Equals("")) {
            StartCoroutine(triggerActionWithCoroutine(0.1f, "IncludePhysical"));
            StartCoroutine(triggerActionWithCoroutine(0.1f, "DontIncludeAppearance"));
            StartCoroutine(triggerActionWithCoroutine(0.1f, actionName));
            return;
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
            if (headAudio == null) {
                headAudio = containingAtom.GetStorableByID("HeadAudioSource");
            }
            namedAudioClip = new NamedAudioClip
            {
                sourceClip = receivedAudioClip,
                displayName = "recorded"
            };
            StartCoroutine(playAudio(0.01f));
        }
    }

    JSONStorable headAudio;
    NamedAudioClip namedAudioClip;

    private IEnumerator playAudio(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        headAudio.CallAction("PlayNow", namedAudioClip);
    }

    ActionLoader _actionLoader;
    JSONArray _triggerList;
    private JSONStorableBool _pushToTalkToggle;
    private JSONStorableBool _sendActions;
    private JSONStorableString _triggersTextField;
    private JSONStorableBool _sendTriggers;
    private JSONStorableStringChooser _speakerIdChooser;
    private JSONStorableStringChooser _microphoneDeviceChooser;
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
        
        confingJSON["serverURL"] = _serverURL.val.ToString();
        confingJSON["speakerId"] = _speakerIdChooser.val.ToString();
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
            startTrigger.receiver = GetPluginStorableById(person, "Imposter");
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
        stream?.Close();
        client?.Close();
        client = new TcpClient();
        string remoteServer = _serverURL.val.ToString();
        client.BeginConnect(remoteServer, orchestratorPort, (IAsyncResult ar) => {
            if (client.Connected) {
                stream = client.GetStream();

                StartCoroutine(handshake(3));

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
            connect();
            return;
        }
        microphoneClip = Microphone.Start(getMicrophoneDeviceName(), false, recordingMaxSeconds, microphoneFrequency);
    }

    private void stopRecordingAndSend() {
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
        jSONNode.Add("secret", secret);

        sendJSON(jSONNode.ToString());
    }

    int recordingMaxSeconds = 60;
    

    byte[] buffer = new byte[0];
    bool streaming = false;
    int bodyLength = 0;
    private void ReceiveMessages()
    {
        while (true)
        {
            if (stream.DataAvailable)
            {
                try {
                    //SuperController.LogMessage($"Stream DataAvailable");
                    // Read the data from the stream
                    byte[] container = new byte[client.ReceiveBufferSize];
                    // SuperController.LogMessage("client.ReceiveBufferSize: " + container.Length);
                    int bytesRead = stream.Read(container, 0, container.Length);
                    byte[] data = new byte[bytesRead];
                    Array.Copy(container, 0, data, 0, bytesRead);//use circular reference instead
                    buffer = buffer.Concat(data).ToArray();
                    while (true) {
                        if (!streaming && buffer.Length >= 4) {
                            streaming = true;
                            byte[] headerBytes = buffer.Take(4).ToArray();
                            bodyLength = (headerBytes[0] << 24) | (headerBytes[1] << 16) | (headerBytes[2] << 8) | headerBytes[3];
                            byte[] newArray = new byte[buffer.Length - 4];
                            Array.Copy(buffer, 4, newArray, 0, newArray.Length);//use circular reference instead
                            buffer = newArray;
                        } else if (streaming && buffer.Length >= bodyLength) {
                            byte[] bytes = buffer.Take(bodyLength).ToArray();
                            byte[] newArray = new byte[buffer.Length - bodyLength];
                            Array.Copy(buffer, bodyLength, newArray, 0, newArray.Length);//use circular reference instead
                            buffer = newArray;
                            string message = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                            streaming = false;
                            // SuperController.LogMessage("Rest in buffer to read: " + buffer.Length);
                            onMessageReceived(message);
                        } else {
                            break;
                        }
                    }
                } catch (Exception e) {
                    SuperController.LogMessage($"Exception in Socket Stream: ${e.Message}");
                }
                
            }
        }
    }

    private void sendJSON(string jsonString) {
        if (!client.Connected) {
            connect();
        }
        // Send a message to the server
        byte[] data = Encoding.UTF8.GetBytes(jsonString);
        int bodyLength = data.Length;
        int uint32 = data.Length;//TODO check unsigned
        byte[] headerBytes = new byte[] { (byte)(uint32 >> 24), (byte)(uint32 >> 16), (byte)(uint32 >> 8), (byte)uint32 };
        
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(data, 0, data.Length);
    }

    private void OnDestroy() {
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

    public class UIDynamicLabelInput: UIDynamic {
		public Text label;
		public InputField input;
	}
    private GameObject ourLabelWithInputPrefab;
    // Create one-line text input with label
    public UIDynamicLabelInput CreateLabelInput(string label, JSONStorableString storable, bool rightSide) {
        if (ourLabelWithInputPrefab == null)
        {
            ourLabelWithInputPrefab = new GameObject("LabelInput");
            ourLabelWithInputPrefab.SetActive(false);
            RectTransform rt = ourLabelWithInputPrefab.AddComponent<RectTransform>();
            rt.anchorMax = new Vector2(0, 1);
            rt.anchorMin = new Vector2(0, 1);
            rt.offsetMax = new Vector2(535, -500);
            rt.offsetMin = new Vector2(10, -600);
            LayoutElement le = ourLabelWithInputPrefab.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minHeight = 45;
            le.minWidth = 350;
            le.preferredHeight = 45;
            le.preferredWidth = 500;

            RectTransform backgroundTransform = manager.configurableScrollablePopupPrefab.transform.Find("Background") as RectTransform;
            backgroundTransform = UnityEngine.Object.Instantiate(backgroundTransform, ourLabelWithInputPrefab.transform);
            backgroundTransform.name = "Background";
            backgroundTransform.anchorMax = new Vector2(1, 1);
            backgroundTransform.anchorMin = new Vector2(0, 0);
            backgroundTransform.offsetMax = new Vector2(0, 0);
            backgroundTransform.offsetMin = new Vector2(0, -10);

            RectTransform labelTransform = manager.configurableScrollablePopupPrefab.transform.Find("Button/Text") as RectTransform;;
            labelTransform = UnityEngine.Object.Instantiate(labelTransform, ourLabelWithInputPrefab.transform);
            labelTransform.name = "Text";
            labelTransform.anchorMax = new Vector2(0, 1);
            labelTransform.anchorMin = new Vector2(0, 0);
            labelTransform.offsetMax = new Vector2(155, -5);
            labelTransform.offsetMin = new Vector2(5, 0);
            Text labelText = labelTransform.GetComponent<Text>();
            labelText.text = "Name";
            labelText.color = Color.white;

            RectTransform inputTransform = manager.configurableTextFieldPrefab.transform as RectTransform;
            inputTransform = Instantiate(inputTransform, ourLabelWithInputPrefab.transform);
            inputTransform.anchorMax = new Vector2(1, 1);
            inputTransform.anchorMin = new Vector2(0, 0);
            inputTransform.offsetMax = new Vector2(-5, -5);
            inputTransform.offsetMin = new Vector2(160, -5);
            UIDynamicTextField textfield = inputTransform.GetComponent<UIDynamicTextField>();
            textfield.backgroundColor = Color.white;
            LayoutElement layout = textfield.GetComponent<LayoutElement>();
            layout.preferredHeight = layout.minHeight = 35;
            InputField inputfield = textfield.gameObject.AddComponent<InputField>();
            inputfield.textComponent = textfield.UItext;

            RectTransform textTransform = textfield.UItext.rectTransform;
            textTransform.anchorMax = new Vector2(1, 1);
            textTransform.anchorMin = new Vector2(0, 0);
            textTransform.offsetMax = new Vector2(-5, -5);
            textTransform.offsetMin = new Vector2(10, -5);

            Destroy(textfield);

            UIDynamicLabelInput uid =  ourLabelWithInputPrefab.AddComponent<UIDynamicLabelInput>();
            uid.label = labelText;
            uid.input = inputfield;
        }

        {
            Transform t = CreateUIElement(ourLabelWithInputPrefab.transform, rightSide);
            UIDynamicLabelInput uid = t.gameObject.GetComponent<UIDynamicLabelInput>();
            storable.inputField = uid.input;
            uid.label.text = label;
            t.gameObject.SetActive(true);
            return uid;
        }
    }
}
}
