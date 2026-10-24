import { useCallback, useEffect, useState } from 'react'
import { api, type Application, type NewApplication, type NewLetter, type Run, type RunSummary } from '../api'
import { useRunStream } from '../useRunStream'
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
}

export default function RunView({ run, onFinished, onDelete, onCreateLetter, applications, onSaveJob }: Props) {
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

  return (
    <section>
      <div className="card">
        <h2>{title}{company && <span className="muted"> · {company}</span>}</h2>
        {state.status === 'queued' && <p className="muted">I kö, startar strax…</p>}
        <Pipeline kind={run.kind} steps={state.steps} />
      </div>

      {state.error && <div className="card error">{state.error}</div>}

      {detail?.status === 'completed' && (run.kind === 'search'
        ? <SearchResults hits={detail.hits ?? []} applications={applications} onCreateLetter={onCreateLetter} onSaveJob={onSaveJob} />
        : <LetterView run={detail} application={applications.find(a => a.letterRunId === run.id || (!!detail.platsbankenId && a.platsbankenId === detail.platsbankenId))} onSaveJob={onSaveJob} />)}

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
