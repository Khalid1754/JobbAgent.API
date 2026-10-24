import { useCallback, useEffect, useState } from 'react'
import { api, type NewLetter, type NewSearch, type RunSummary } from './api'
import CvPanel from './components/CvPanel'
import LetterForm from './components/LetterForm'
import RunList from './components/RunList'
import RunView from './components/RunView'
import SearchForm from './components/SearchForm'

type Tab = 'search' | 'letter' | 'cv'

const TABS: { id: Tab; label: string }[] = [
  { id: 'search', label: '🔎 Hitta jobb' },
  { id: 'letter', label: '✉️ Egen annons' },
  { id: 'cv', label: '📄 Mitt CV' },
]

export default function App() {
  const [runs, setRuns] = useState<RunSummary[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('search')
  const [status, setStatus] = useState({ aiEnabled: true, hasCv: true })
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(() => {
    api.listRuns().then(setRuns, e => setError((e as Error).message))
  }, [])

  useEffect(() => {
    refresh()
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

  async function remove(id: string) {
    await api.deleteRun(id)
    setSelected(null)
    refresh()
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
            <RunView key={current.id} run={current} onFinished={refresh} onDelete={() => remove(current.id)} onCreateLetter={createLetter} />
          ) : tab === 'search' ? (
            <SearchForm onSearch={search} disabled={!canRun} />
          ) : tab === 'letter' ? (
            <LetterForm onCreate={createLetter} disabled={!canRun} />
          ) : (
            <CvPanel onSaved={() => setStatus(s => ({ ...s, hasCv: true }))} />
          )}
        </div>
      </main>
    </>
  )
}
