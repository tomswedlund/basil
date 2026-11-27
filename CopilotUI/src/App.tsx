import { useState, useEffect, useRef } from 'react'
import ReactMarkdown from 'react-markdown'
import './App.css'
import sidebarIcon from './sidebar-toggle.svg'
import gearIcon from './gear-icon.svg'
import basilIcon from './basil-icon-med.png'

const UserMessage = ({ content, idx, turnId, lastInTurn }) => (
  <div className={`message user${lastInTurn ? ' turn-end' : ''}`} data-turn={turnId || ''}>
    <div className="user-bubble">{content}</div>
  </div>
)

const AssistantMessage = ({ content, idx, turnId, lastInTurn }) => (
  <div className={`message assistant${lastInTurn ? ' turn-end' : ''}`} data-turn={turnId || ''}>
    <div className="assistant-bubble"><ReactMarkdown>{content}</ReactMarkdown></div>
  </div>
)

const ToolCallMessage = ({ toolName, description, arguments: args, status, result, idx, turnId, lastInTurn }) => {
  const [expanded, setExpanded] = useState(false)
  const isRunning = status === 'running'
  const headerLabel = description || 'Tool call'
  let prettyArgs = ''
  try { prettyArgs = JSON.stringify(JSON.parse(args || '{}'), null, 2) } catch { prettyArgs = args || '{}' }

  return (
    <div className={`message tool-call${lastInTurn ? ' turn-end' : ''}`} data-turn={turnId || ''}>
      <div className="tool-call-card">
        <div className="tool-call-header" onClick={() => setExpanded(!expanded)}>
          <span className="tool-call-toggle">{expanded ? '▼' : '▶'}</span>
          <span className="tool-call-name">{headerLabel}</span>
          {!expanded && isRunning && <span className="tool-call-spinner"></span>}
        </div>
        {expanded && (
          <div className="tool-call-body">
            <pre className="tool-call-args">{prettyArgs}</pre>
            {isRunning && <div className="tool-call-running">Running ...</div>}
            {status === 'complete' && result && <pre className="tool-result-output">{result}</pre>}
          </div>
        )}
      </div>
    </div>
  )
}

const ThinkingIndicator = () => (
  <div className="thinking-indicator">
    <span className="thinking-dot"></span>
    <span className="thinking-dot"></span>
    <span className="thinking-dot"></span>
  </div>
)

const ChatInput = ({ inputProps, onSend, thinking, sticky }) => (
  <div className={sticky ? 'input-area' : 'input-area-inline'}>
    {thinking && <ThinkingIndicator />}
    <div className="input-wrapper">
      <input 
        value={inputProps.value}
        onChange={inputProps.onChange}
        placeholder={inputProps.placeholder}
        onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); onSend?.(); } }}
      />
      <button onClick={onSend}>➤</button>
    </div>
  </div>
)

const Messages = ({ messages, inputProps, onSend, thinking }) => {
  // Determine if each message is the last in its turn (next message has a different turn or is a user message)
  const isLastInTurn = (i) => {
    if (i >= messages.length - 1) return true
    const cur = messages[i]
    const next = messages[i + 1]
    if (next.role === 'user') return true
    if (cur.turnId && next.turnId && cur.turnId !== next.turnId) return true
    return false
  }
  return (
  <>
    <div className="messages">
      {messages.map((m, i) => {
        const lastInTurn = isLastInTurn(i)
        if (m.role === 'user') return <UserMessage key={i} content={m.content} idx={i} turnId={m.turnId} lastInTurn={lastInTurn} />
        if (m.role === 'tool_call') return <ToolCallMessage key={i} toolName={m.toolName} description={m.description} arguments={m.arguments} status={m.status} result={m.result} idx={i} turnId={m.turnId} lastInTurn={lastInTurn} />
        return <AssistantMessage key={i} content={m.content} idx={i} turnId={m.turnId} lastInTurn={lastInTurn} />
      })}
    </div>
    <ChatInput inputProps={inputProps} onSend={onSend} thinking={thinking} sticky />
  </>
  )
}

const EmptyState = ({ inputProps, onSend, thinking }) => (
  <div className="empty-state">
    <div className="empty-state-content">
      <h1 className="empty-state-title">Start, you vicious bastard!</h1>
      <ChatInput inputProps={inputProps} onSend={onSend} thinking={thinking} sticky={false} />
    </div>
  </div>
)

