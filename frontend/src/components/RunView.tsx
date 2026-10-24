import { useCallback, useEffect, useState } from 'react'
import { api, type Application, type Cv, type NewApplication, type NewLetter, type Run, type RunSummary } from '../api'
import { useRunStream } from '../useRunStream'
import InterviewView from './InterviewView'
import LetterView from './LetterView'
import Pipeline from './Pipeline'
import SearchResults from './SearchResults'
import Timeline from './Timeline'

interface Props {
  run: RunSummary
  onFinished: () => void
  onDelete: () => void
  onCreateLetter: (letter: NewLetter) => Promise<void>
  applications: Application[]
  onSaveJob: (job: NewApplication) => Promise<void>
  onPrepareInterview: (from: { letterRunId?: string }) => Promise<void>
  cvs: Cv[]
}

export default function RunView({ run, onFinished, onDelete, onCreateLetter, applications, onSaveJob, onPrepareInterview, cvs }: Props) {
  const [detail, setDetail] = useState<Run | null>(null)
  const load = useCallback(() => api.getRun(run.id).then(setDetail, () => {}), [run.id])
  useEffect(() => { load() }, [load])

  // När agenterna är klara hämtas resultatet (annonser eller brev).
  const state = useRunStream(run.id, () => {
    load()
    onFinished()
  })

  const title = detail?.title ?? run.title
  const company = detail?.company ?? run.company
  const cv = cvs.length > 1 && detail ? (cvs.find(c => c.id === detail.cvId) ?? cvs.find(c => c.isDefault)) : undefined

  return (
    <section>
      <div className="card">
        <h2>{title}{company && <span className="muted"> · {company}</span>}</h2>
        {cv && <p className="meta">Med CV:t {cv.name}</p>}
        {state.status === 'queued' && <p className="muted">I kö, startar strax…</p>}
        <Pipeline kind={run.kind} steps={state.steps} />
      </div>

      {state.error && <div className="card error">{state.error}</div>}

      {detail?.status === 'completed' && run.kind === 'search' && (
        <SearchResults hits={detail.hits ?? []} cvId={detail.cvId} applications={applications} onCreateLetter={onCreateLetter} onSaveJob={onSaveJob} />
      )}
      {detail?.status === 'completed' && run.kind === 'letter' && (
        <LetterView run={detail} onSaveJob={onSaveJob} onPrepareInterview={() => onPrepareInterview({ letterRunId: run.id })}
          application={applications.find(a => a.letterRunId === run.id || (!!detail.platsbankenId && a.platsbankenId === detail.platsbankenId))} />
      )}
      {detail?.status === 'completed' && run.kind === 'interview' && detail.interview && <InterviewView prep={detail.interview} />}

      {state.steps.length > 0 && (
        <div className="card">
          <details>
            <summary><b>Vad agenterna gjorde</b></summary>
            <Timeline steps={state.steps} />
          </details>
        </div>
      )}

      {(state.status === 'completed' || state.status === 'failed') && (
        <button className="danger" onClick={onDelete}>Ta bort</button>
      )}
    </section>
  )
}
