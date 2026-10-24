import type { Watch } from '../api'

interface Props {
  watches: Watch[]
  onOpen: (runId: string) => void
  onRun: (w: Watch) => Promise<void>
  onToggle: (w: Watch) => Promise<void>
  onDelete: (w: Watch) => Promise<void>
}

const when = (iso: string) => new Date(iso).toLocaleString('sv-SE', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

// Sparade sökningar som körs varje morgon klockan 7.
export default function WatchList({ watches, onOpen, onRun, onToggle, onDelete }: Props) {
  if (watches.length === 0) return null
  return (
    <div className="card">
      <h3>Bevakningar</h3>
      <p className="muted">Körs automatiskt varje morgon från klockan 7 när JobbAgent är igång.</p>
      <ul className="watches">
        {watches.map(w => (
          <li key={w.id} className={w.enabled ? '' : 'paused'}>
            <div>
              <b>{w.title}</b> <span className="meta">· minst {w.minScore} %</span>
              <div className="meta">
                {w.lastRunAt ? `Senast ${when(w.lastRunAt)}` : 'Inte körd än'}
                {!w.enabled && ' · pausad'}
              </div>
            </div>
            {w.lastRunId && (
              <button className={w.newMatches > 0 ? '' : 'secondary'} onClick={() => onOpen(w.lastRunId!)}>
                {w.newMatches > 0 ? `${w.newMatches} nya` : 'Visa'}
              </button>
            )}
            <button className="secondary" onClick={() => onRun(w)}>Kör nu</button>
            <button className="secondary" onClick={() => onToggle(w)}>{w.enabled ? 'Pausa' : 'Starta'}</button>
            <button className="secondary danger-text" title="Ta bort bevakningen" onClick={() => onDelete(w)}>✕</button>
          </li>
        ))}
      </ul>
    </div>
  )
}