const SidebarHeader = ({ sidebarCollapsed, onToggleSidebar, isMobile, onCloseMobile, onOpenMobile }) => (
  <div className="sidebar-header">
    {!sidebarCollapsed && (
      <div className="sidebar-header-content">
        <img src={basilIcon} alt="Basil" className="basil-icon" />
        <span className="basil-text">Basil</span>
      </div>
    )}
    <div className="sidebar-icons">
      {sidebarCollapsed && (
        <button className="sidebar-icon expand-icon" onClick={() => {
          if (isMobile && onOpenMobile) onOpenMobile();
          else onToggleSidebar();
        }}>
          <img src={sidebarIcon} alt="Expand" className="sidebar-svg" />
        </button>
      )}
      {!sidebarCollapsed && (
        <>
          <button className="sidebar-icon gear-icon" onClick={() => console.log('Settings')}>
            <img src={gearIcon} alt="Settings" className="sidebar-svg" />
          </button>
          <button className="sidebar-icon collapse-icon" onClick={() => {
            if (isMobile && onCloseMobile) onCloseMobile();
            else onToggleSidebar();
          }}>
            <img src={sidebarIcon} alt="Collapse" className="sidebar-svg" />
          </button>
        </>
      )}
    </div>
  </div>
)

const CollapsibleSection = ({ title, children, isCollapsed, onToggle, showNewButton, onNew }) => (
  <div className="collapsible-section">
    <div className="section-header">
      <button className="collapse-btn" onClick={onToggle}>
        {isCollapsed ? '▶' : '▼'}
      </button>
      <h3>{title}</h3>
      {showNewButton && (
        <button className="new-chat-btn" onClick={onNew}>
          +
        </button>
      )}
    </div>
    {!isCollapsed && <div className="section-content">{children}</div>}
  </div>
)

const ChatHeader = ({ title, onToggleSidebar, isMobile }) => (
  <header className="chat-header">
    {isMobile && (
      <button className="mobile-menu-btn" onClick={onToggleSidebar}>
        <img src={sidebarIcon} alt="Menu" className="sidebar-svg" />
      </button>
    )}
    <div className="chat-header-title">{title}</div>
  </header>
)

const SessionItem = ({ name, isActive, status = 'idle', onClick, onContextMenu, renaming, renameValue, onRenameChange, onRenameCommit, onRenameCancel }) => {
  const statusColor = {
    idle: '#21262d',
    working: '#f1c40f',
    error: '#e74c3c',
    completed: '#2ecc71'
  }[status]

  return (
    <div
      className={`session-item ${isActive ? 'active' : ''}`}
      onClick={onClick}
      onContextMenu={onContextMenu}
    >
      <span className="status-indicator" style={{ backgroundColor: statusColor }}></span>
      {renaming ? (
        <input
          className="rename-input"
          autoFocus
          value={renameValue}
          onChange={e => onRenameChange(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') { e.preventDefault(); onRenameCommit() }
            if (e.key === 'Escape') { e.preventDefault(); onRenameCancel() }
          }}
          onBlur={onRenameCommit}
          onClick={e => e.stopPropagation()}
        />
      ) : name}
    </div>
  )
}

