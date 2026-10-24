import { useState } from 'react'
import type { Application, NewApplication, NewLetter, SearchHit } from '../api'
import Score from './Score'

const date = (iso: string) => new Date(iso).toLocaleDateString('sv-SE')

interface Props {
  hits: SearchHit[]
  cvId?: number
  applications: Application[]
  onCreateLetter: (letter: NewLetter) => Promise<void>
  onSaveJob: (job: NewApplication) => Promise<void>
}

export default function SearchResults({ hits, cvId, applications, onCreateLetter, onSaveJob }: Props) {
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function write(hit: SearchHit) {
    setBusy(hit.id)
    setError(null)
    try {
      await onCreateLetter({ platsbankenId: hit.id, cvId })
    } catch (err) {
      setError((err as Error).message)
      setBusy(null)
    }
  }

  async function save(hit: SearchHit) {
    setError(null)
    try {
      await onSaveJob({
        title: hit.title, company: hit.employer, location: hit.municipality, url: hit.url,
        platsbankenId: hit.id, deadline: hit.deadline, matchScore: hit.score,
      })
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const saved = new Set(applications.map(a => a.platsbankenId))

  if (hits.length === 0)
    return <div className="card">Inga annonser hittades. Prova en annan ort, egna sökord eller ta bort distansvalet.</div>

  return (
    <div className="card">
      <h3>{hits.length} annonser, bäst matchning först{hits.some(h => h.isNew) && ` · ${hits.filter(h => h.isNew).length} nya sedan förra gången`}</h3>
      {error && <p className="error">{error}</p>}
      <ul className="hits">
        {hits.map(hit => (
          <li key={hit.id}>
            <Score value={hit.score} large />
            <div>
              {hit.isNew && <span className="new">Ny</span>}
              <a href={hit.url} target="_blank" rel="noreferrer"><b>{hit.title}</b></a>
              <div className="meta">
                {[hit.employer, hit.municipality, hit.deadline && `sök senast ${date(hit.deadline)}`].filter(Boolean).join(' · ')}
              </div>
              <div>{hit.reason}</div>
            </div>
            <div className="hit-actions">
              <button className="secondary" disabled={busy !== null} onClick={() => write(hit)}>
                {busy === hit.id ? 'Startar…' : 'Skriv brev'}
              </button>
              <button className="secondary" disabled={saved.has(hit.id)} onClick={() => save(hit)} title="Spara på ansökningstavlan">
                {saved.has(hit.id) ? '✓ Sparad' : '⭐ Spara'}
              </button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
