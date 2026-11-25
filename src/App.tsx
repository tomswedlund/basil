import { useState, useEffect } from 'react'
import './App.css'
import sidebarIcon from './sidebar-toggle.svg'
import gearIcon from './gear-icon.svg'
import basilIcon from './basil-icon-med.png'

const UserMessage = ({ content }) => (
  <div className="message user">
    <div className="message-content">{content}</div>
  </div>
)

const AssistantMessage = ({ content }) => (
  <div className="message assistant">
    <div className="message-content">{content}</div>
  </div>
)

const Messages = ({ messages, inputProps }) => (
  <div className="messages">
    <div className="messages-content">
      {messages.map((m, i) => 
        m.role === 'user' 
          ? <UserMessage key={i} content={m.content} />
          : <AssistantMessage key={i} content={m.content} />
      )}
    </div>
    <div className="input-area">
      <div className="input-wrapper">
        <input 
          value={inputProps.value}
          onChange={inputProps.onChange}
          placeholder={inputProps.placeholder}
        />
        <button>➤</button>
      </div>
    </div>
  </div>
)

const SidebarHeader = ({ sidebarCollapsed, onToggleSidebar, isMobile }) => (
  <div className="sidebar-header">
    {!sidebarCollapsed && (
      <div className="sidebar-header-content">
        <img src={basilIcon} alt="Basil" className="basil-icon" />
        <span className="basil-text">Basil</span>
      </div>
    )}
    <div className="sidebar-icons">
      {sidebarCollapsed && !isMobile && (
        <button className="sidebar-icon expand-icon" onClick={onToggleSidebar}>
          <img src={sidebarIcon} alt="Expand" className="sidebar-svg" />
        </button>
      )}
      {!sidebarCollapsed && (
        <>
          <button className="sidebar-icon gear-icon" onClick={() => console.log('Settings')}>
            <img src={gearIcon} alt="Settings" className="sidebar-svg" />
          </button>
          <button className="sidebar-icon collapse-icon" onClick={onToggleSidebar}>
            <img src={sidebarIcon} alt="Collapse" className="sidebar-svg" />
          </button>
        </>
      )}
    </div>
  </div>
)

const CollapsibleSection = ({ title, children, isCollapsed, onToggle, showNewButton }) => (
  <div className="collapsible-section">
    <div className="section-header">
      <button className="collapse-btn" onClick={onToggle}>
        {isCollapsed ? '▶' : '▼'}
      </button>
      <h3>{title}</h3>
      {showNewButton && (
        <button className="new-chat-btn" onClick={() => console.log('New chat')}>
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

const SessionItem = ({ name, isActive, status = 'idle', onClick }) => {
  const statusColor = {
    idle: '#21262d',
    working: '#f1c40f',
    error: '#e74c3c',
    completed: '#2ecc71'
  }[status]

  return (
    <div className={`session-item ${isActive ? 'active' : ''}`} onClick={onClick}>
      <span className="status-indicator" style={{ backgroundColor: statusColor }}></span>
      {name}
    </div>
  )
}

function App() {
  const [input, setInput] = useState('')
  const [sessions, setSessions] = useState([
    { id: 1, name: 'Current session (idle)', status: 'idle' },
    { id: 2, name: 'Session working (yellow)', status: 'working' },
    { id: 3, name: 'Session error (red)', status: 'error' },
    { id: 4, name: 'Session completed (green)', status: 'completed' }
  ])
  const [selectedSession, setSelectedSession] = useState('Current session (idle)')
  const [chatCollapsed, setChatCollapsed] = useState(false)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [sidebarVisible, setSidebarVisible] = useState(true)
  const [debugMode, setDebugMode] = useState(false)
  const [isMobile, setIsMobile] = useState(window.innerWidth <= 768)
  
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    setDebugMode(params.get('debug') === '1')
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
  
  const handleSessionClick = (sessionName) => {
    setSelectedSession(sessionName)
    setSessions(prev => prev.map(s => 
      s.name === sessionName ? { ...s, status: 'idle' } : s
    ))
  }
  
  const messages = Array.from({ length: 50 }, (_, i) => ({
    role: i % 2 === 0 ? 'assistant' : 'user',
    content: `Message ${i + 1}: This is a sample conversation message to demonstrate scrolling with many messages. The quick brown fox jumps over the lazy dog.`
  }))

  return (
    <div className={`copilot-app ${debugMode ? 'debug-mode' : ''}`}>
      <aside className={`sidebar ${sidebarCollapsed ? 'collapsed' : ''} ${isMobile && sidebarVisible ? 'visible' : ''}`}>
        <SidebarHeader sidebarCollapsed={sidebarCollapsed} onToggleSidebar={() => setSidebarCollapsed(!sidebarCollapsed)} isMobile={isMobile} />
        {!sidebarCollapsed && (
          <CollapsibleSection 
            title="CHAT" 
            isCollapsed={chatCollapsed}
            onToggle={() => setChatCollapsed(!chatCollapsed)}
            showNewButton={true}
          >
            <div className="sessions-list">
              {sessions.map(s => (
                <SessionItem 
                  key={s.id}
                  name={s.name}
                  isActive={selectedSession === s.name}
                  status={s.status}
                  onClick={() => handleSessionClick(s.name)}
                />
              ))}
            </div>
          </CollapsibleSection>
        )}
      </aside>
      
      <main className="chat-area">
        <ChatHeader title={selectedSession} onToggleSidebar={() => setSidebarVisible(!sidebarVisible)} isMobile={isMobile} />
        <Messages 
          messages={messages}
          inputProps={{
            value: input,
            onChange: e => setInput(e.target.value),
            placeholder: 'Are you quite finished...'
          }}
        />
      </main>
    </div>
  )
}

export default App
