import { useEffect, useReducer, useRef } from 'react';

const backgroundLineColor = '#015';
const userLineBackgroundColor = '#041'
const Button = ({onClick, text, color}) => {
    return <span
    style={{
        color: "white",
        backgroundColor: color,
        borderRadius: 8,
        paddingLeft: 16,
        paddingRight: 16,
        paddingTop: 16,
        paddingBottom: 16,
        margin: 8,
        cursor: 'pointer',
    }}
    onClick={onClick}
    >
        {text}
    </span>;

}

const TextInput = ({onType, text, send}) => {
    return <input
    style={{
        color: "white",
        backgroundColor: 'gray',
        borderRadius: 8,
        padding: 16,
        margin: 8,
        border: 'none',
        cursor: 'pointer',
        fontSize: 'large',
        display: 'flex',
        flexGrow: 1,
        fontFamily: 'Roboto',
        overflowWrap: 'break-word'
    }}
    onChange={(event)=> onType(event.target.value)}
    onKeyDown={(event)=> event.key === 'Enter' && send()}
    value={text}
    />
}
const TextArea = ({onType, text, send}) => {
    return <textarea
    style={{
        color: "white",
        backgroundColor: '#444',
        borderRadius: 8,
        padding: 16,
        border: 'none',
        cursor: 'pointer',
        fontSize: 'large',
        display: 'flex',
        flex: 1,
        fontFamily: 'Roboto',
        overflowWrap: 'break-word',
        height: 200
    }}
    onChange={(event)=> onType(event.target.value)}
    value={text}
/>
    
}

const SimpleLine = ({line}) => {
    return <div
        style={{
                display: line.me ? 'flex': 'inline-flex',
            }}
        >
            <div style={{flexGrow: 1}}></div>
            <div style={{
                color: 'white',
                padding: 8,
                marginTop: 8,
                marginBottom: 8,
                backgroundColor: line.me ? userLineBackgroundColor: backgroundLineColor,
                borderRadius: 8,
                borderBottomRightRadius: line.me ? 0 : 8,
                borderBottomLeftRadius: !line.me ? 0 : 8,
            }}>
            {line.message}

            </div>
    </div>
}

const AudioLine = ({line}) => {
    return <div style= {{
        color: 'white',
        padding: 8,
        marginTop: 8,
        marginBottom: 8,
        backgroundColor: backgroundLineColor,
        borderRadius: 8,
        display: 'inline-block',
        }}>
        <div>
            <SimpleLine line={line}/>
        </div>
        <audio controls
        src={"data:audio/wav;base64," + line.wav}
        // autoPlay
        />
    </div>
    
}

const MultiChoiceLine = ({line, onChoice}) => {
    return <div style= {{
        color: 'white',
        padding: 8,
        marginTop: 8,
        marginBottom: 8,
        backgroundColor: backgroundLineColor,
        borderRadius: 8,
        display: 'inline-block',
        }}>
        {
            line.choices.map((choice, i) => {
                return <div
                    style= {{
                        color: 'white',
                        padding: 8,
                        margin: 8,
                        backgroundColor: userLineBackgroundColor,
                        borderRadius: 8,
                        display: 'inline-block',
                        cursor: 'pointer'
                        }}
                    onClick={() => onChoice(choice)}
                    key={i}
                >
                    {i + 1}- {choice}
                </div>
            })
        }
    </div>
    
}

const SceneChoice = ({line, onChoice}) => {
    return <div style= {{
        color: 'white',
        padding: 8,
        marginTop: 8,
        marginBottom: 8,
        backgroundColor: backgroundLineColor,
        borderRadius: 8,
        display: 'inline-block',
        }}>
        {
            line.choices.map((choice, i) => {
                return <div
                    style= {{
                        color: 'white',
                        padding: 8,
                        margin: 8,
                        backgroundColor: userLineBackgroundColor,
                        borderRadius: 8,
                        display: 'inline-block',
                        cursor: 'pointer'
                        }}
                    onClick={() => onChoice(choice.content)}
                    key={i}
                >
                    {i + 1}- {choice.title}
                </div>
            })
        }
    </div>
    
}

const History = ({history, onChoice, onSceneChoice}) => {
    return history.map((action, i) => {
        if (action.type === 'wav') {
            return <div key={i}>
                <AudioLine line={action.payload}/>
            </div>
        } if (action.type === 'scene') {
            return <div key={i}>
                <SceneChoice line={action.payload} onChoice={onSceneChoice} />
            </div>
        } else if (action.type === 'multi_choice') {
            return <div key={i}>
                <MultiChoiceLine line={action.payload} onChoice={onChoice} />
            </div>
        } else {
            return <div key={i}>
                <SimpleLine line={action.payload}/>
            </div>
        }
        
    })

}

