import { Fragment } from 'react'
import type { RunKind, Step } from '../api'
import { AGENTS, agentStates } from '../agents'

const STATE_TEXT = { waiting: 'Väntar', working: 'Jobbar…', done: 'Klar', failed: 'Misslyckades' }

// Agenterna i rad, med den som jobbar just nu markerad.
export default function Pipeline({ kind, steps }: { kind: RunKind; steps: Step[] }) {
  const states = agentStates(kind, steps)
  const lastHandoff = [...steps].reverse().find(s => s.kind === 'handoff')
  return (
    <div className="pipeline">
      {AGENTS[kind].map((agent, i) => (
        <Fragment key={agent.name}>
          {i > 0 && <span className={`arrow ${lastHandoff?.toAgent === agent.name && states[agent.name] === 'working' ? 'active' : ''}`}>→</span>}
          <div className={`agent ${states[agent.name]}`}>
            <span className="icon">{agent.icon}</span>
            <b>{agent.name}</b>
            <span className="meta">{agent.label}</span>
            <span className="state">{STATE_TEXT[states[agent.name]]}</span>
          </div>
        </Fragment>
      ))}
    </div>
  )
}
