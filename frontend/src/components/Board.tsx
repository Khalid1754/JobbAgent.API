import { useState, type DragEvent } from 'react'
import type { Application, Stage } from '../api'
import { STAGES, countByStage, nextUp } from '../board'
import ApplicationEditor from './ApplicationEditor'
import Score from './Score'

interface Props {
  applications: Application[]
  onUpdate: (a: Application) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onAdd: () => void
  onOpenLetter: (runId: string) => void
}

const date = (iso: string) => new Date(iso).toLocaleDateString('sv-SE')

// Ansökningstavlan: dra korten mellan kolumnerna eller öppna ett kort för att ändra det.
export default function Board({ applications, onUpdate, onDelete, onAdd, onOpenLetter }: Props) {
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
          <h2>Mina ansökningar</h2>
          <p className="muted">
            {applications.length === 0
              ? 'Spara jobb från en sökning eller ett brev, eller lägg till ett för hand.'
              : `${sent} sökta · ${counts.interview} ${counts.interview === 1 ? 'intervju' : 'intervjuer'} · ${counts.offer} ${counts.offer === 1 ? 'erbjudande' : 'erbjudanden'}`}
          </p>
        </div>
        <button onClick={onAdd}>+ Lägg till jobb</button>
      </div>
      <div className="board">
        {STAGES.map(stage => (
          <div key={stage.id} className={`column ${over === stage.id ? 'over' : ''}`}
            onDragOver={e => { e.preventDefault(); setOver(stage.id) }}
            onDragLeave={() => setOver(null)}
            onDrop={e => drop(e, stage.id)}>
            <h3>{stage.icon} {stage.label} <span className="meta">{counts[stage.id]}</span></h3>
            {applications.filter(a => a.stage === stage.id).map(a => {
              const next = nextUp(a)
              return (
                <button key={a.id} className="job" draggable onDragStart={e => e.dataTransfer.setData('text/plain', a.id)} onClick={() => setEditing(a)}>
                  <span className="job-title">{a.title}</span>
                  {a.company && <span className="meta">{a.company}</span>}
                  <span className="job-foot">
                    {a.matchScore != null && <Score value={a.matchScore} />}
                    {a.letterRunId && <span title="Personligt brev finns">✉️</span>}
                    {a.notes && <span title="Har anteckningar">📝</span>}
                    <span className="meta">{date(a.stageChangedAt)}</span>
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
          onOpenLetter={editing.letterRunId ? () => onOpenLetter(editing.letterRunId!) : undefined} />
      )}
    </section>
  )
}
