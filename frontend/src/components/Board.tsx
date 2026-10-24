import { useState, type DragEvent } from 'react'
import type { Application, Stage } from '../api'
import { STAGES, countByStage, nextUp } from '../board'
import { useLang } from '../i18n'
import ApplicationEditor from './ApplicationEditor'
import Score from './Score'

interface Props {
  applications: Application[]
  onUpdate: (a: Application) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onAdd: () => void
  onOpenLetter: (runId: string) => void
  onPrepareInterview: (a: Application) => Promise<void>
}


// Ansökningstavlan: dra korten mellan kolumnerna eller öppna ett kort för att ändra det.
export default function Board({ applications, onUpdate, onDelete, onAdd, onOpenLetter, onPrepareInterview }: Props) {
  const { t, locale } = useLang()
  const [editing, setEditing] = useState<Application | null>(null)
  const [over, setOver] = useState<Stage | null>(null)
  const counts = countByStage(applications)

  function drop(e: DragEvent, stage: Stage) {
    e.preventDefault()
    setOver(null)
    const card = applications.find(a => a.id === e.dataTransfer.getData('text/plain'))
    if (card && card.stage !== stage) onUpdate({ ...card, stage })
  }

  const sent = applications.length - counts.saved
  return (
    <section>
      <div className="card board-head">
        <div>
          <h2>{t('Mina ansökningar')}</h2>
          <p className="muted">
            {applications.length === 0
              ? t('Spara jobb från en sökning eller ett brev, eller lägg till ett för hand.')
              : [
                  t('{n} sökta', { n: sent }),
                  counts.interview === 1 ? t('{n} intervju', { n: 1 }) : t('{n} intervjuer', { n: counts.interview }),
                  counts.offer === 1 ? t('{n} erbjudande', { n: 1 }) : t('{n} erbjudanden', { n: counts.offer }),
                ].join(' · ')}
          </p>
        </div>
        <button onClick={onAdd}>+ {t('Lägg till jobb')}</button>
      </div>
      <div className="board">
        {STAGES.map(stage => (
          <div key={stage.id} className={`column ${over === stage.id ? 'over' : ''}`}
            onDragOver={e => { e.preventDefault(); setOver(stage.id) }}
            onDragLeave={() => setOver(null)}
            onDrop={e => drop(e, stage.id)}>
            <h3>{stage.icon} {t(stage.label)} <span className="meta">{counts[stage.id]}</span></h3>
            {applications.filter(a => a.stage === stage.id).map(a => {
              const next = nextUp(a)
              return (
                <button key={a.id} className="job" draggable onDragStart={e => e.dataTransfer.setData('text/plain', a.id)} onClick={() => setEditing(a)}>
                  <span className="job-title">{a.title}</span>
                  {a.company && <span className="meta">{a.company}</span>}
                  <span className="job-foot">
                    {a.matchScore != null && <Score value={a.matchScore} />}
                    {a.letterRunId && <span title={t('Personligt brev finns')}>✉️</span>}
                    {a.notes && <span title={t('Har anteckningar')}>📝</span>}
                    <span className="meta">{new Date(a.stageChangedAt).toLocaleDateString(locale)}</span>
                  </span>
                  {next && <span className={`next ${next.urgent ? 'urgent' : ''}`}>{next.text}</span>}
                </button>
              )
            })}
          </div>
        ))}
      </div>
      {editing && (
        <ApplicationEditor application={editing} onClose={() => setEditing(null)}
          onSave={async a => { await onUpdate(a); setEditing(null) }}
          onDelete={async () => { await onDelete(editing.id); setEditing(null) }}
          onOpenLetter={editing.letterRunId ? () => onOpenLetter(editing.letterRunId!) : undefined}
          onPrepareInterview={() => onPrepareInterview(editing)} />
      )}
    </section>
  )
}
