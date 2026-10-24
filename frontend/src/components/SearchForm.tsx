import { useState, type FormEvent } from 'react'
import type { Cv, NewSearch } from '../api'
import { useLang } from '../i18n'
import CvSelect from './CvSelect'

export interface SearchOptions {
  watch: boolean
  minScore: number
}

interface Props {
  cvs: Cv[]
  onSearch: (search: NewSearch, options: SearchOptions) => Promise<void>
  disabled: boolean
}

export default function SearchForm({ cvs, onSearch, disabled }: Props) {
  const { t } = useLang()
  const [location, setLocation] = useState('')
  const [keywords, setKeywords] = useState('')
  const [remoteOnly, setRemoteOnly] = useState(false)
  const [watch, setWatch] = useState(false)
  const [minScore, setMinScore] = useState(70)
  const [cvId, setCvId] = useState<number>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onSearch({ location, keywords, remoteOnly, cvId }, { watch, minScore })
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card form" onSubmit={submit}>
      <h2>{t('Hitta jobb åt mig')}</h2>
      <p className="muted">
        <b>🔎 {t('Sökaren')}</b> {t('läser ditt CV, tar fram sökord och söker bland annonserna i Arbetsförmedlingens Platsbanken. Sedan ger')}{' '}
        <b>🎯 {t('Matcharen')}</b> {t('varje annons en poäng utifrån hur väl du passar.')}
      </p>
      <div className="row">
        <label>
          {t('Ort (valfritt)')}
          <input value={location} maxLength={100} onChange={e => setLocation(e.target.value)} placeholder={t('t.ex. Stockholm')} />
        </label>
        <label>
          {t('Egna sökord (valfritt)')}
          <input value={keywords} maxLength={200} onChange={e => setKeywords(e.target.value)} placeholder={t('t.ex. backend, .NET')} />
        </label>
      </div>
      <CvSelect cvs={cvs} value={cvId} onChange={setCvId} />
      <label className="check">
        <input type="checkbox" checked={remoteOnly} onChange={e => setRemoteOnly(e.target.checked)} />
        {t('Bara jobb på distans')}
      </label>
      <label className="check">
        <input type="checkbox" checked={watch} onChange={e => setWatch(e.target.checked)} />
        {t('Bevaka sökningen: kör den automatiskt varje morgon och visa nya annonser som matchar minst')}
        <select className="inline" value={minScore} disabled={!watch} onChange={e => setMinScore(Number(e.target.value))}>
          {[50, 60, 70, 80, 90].map(n => <option key={n} value={n}>{n} %</option>)}
        </select>
      </label>
      <button disabled={busy || disabled}>{busy ? t('Startar…') : t('Sök jobb')}</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
