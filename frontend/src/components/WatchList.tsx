import type { Watch } from '../api'
import { useLang } from '../i18n'

interface Props {
  watches: Watch[]
  onOpen: (runId: string) => void
  onRun: (w: Watch) => Promise<void>
  onToggle: (w: Watch) => Promise<void>
  onDelete: (w: Watch) => Promise<void>
}

const when = (iso: string, locale: string) => new Date(iso).toLocaleString(locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

// Sparade sökningar som körs varje morgon klockan 7.
export default function WatchList({ watches, onOpen, onRun, onToggle, onDelete }: Props) {
  const { t, locale } = useLang()
  if (watches.length === 0) return null
  return (
    <div className="card">
      <h3>{t('Bevakningar')}</h3>
      <p className="muted">{t('Körs automatiskt varje morgon från klockan 7 när JobbAgent är igång.')}</p>
      <ul className="watches">
        {watches.map(w => (
          <li key={w.id} className={w.enabled ? '' : 'paused'}>
            <div>
              <b>{w.title}</b> <span className="meta">· {t('minst {score} %', { score: w.minScore })}</span>
              <div className="meta">
                {w.lastRunAt ? t('Senast {date}', { date: when(w.lastRunAt, locale) }) : t('Inte körd än')}
                {!w.enabled && ' · ' + t('pausad')}
              </div>
            </div>
            {w.lastRunId && (
              <button className={w.newMatches > 0 ? '' : 'secondary'} onClick={() => onOpen(w.lastRunId!)}>
                {w.newMatches > 0 ? t('{n} nya', { n: w.newMatches }) : t('Visa')}
              </button>
            )}
            <button className="secondary" onClick={() => onRun(w)}>{t('Kör nu')}</button>
            <button className="secondary" onClick={() => onToggle(w)}>{w.enabled ? t('Pausa') : t('Starta')}</button>
            <button className="secondary danger-text" title={t('Ta bort bevakningen')} onClick={() => onDelete(w)}>✕</button>
          </li>
        ))}
      </ul>
    </div>
  )
}
