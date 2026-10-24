import type { RunStatus, RunSummary } from '../api'
import { KIND_ICON } from '../agents'
import { msg, useLang } from '../i18n'
import Score from './Score'

const STATUS: Record<RunStatus, string> = { queued: msg('I kö'), running: msg('Pågår'), completed: msg('Klar'), failed: msg('Misslyckades') }

export default function RunList({ runs, selected, onSelect }: { runs: RunSummary[]; selected: string | null; onSelect: (id: string) => void }) {
  const { t } = useLang()
  if (runs.length === 0) return <p className="muted">{t('Inget här än. Sök jobb eller skriv ditt första brev.')}</p>
  return (
    <ul className="runs">
      {runs.map(run => (
        <li key={run.id}>
          <button className={run.id === selected ? 'active' : ''} onClick={() => onSelect(run.id)}>
            <span className="icon">{KIND_ICON[run.kind]}</span>
            <span className="title">
              {run.title}
              {run.company && <span className="meta"> · {run.company}</span>}
            </span>
            {run.status === 'completed' && run.matchScore != null
              ? <Score value={run.matchScore} />
              : <span className={`badge ${run.status}`}>{t(STATUS[run.status])}</span>}
          </button>
        </li>
      ))}
    </ul>
  )
}