function App() {
  // force reload
  const [input, setInput] = useState('')
  const [sessions, setSessions] = useState<{id:string, title:string, status:string}[]>([])
  const [selectedSessionId, setSelectedSessionId] = useState<string>('')
  const [messagesMap, setMessagesMap] = useState<Record<string, {role:string, content:string, turnId?:string, toolCallId?:string, toolName?:string, description?:string, arguments?:string, status?:string, result?:string}[]>>({})
  // Temp session ids that exist only locally until the first prompt creates them on the backend.
  const tempIdsRef = useRef<Set<string>>(new Set())
  const [contextMenu, setContextMenu] = useState<{x:number, y:number, sessionId:string} | null>(null)
  const [renamingId, setRenamingId] = useState<string | null>(null)
  const [renameValue, setRenameValue] = useState('')
  const [isThinking, setIsThinking] = useState(false)
  const [chatCollapsed, setChatCollapsed] = useState(false)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [sidebarVisible, setSidebarVisible] = useState(true)
  const [debugMode, setDebugMode] = useState(false)
  const [debugComponent, setDebugComponent] = useState('')
  const [isMobile, setIsMobile] = useState(window.innerWidth <= 768)
  
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    setDebugMode(params.get('debug') === '1')
  }, [])

  useEffect(() => {
    // Apply red border to all instances of the selected component class
    const styleId = 'debug-highlight-style'
    let el = document.getElementById(styleId)
    if (el) el.remove()
    if (debugComponent) {
      el = document.createElement('style')
      el.id = styleId
      // debugComponent may be pipe-separated CSS classes (e.g. "input-area|input-wrapper")
      const selectors = debugComponent.split('|').map(c => `.${c.trim()}`).join(', ')
      el.textContent = `${selectors} { outline: 2px solid red !important; outline-offset: -1px; }`
      document.head.appendChild(el)
    }
  }, [debugComponent])

  useEffect(() => {
    const fetchSessions = async () => {
      try {
        const res = await fetch('/api/v1/sessions')
        if (res.ok) {
          const list: {id:string, title:string}[] = await res.json()
          const sess = list.map(s => ({ id: s.id, title: s.title || s.id, status: 'idle' as const }))
          setSessions(sess)
          if (sess.length > 0 && !selectedSessionId) {
            const first = sess[0].id
            setSelectedSessionId(first)
            loadSessionMessages(first)
          }
        }
      } catch (e) {
        console.error('Failed to fetch sessions', e)
      }
    }
    fetchSessions()
  }, [])
  
  useEffect(() => {
    const handleResize = () => {
      const mobile = window.innerWidth <= 768
      setIsMobile(mobile)
      if (mobile) {
        setSidebarVisible(false)
        setSidebarCollapsed(false)
      } else {
        setSidebarVisible(true)
      }
    }
    handleResize()
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [])
  
  const loadSessionMessages = async (sessionId) => {
    if (tempIdsRef.current.has(sessionId)) return
    try {
      const res = await fetch(`/api/v1/sessions/${sessionId}/messages`)
      if (res.ok) {
        const msgs: {role:string, content:string, toolCallId?:string, toolName?:string, description?:string, arguments?:string, status?:string, result?:string}[] = await res.json()
        setMessagesMap(m => ({ ...m, [sessionId]: msgs }))
      }
    } catch (e) {
      console.error('Failed to load session messages', e)
    }
  }

  const handleSessionClick = (sessionId) => {
    setSelectedSessionId(sessionId)
    setSessions(prev => prev.map(s => 
      s.id === sessionId ? { ...s, status: 'idle' } : s
    ))
    if (!messagesMap[sessionId]) {
      loadSessionMessages(sessionId)
    }
  }

  const refreshSessions = async () => {
    try {
      const res = await fetch('/api/v1/sessions')
      if (res.ok) {
        const list: {id:string, title:string}[] = await res.json()
        setSessions(list.map(s => ({ id: s.id, title: s.title || s.id, status: 'idle' as const })))
      }
    } catch (e) {
      console.error('Failed to fetch sessions', e)
    }
  }

  const handleContextMenu = (e, sessionId) => {
    e.preventDefault()
    e.stopPropagation()
    setContextMenu({ x: e.clientX, y: e.clientY, sessionId })
  }

  const closeContextMenu = () => setContextMenu(null)

  const startRename = (sessionId) => {
    const s = sessions.find(s => s.id === sessionId)
    setRenamingId(sessionId)
    setRenameValue(s?.title ?? '')
    closeContextMenu()
  }

  const commitRename = async () => {
    const id = renamingId
    const value = renameValue.trim()
    setRenamingId(null)
    if (!id || !value) return
    if (tempIdsRef.current.has(id)) {
      setSessions(prev => prev.map(s => s.id === id ? { ...s, title: value } : s))
      return
    }
    try {
      const res = await fetch(`/api/v1/sessions/${id}/title`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title: value })
      })
      if (res.ok) {
        const info = await res.json()
        setSessions(prev => prev.map(s => s.id === id ? { ...s, title: info.title || value } : s))
      }
    } catch (e) {
      console.error(e)
    }
  }

  const handleDelete = async (sessionId) => {
    closeContextMenu()
    if (!window.confirm('Delete this session?')) return
    if (tempIdsRef.current.has(sessionId)) {
      tempIdsRef.current.delete(sessionId)
      setSessions(prev => prev.filter(s => s.id !== sessionId))
      setMessagesMap(m => { const n = { ...m }; delete n[sessionId]; return n })
      if (selectedSessionId === sessionId) setSelectedSessionId('')
      return
    }
    try {
      const res = await fetch(`/api/v1/sessions/${sessionId}`, { method: 'DELETE' })
      if (res.ok || res.status === 204) {
        setSessions(prev => prev.filter(s => s.id !== sessionId))
        setMessagesMap(m => { const n = { ...m }; delete n[sessionId]; return n })
        if (selectedSessionId === sessionId) setSelectedSessionId('')
      }
    } catch (e) {
      console.error(e)
    }
  }

  const handleNewSession = () => {
    const newId = `new_chat_${Date.now()}`
    tempIdsRef.current.add(newId)
    setSessions(prev => [{ id: newId, title: 'New chat', status: 'idle' }, ...prev])
    setSelectedSessionId(newId)
    setMessagesMap(m => ({ ...m, [newId]: [] }))
  }

  const handleSend = async () => {
    if (!input.trim()) return
    // If no session is selected, create a local temp one first (same as clicking +).
    if (!selectedSessionId) {
      const newId = `new_chat_${Date.now()}`
      tempIdsRef.current.add(newId)
      setSessions(prev => [{ id: newId, title: 'New chat', status: 'idle' }, ...prev])
      setMessagesMap(m => ({ ...m, [newId]: [] }))
      setSelectedSessionId(newId)
      // Continue with the temp session we just created.
      await sendPrompt(newId)
      return
    }
    await sendPrompt(selectedSessionId)
  }

  const sendPrompt = async (id) => {
    const isTemp = tempIdsRef.current.has(id)
    let sessionId = id
    const userMsg = { role: 'user', content: input.trim() }
    const updated = [...(messagesMap[id] ?? []), userMsg]
    setMessagesMap(m => ({ ...m, [id]: updated }))
    setInput('')
    // If this is a local-only temp session, create it on the backend now (with the first prompt).
    if (isTemp) {
      sessionId = `session_${Date.now()}`
      try {
        const createRes = await fetch('/api/v1/sessions', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ sessionId })
        })
        if (!createRes.ok) {
          const assistantMsg = { role: 'assistant', content: 'Failed to create session' }
          setMessagesMap(m => ({ ...m, [id]: [...(m[id] ?? []), assistantMsg] }))
          return
        }
        // Promote: move messages from temp id to real id, drop the temp entry.
        tempIdsRef.current.delete(id)
        const oldId = id
        setMessagesMap(m => {
          const msgs = m[oldId] ?? []
          const next = { ...m }
          delete next[oldId]
          next[sessionId] = msgs
          return next
        })
        setSessions(prev => prev.map(s => s.id === oldId ? { ...s, id: sessionId } : s))
        setSelectedSessionId(sessionId)
      } catch (e) {
        console.error(e)
        const assistantMsg = { role: 'assistant', content: 'Network error creating session' }
        setMessagesMap(m => ({ ...m, [id]: [...(m[id] ?? []), assistantMsg] }))
        return
      }
    }
    // Chat with the (now real) session via streaming SSE.
    try {
      const res = await fetch(`/api/v1/sessions/${sessionId}/chat/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ messages: updated.map(m => ({ role: m.role, content: m.content })) })
      })
      if (!res.ok || !res.body) {
        const assistantMsg = { role: 'assistant', content: 'Error contacting API' }
        setMessagesMap(m => ({ ...m, [sessionId]: [...(m[sessionId] ?? []), assistantMsg] }))
        return
      }
      const assistantIdx = updated.length
      let accumulated = ''
      const reader = res.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''
      let gotMessage = false
      let currentTurnId = ''
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        // Parse SSE events separated by \n\n
        const events = buffer.split('\n\n')
        buffer = events.pop() ?? ''
        for (const evt of events) {
          const lines = evt.split('\n')
          let eventType = ''
          let dataStr = ''
          for (const line of lines) {
            if (line.startsWith('event: ')) eventType = line.slice(7).trim()
            else if (line.startsWith('data: ')) dataStr = line.slice(6)
          }
          if (!dataStr || dataStr === '[DONE]') continue
          if (eventType === 'turn_start') {
            try {
              const data = JSON.parse(dataStr)
              currentTurnId = data.turnId ?? ''
            } catch {}
            setIsThinking(true)
          } else if (eventType === 'turn_end') {
            setIsThinking(false)
          } else if (eventType === 'chunk') {
            // Assistant text response
            try {
              const data = JSON.parse(dataStr)
              if (data.content != null && data.content.trim() !== '') {
                gotMessage = true
                const tid = currentTurnId
                setMessagesMap(m => {
                  const arr = [...(m[sessionId] ?? [])]
                  arr.push({ role: 'assistant', content: data.content, turnId: tid })
                  return { ...m, [sessionId]: arr }
                })
              }
            } catch {}
          } else if (eventType === 'tool_call') {
            // The LLM is requesting to run a tool
            try {
              const data = JSON.parse(dataStr)
              gotMessage = true
              const tid = currentTurnId
              setMessagesMap(m => {
                const arr = [...(m[sessionId] ?? [])]
                arr.push({
                  role: 'tool_call',
                  content: '',
                  toolCallId: data.toolCallId,
                  toolName: data.toolName,
                  description: data.description,
                  arguments: data.arguments,
                  status: 'running',
                  turnId: tid
                })
                return { ...m, [sessionId]: arr }
              })
            } catch {}
          } else if (eventType === 'tool_result' || eventType === 'tool_execution') {
            // Tool result — find the matching tool_call by toolCallId and mark it complete
            try {
              const data = JSON.parse(dataStr)
              const toolCallId = data.toolCallId
              const resultContent = data.content || data.toolResult?.sessionLog || data.toolResult?.textResultForLlm || ''
              setMessagesMap(m => {
                const arr = [...(m[sessionId] ?? [])]
                const tcIdx = arr.findIndex(msg => msg.toolCallId === toolCallId && msg.role === 'tool_call')
                if (tcIdx >= 0) {
                  arr[tcIdx] = { ...arr[tcIdx], status: 'complete', result: resultContent }
                }
                return { ...m, [sessionId]: arr }
              })
            } catch {}
          } else if (eventType === 'message') {
            // Legacy: single final message (replaces the last assistant message)
            try {
              const data = JSON.parse(dataStr)
              if (data.content != null) {
                accumulated = data.content
                gotMessage = true
                const tid = currentTurnId
                setMessagesMap(m => {
                  const arr = [...(m[sessionId] ?? [])]
                  arr[assistantIdx] = { role: 'assistant', content: accumulated, turnId: tid }
                  return { ...m, [sessionId]: arr }
                })
              }
            } catch {}
          } else {
            // Delta event (no event: prefix)
            try {
              const data = JSON.parse(dataStr)
              if (data.delta != null) {
                accumulated += data.delta
                setMessagesMap(m => {
                  const arr = [...(m[sessionId] ?? [])]
                  arr[assistantIdx] = { role: 'assistant', content: accumulated }
                  return { ...m, [sessionId]: arr }
                })
              }
            } catch {}
          }
        }
      }
      setIsThinking(false)
      if (!gotMessage && !accumulated) {
        setMessagesMap(m => ({ ...m, [sessionId]: [...(m[sessionId] ?? []), { role: 'assistant', content: '(no response)' }] }))
      }
      // Copilot materializes the session + summary on first prompt; re-fetch to reflect it.
      refreshSessions()
    } catch (e) {
      console.error(e)
      setIsThinking(false)
      const assistantMsg = { role: 'assistant', content: 'Network error' }
      setMessagesMap(m => ({ ...m, [sessionId]: [...(m[sessionId] ?? []), assistantMsg] }))
    }
  }
  
  const messages = messagesMap[selectedSessionId] ?? []
  const isEmptyState = !selectedSessionId || (tempIdsRef.current.has(selectedSessionId) && messages.length === 0)

  return (
    <>
    {debugMode && (
      <div className="debug-header">
        <select className="debug-select" value={debugComponent} onChange={e => setDebugComponent(e.target.value)}>
          <option value="">— Select component to highlight —</option>
          <option value="message user|message assistant|message tool-call">UserMessage / AssistantMessage / ToolCallMessage</option>
          <option value="tool-call-card">ToolCallMessage (card)</option>
          <option value="tool-call-header">ToolCallMessage (header)</option>
          <option value="tool-call-body">ToolCallMessage (body)</option>
          <option value="thinking-indicator">ThinkingIndicator</option>
          <option value="input-area|input-wrapper">ChatInput</option>
          <option value="messages">Messages</option>
          <option value="empty-state|empty-state-content">EmptyState</option>
          <option value="sidebar|sidebar-header|sidebar-icons">SidebarHeader</option>
          <option value="collapsible-section|section-header|section-content">CollapsibleSection</option>
          <option value="chat-header|chat-header-title">ChatHeader</option>
          <option value="session-item">SessionItem</option>
          <option value="chat-area">App (chat-area)</option>
        </select>
        {debugComponent && <span className="debug-label">Highlighting: {debugComponent.split('|').map(c => `.${c}`).join(' / ')}</span>}
      </div>
    )}
    <div className={`copilot-app ${debugMode ? 'debug-mode' : ''}`}>
      <aside className={`sidebar ${sidebarCollapsed ? 'collapsed' : ''} ${isMobile && sidebarVisible ? 'visible' : ''}`}>
        <SidebarHeader 
          sidebarCollapsed={sidebarCollapsed} 
          onToggleSidebar={() => setSidebarCollapsed(!sidebarCollapsed)} 
          isMobile={isMobile}
          onCloseMobile={() => setSidebarVisible(false)}
          onOpenMobile={() => {
            setSidebarVisible(true);
            setSidebarCollapsed(false);
          }}
        />
        {!sidebarCollapsed && (
          <CollapsibleSection 
            title="CHAT" 
            isCollapsed={chatCollapsed}
            onToggle={() => setChatCollapsed(!chatCollapsed)}
            showNewButton={true}
            onNew={handleNewSession}
          >
            <div className="sessions-list">
              {sessions.map(s => (
                <SessionItem 
                  key={s.id}
                  name={s.title}
                  isActive={selectedSessionId === s.id}
                  status={s.status}
                  onClick={() => handleSessionClick(s.id)}
                  onContextMenu={(e) => handleContextMenu(e, s.id)}
                  renaming={renamingId === s.id}
                  renameValue={renameValue}
                  onRenameChange={setRenameValue}
                  onRenameCommit={commitRename}
                  onRenameCancel={() => setRenamingId(null)}
                />
              ))}
            </div>
          </CollapsibleSection>
        )}
      </aside>
      
      <main 
        className="chat-area"
        onClick={() => {
          if (isMobile && sidebarVisible) {
            setSidebarVisible(false);
          }
        }}
      >
        <ChatHeader title={sessions.find(s => s.id === selectedSessionId)?.title || selectedSessionId || 'No session'} onToggleSidebar={() => setSidebarVisible(!sidebarVisible)} isMobile={isMobile} />
        {isEmptyState ? (
          <EmptyState
            inputProps={{
              value: input,
              onChange: e => setInput(e.target.value),
              placeholder: 'Are you quite finished...'
            }}
            onSend={handleSend}
            thinking={isThinking}
          />
        ) : (
          <Messages 
            messages={messages}
            inputProps={{
              value: input,
              onChange: e => setInput(e.target.value),
              placeholder: 'Are you quite finished...'
            }}
            onSend={handleSend}
            thinking={isThinking}
          />
        )}
      </main>

      {contextMenu && (
        <>
          <div className="context-menu-overlay" onClick={closeContextMenu} onContextMenu={(e) => { e.preventDefault(); closeContextMenu() }} />
          <div className="context-menu" style={{ left: contextMenu.x, top: contextMenu.y }}>
            <button className="context-menu-item" onClick={() => startRename(contextMenu.sessionId)}>Rename</button>
            <button className="context-menu-item danger" onClick={() => handleDelete(contextMenu.sessionId)}>Delete</button>
          </div>
        </>
      )}
    </div>
    </>
  )
}

export default App
