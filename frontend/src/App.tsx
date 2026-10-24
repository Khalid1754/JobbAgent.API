import { useCallback, useEffect, useState } from 'react'
import { api, type Application, type NewApplication, type NewLetter, type NewSearch, type RunSummary } from './api'
import ApplicationEditor from './components/ApplicationEditor'
import Board from './components/Board'
import CvPanel from './components/CvPanel'
import LetterForm from './components/LetterForm'
import RunList from './components/RunList'
import RunView from './components/RunView'
import SearchForm from './components/SearchForm'

type Tab = 'search' | 'letter' | 'board' | 'cv'

const TABS: { id: Tab; label: string }[] = [
  { id: 'search', label: '🔎 Hitta jobb' },
  { id: 'letter', label: '✉️ Egen annons' },
  { id: 'board', label: '📋 Mina ansökningar' },
  { id: 'cv', label: '📄 Mitt CV' },
]

export default function App() {
  const [runs, setRuns] = useState<RunSummary[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('search')
  const [status, setStatus] = useState({ aiEnabled: true, hasCv: true })
  const [error, setError] = useState<string | null>(null)
  const [applications, setApplications] = useState<Application[]>([])
  const [adding, setAdding] = useState(false)

  const refresh = useCallback(() => {
    api.listRuns().then(setRuns, e => setError((e as Error).message))
  }, [])

  useEffect(() => {
    refresh()
    api.listApplications().then(setApplications, () => {})
    api.status().then(s => {
      setStatus(s)
      if (!s.hasCv) setTab('cv')
    }, () => {})
  }, [refresh])

  function open(run: RunSummary) {
    setRuns(r => [run, ...r.filter(x => x.id !== run.id)])
    setSelected(run.id)
  }

  const search = async (s: NewSearch) => open(await api.search(s))
  const createLetter = async (l: NewLetter) => open(await api.createLetter(l))

  const upsert = (a: Application) => setApplications(list => [a, ...list.filter(x => x.id !== a.id)])
  const saveJob = async (job: NewApplication) => upsert(await api.createApplication(job))
  const updateJob = async (a: Application) => upsert(await api.updateApplication(a))

  // Finns redan en intervjuförberedelse för jobbet öppnas den, annars startar agenterna en ny.
  async function prepareInterview(from: { letterRunId?: string; applicationId?: string }) {
    const existing = runs.find(r => r.kind === 'interview' && r.status !== 'failed' &&
      ((from.letterRunId && r.sourceRunId === from.letterRunId) || (from.applicationId && r.applicationId === from.applicationId)))
    open(existing ?? await api.createInterview(from))
  }

  async function deleteJob(id: string) {
    await api.deleteApplication(id)
    setApplications(list => list.filter(a => a.id !== id))
  }

  // Ett nytt kort skapas först och fylls sedan i med det som bara går att ändra (anteckningar, datum).
  async function addJob(a: Application) {
    const created = await api.createApplication({ title: a.title, company: a.company, location: a.location, url: a.url, deadline: a.deadline, stage: a.stage })
    upsert(a.notes || a.appliedAt || a.interviewAt ? await api.updateApplication({ ...a, id: created.id }) : created)
    setAdding(false)
  }

  async function remove(id: string) {
    await api.deleteRun(id)
    setSelected(null)
    refresh()
    api.listApplications().then(setApplications, () => {})
  }

  function showTab(t: Tab) {
    setTab(t)
    setSelected(null)
  }

  const current = runs.find(r => r.id === selected)
  const canRun = status.aiEnabled && status.hasCv

  return (
    <>
      <header>
        <h1>Jobb<span>Agent</span></h1>
        <span className="muted">AI-agenter som hittar jobb och skriver personliga brev utifrån ditt CV</span>
      </header>
      {!status.aiEnabled && <div className="banner">GROQ_API_KEY saknas. Lägg in den i .env och starta om med start.bat.</div>}
      {status.aiEnabled && !status.hasCv && <div className="banner">Börja med att lägga in ditt CV, sedan kan agenterna jobba.</div>}
      {error && <div className="banner">{error}</div>}
      <main>
        <aside>
          <nav className="tabs">
            {TABS.map(t => (
              <button key={t.id} className={!current && tab === t.id ? 'active' : ''} onClick={() => showTab(t.id)}>{t.label}</button>
            ))}
          </nav>
          <h3>Historik</h3>
          <RunList runs={runs} selected={selected} onSelect={setSelected} />
        </aside>
        <div>
          {current ? (
            <RunView key={current.id} run={current} onFinished={refresh} onDelete={() => remove(current.id)} onCreateLetter={createLetter}
              applications={applications} onSaveJob={saveJob} onPrepareInterview={prepareInterview} />
          ) : tab === 'search' ? (
            <SearchForm onSearch={search} disabled={!canRun} />
          ) : tab === 'letter' ? (
            <LetterForm onCreate={createLetter} disabled={!canRun} />
          ) : tab === 'board' ? (
            <Board applications={applications} onUpdate={updateJob} onDelete={deleteJob} onAdd={() => setAdding(true)} onOpenLetter={setSelected}
              onPrepareInterview={a => prepareInterview(a.letterRunId ? { letterRunId: a.letterRunId } : { applicationId: a.id })} />
          ) : (
            <CvPanel onSaved={() => setStatus(s => ({ ...s, hasCv: true }))} />
          )}
        </div>
      </main>
      {adding && (
        <ApplicationEditor application={{ id: '', title: '', stage: 'saved', createdAt: '', stageChangedAt: '' }}
          onSave={addJob} onClose={() => setAdding(false)} />
      )}
    </>
  )
}
