import { useState, type FormEvent } from 'react'
import type { NewSearch } from '../api'

export default function SearchForm({ onSearch, disabled }: { onSearch: (search: NewSearch) => Promise<void>; disabled: boolean }) {
  const [location, setLocation] = useState('')
  const [keywords, setKeywords] = useState('')
  const [remoteOnly, setRemoteOnly] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onSearch({ location, keywords, remoteOnly })
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card form" onSubmit={submit}>
      <h2>Hitta jobb åt mig</h2>
      <p className="muted">
        <b>🔎 Sökaren</b> läser ditt CV, tar fram sökord och söker bland annonserna i Arbetsförmedlingens Platsbanken.
        Sedan ger <b>🎯 Matcharen</b> varje annons en poäng utifrån hur väl du passar.
      </p>
      <div className="row">
        <label>
          Ort (valfritt)
          <input value={location} maxLength={100} onChange={e => setLocation(e.target.value)} placeholder="t.ex. Stockholm" />
        </label>
        <label>
          Egna sökord (valfritt)
          <input value={keywords} maxLength={200} onChange={e => setKeywords(e.target.value)} placeholder="t.ex. backend, .NET" />
        </label>
      </div>
      <label className="check">
        <input type="checkbox" checked={remoteOnly} onChange={e => setRemoteOnly(e.target.checked)} />
        Bara jobb på distans
      </label>
      <button disabled={busy || disabled}>{busy ? 'Startar…' : 'Sök jobb'}</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
