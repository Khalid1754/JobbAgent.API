import { useCallback, useEffect, useState } from 'react'
import { msg, useLang } from './i18n'
import { api, type Application, type Cv, type NewApplication, type NewLetter, type NewSearch, type RunSummary, type Watch } from './api'
import ApplicationEditor from './components/ApplicationEditor'
import Board from './components/Board'
import CvPanel from './components/CvPanel'
import Preferences from './components/Preferences'
import LetterForm from './components/LetterForm'
import RunList from './components/RunList'
import RunView from './components/RunView'
import SearchForm, { type SearchOptions } from './components/SearchForm'
import WatchList from './components/WatchList'

type Tab = 'search' | 'letter' | 'board' | 'cv'

const TABS: { id: Tab; label: string }[] = [
  { id: 'search', label: msg('🔎 Hitta jobb') },
  { id: 'letter', label: msg('✉️ Egen annons') },
  { id: 'board', label: msg('📋 Mina ansökningar') },
  { id: 'cv', label: msg('📄 Mina CV') },
]

export default function App() {
  const { t } = useLang()
  const [runs, setRuns] = useState<RunSummary[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('search')
  const [status, setStatus] = useState<{ aiEnabled: boolean; hasCv: boolean; jsearch?: boolean }>({ aiEnabled: true, hasCv: true })
  const [error, setError] = useState<string | null>(null)
  const [applications, setApplications] = useState<Application[]>([])
  const [adding, setAdding] = useState(false)
  const [watches, setWatches] = useState<Watch[]>([])
  const [cvs, setCvs] = useState<Cv[]>([])
  const loadCvs = useCallback(async () => {
    const list = await api.listCvs()
    setCvs(list)
    setStatus(s => ({ ...s, hasCv: list.length > 0 }))
  }, [])

  const refresh = useCallback(() => {
    api.listRuns().then(setRuns, e => setError((e as Error).message))
    api.listWatches().then(setWatches, () => {})
  }, [])

  // Bevakningarna körs i bakgrunden, så historiken hämtas om då och då.
  useEffect(() => {
    const timer = setInterval(refresh, 5 * 60 * 1000)
    return () => clearInterval(timer)
  }, [refresh])

  useEffect(() => {
    refresh()
    api.listApplications().then(setApplications, () => {})
    api.listCvs().then(setCvs, () => {})
    api.status().then(s => {
      setStatus(s)
      if (!s.hasCv) setTab('cv')
    }, () => {})
  }, [refresh, loadCvs])

  function open(run: RunSummary) {
    setRuns(r => [run, ...r.filter(x => x.id !== run.id)])
    setSelected(run.id)
  }

  async function search(s: NewSearch, options: SearchOptions) {
    if (!options.watch) return open(await api.search(s))
    const watch = await api.createWatch({ ...s, minScore: options.minScore })
    open(await api.runWatch(watch.id))
    refresh()
  }

  // Öppnar en körning. Är det en bevaknings senaste sökning räknas de nya annonserna som sedda.
  function select(id: string) {
    setSelected(id)
    const watch = watches.find(w => w.lastRunId === id && w.newMatches > 0)
    if (watch) api.markWatchSeen(watch.id).then(() => setWatches(list => list.map(w => (w.id === watch.id ? { ...w, newMatches: 0 } : w))), () => {})
  }

  const runWatch = async (w: Watch) => { open(await api.runWatch(w.id)); refresh() }
  const toggleWatch = async (w: Watch) => {
    const updated = await api.updateWatch(w.id, { ...w, enabled: !w.enabled })
    setWatches(list => list.map(x => (x.id === w.id ? updated : x)))
  }
  const deleteWatch = async (w: Watch) => {
    await api.deleteWatch(w.id)
    setWatches(list => list.filter(x => x.id !== w.id))
  }
  const newMatches = watches.reduce((sum, w) => sum + w.newMatches, 0)
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

  function showTab(next: Tab) {
    setTab(next)
    setSelected(null)
  }

  const current = runs.find(r => r.id === selected)
  const canRun = status.aiEnabled && status.hasCv

  return (
    <>
      <header>
        <h1>Jobb<span>Agent</span></h1>
        <span className="muted">{t('AI-agenter som hittar jobb och skriver personliga brev utifrån ditt CV')}</span>
        <Preferences />
      </header>
      {!status.aiEnabled && <div className="banner">{t('GROQ_API_KEY saknas. Lägg in den i .env och starta om med start.bat.')}</div>}
      {status.aiEnabled && !status.hasCv && <div className="banner">{t('Börja med att lägga in ditt CV, sedan kan agenterna jobba.')}</div>}
      {error && <div className="banner">{error}</div>}
      <main>
        <aside>
          <nav className="tabs">
            {TABS.map(item => (
              <button key={item.id} className={!current && tab === item.id ? 'active' : ''} onClick={() => showTab(item.id)}>
                {t(item.label)}
                {item.id === 'search' && newMatches > 0 && <span className="count">{newMatches}</span>}
              </button>
            ))}
          </nav>
          <h3>{t('Historik')}</h3>
          <RunList runs={runs} selected={selected} onSelect={select} />
        </aside>
        <div>
          {current ? (
            <RunView key={current.id} run={current} onFinished={refresh} onDelete={() => remove(current.id)} onCreateLetter={createLetter}
              applications={applications} onSaveJob={saveJob} onPrepareInterview={prepareInterview} cvs={cvs} />
          ) : tab === 'search' ? (
            <>
              <SearchForm cvs={cvs} onSearch={search} disabled={!canRun} moreSources={!!status.jsearch} />
              <WatchList watches={watches} onOpen={select} onRun={runWatch} onToggle={toggleWatch} onDelete={deleteWatch} />
            </>
          ) : tab === 'letter' ? (
            <LetterForm cvs={cvs} onCreate={createLetter} disabled={!canRun} />
          ) : tab === 'board' ? (
            <Board applications={applications} onUpdate={updateJob} onDelete={deleteJob} onAdd={() => setAdding(true)} onOpenLetter={select}
              onPrepareInterview={a => prepareInterview(a.letterRunId ? { letterRunId: a.letterRunId } : { applicationId: a.id })} />
          ) : (
            <CvPanel cvs={cvs} onChange={loadCvs} />
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