const initialHistory = [];
const initialContext = "A chat between Emily and Jack. Emily is a college girl, she is 18 years old, and she needs a lot of money.###Jack: Hey.###Emily: Who are you? we've never met before.###Jack: I am Jack###Emily:"
const Container = ({dispatcher, send}) => {
    const [state, dispatch] = useReducer((state, action) => reducer(state, action, {send}), {context: initialContext, history:  initialHistory, text: ''});
    dispatcher.dispatch = (message) => {
        dispatch({'type': 'received', payload: message})
    }
    const divRef = useRef(null);

    useEffect(() => {
        divRef.current.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }, [state.history]);

    return <div style={{
        backgroundColor: 'black',
        display: 'flex',
        flexDirection: 'column',
        height: '700px',
        }}>
        <div
            style={{padding: 8, display: 'block', flex: 1, overflow: 'auto', backgroundColor: 'black'}}
            
        >
            <div style={{display: 'flex'}}>
            <TextArea
                onType={(text)=> dispatch({'type': 'context', 'payload': text})} text={state.context}
                send={()=> dispatch({'type': 'context', 'payload': text})}
            />
            </div>
            
            <History
                history={state.history}
                onChoice={(choice)=> dispatch({'type': 'choice', 'payload': choice})}
                onSceneChoice={(scene)=> dispatch({'type': 'scene', 'payload': scene})}
            />
            <div ref={divRef} style={{height: '100px', flex: 1, display: 'block'}}></div>
        </div>
        <div style={{display: 'flex'}}>
            <TextInput
                onType={(text)=> dispatch({'type': 'typing', 'payload': text})} text={state.text}
                send={()=> dispatch({'type': 'send'})}
            />
            <Button text="Send" color={userLineBackgroundColor} onClick={()=> dispatch({'type': 'send'})}/>
            <Button text="Clear" color="#923" onClick={()=> dispatch({'type': 'clear'})}/>
            <Button text="Retry" color="#239" onClick={()=> dispatch({'type': 'retry'})}/>
        </div>
        </div>
}

const prepareStateToSend = (state) => {
    const history = state.history
    .map((item)=> {
        if (item.type === 'wav') {
            const newItem = {
                type: 'message',
                payload: {
                    message: item.payload.message
                }
            }
            return newItem
        }
        return item;
    })
    return {...state, history}
}
var start = window.performance.now()
function reducer(state, action, {send}) {
    if (action.type === 'send') {
        start = window.performance.now();
        let message = state.text;
        if (message.endsWith("\n")) {
            message.pop()
        }
        if (message === "") {
            // message = "Continue"
        }
        const newState = {
            ...state,
            history: [...state.history, {type: 'message', payload: {message: message, me: true}}],
            text: ''
        }
        send(prepareStateToSend(newState))
        return newState
    }
    
    if (action.type === 'retry') {
        start = window.performance.now();
        state.history.pop()
        const newState = {
            ...state,
            history: [...state.history]
        }
        send(prepareStateToSend(newState))
        return newState
    }
    if (action.type === 'choice') {
        start = window.performance.now();
        const newState = {
            ...state,
            history: [...state.history, {type: 'message', payload: {message: action.payload, me: true}}],
            text: '',
            choice: true,
        }
        send(prepareStateToSend(newState))
        return newState
    }
    if (action.type === 'scene') {
        start = window.performance.now();
        const newState = {
            ...state,
            choice: true,
            history: [state.history[0]],
            context: action.payload
        }
        send(prepareStateToSend(newState))
        return newState
    }
    if (action.type === 'received') {
        const lastHistory = state.history;
        if (lastHistory.length > 0 && lastHistory[lastHistory.length - 1].payload.inProgress) {
            lastHistory.pop();
        }
        if(action.payload && !action.payload.payload.inProgress) {
            var time = (window.performance.now() - start)/1000;
            var speed = action.payload.payload.message.length / time
            console.log(speed, time)
        }
        if (action.payload  && action.payload.type === 'wav') {
            // console.timeEnd("speech time")
        }
        const newState = {
            ...state,
            history: [...lastHistory, action.payload]
        }
        return newState
    }
    if (action.type === 'typing') {
        return {
            ...state,
            text: action.payload
        }
    }

    if (action.type === 'context') {
        return {
            ...state,
            context: action.payload
        }
    }

    if (action.type === 'clear') {
        const newState = {
            ...state,
            history: [state.history[0]],
            text: ''
        }
        send(prepareStateToSend(newState))
        return newState
    }

    throw Error('Unknown action.');
}

export default Container;