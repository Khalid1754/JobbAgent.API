import { Fragment } from 'react'
import type { RunKind, Step } from '../api'
import { AGENTS, agentStates } from '../agents'
import { msg, useLang } from '../i18n'

const STATE_TEXT = { waiting: msg('Väntar'), working: msg('Jobbar…'), done: msg('Klar'), failed: msg('Misslyckades') }

// Agenterna i rad, med den som jobbar just nu markerad.
export default function Pipeline({ kind, steps }: { kind: RunKind; steps: Step[] }) {
  const { t } = useLang()
  const states = agentStates(kind, steps)
  const lastHandoff = [...steps].reverse().find(s => s.kind === 'handoff')
  return (
    <div className="pipeline">
      {AGENTS[kind].map((agent, i) => (
        <Fragment key={agent.name}>
          {i > 0 && <span className={`arrow ${lastHandoff?.toAgent === agent.name && states[agent.name] === 'working' ? 'active' : ''}`}>→</span>}
          <div className={`agent ${states[agent.name]}`}>
            <span className="icon">{agent.icon}</span>
            <b>{t(agent.name)}</b>
            <span className="meta">{t(agent.label)}</span>
            <span className="state">{t(STATE_TEXT[states[agent.name]])}</span>
          </div>
        </Fragment>
      ))}
    </div>
  )
}
