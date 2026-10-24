import type { RunStatus, RunSummary } from '../api'
import Score from './Score'

const STATUS: Record<RunStatus, string> = { queued: 'I kö', running: 'Pågår', completed: 'Klar', failed: 'Misslyckades' }

export default function RunList({ runs, selected, onSelect }: { runs: RunSummary[]; selected: string | null; onSelect: (id: string) => void }) {
  if (runs.length === 0) return <p className="muted">Inget här än. Sök jobb eller skriv ditt första brev.</p>
  return (
    <ul className="runs">
      {runs.map(run => (
        <li key={run.id}>
          <button className={run.id === selected ? 'active' : ''} onClick={() => onSelect(run.id)}>
            <span className="icon">{run.kind === 'search' ? '🔎' : '✉️'}</span>
            <span className="title">
              {run.title}
              {run.company && <span className="meta"> · {run.company}</span>}
            </span>
            {run.status === 'completed' && run.matchScore != null
              ? <Score value={run.matchScore} />
              : <span className={`badge ${run.status}`}>{STATUS[run.status]}</span>}
          </button>
        </li>
      ))}
    </ul>
  )
}
