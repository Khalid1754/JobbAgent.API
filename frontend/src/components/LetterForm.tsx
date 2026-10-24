import { useState, type FormEvent } from 'react'
import type { Language, NewLetter } from '../api'

export default function LetterForm({ onCreate, disabled }: { onCreate: (letter: NewLetter) => Promise<void>; disabled: boolean }) {
  const [mode, setMode] = useState<'url' | 'text'>('url')
  const [adUrl, setAdUrl] = useState('')
  const [adText, setAdText] = useState('')
  const [language, setLanguage] = useState<Language>('auto')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const ready = mode === 'url' ? /^https?:\/\/\S+$/.test(adUrl.trim()) : adText.trim().length >= 100

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onCreate(mode === 'url' ? { adUrl: adUrl.trim(), language } : { adText, language })
    } catch (err) {
      setError((err as Error).message)
      // Om länken inte gick att läsa är nästa steg att klistra in texten.
      if (mode === 'url') setMode('text')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card form" onSubmit={submit}>
      <h2>Skriv brev till en annons</h2>
      <p className="muted">
        <b>📄 Tolkaren</b> plockar ut kraven, <b>🎯 Matcharen</b> jämför dem med ditt CV, <b>✍️ Skribenten</b> skriver
        brevet och <b>✅ Granskaren</b> kontrollerar att inget är påhittat.
      </p>
      <div className="tabs small">
        <button type="button" className={mode === 'url' ? 'active' : ''} onClick={() => setMode('url')}>Länk</button>
        <button type="button" className={mode === 'text' ? 'active' : ''} onClick={() => setMode('text')}>Klistra in text</button>
      </div>
      {mode === 'url' ? (
        <label>
          Länk till annonsen (Platsbanken fungerar bäst)
          <input type="url" value={adUrl} onChange={e => setAdUrl(e.target.value)} placeholder="https://arbetsformedlingen.se/platsbanken/annonser/…" />
        </label>
      ) : (
        <label>
          Annonstexten
          <textarea rows={10} value={adText} maxLength={20000} onChange={e => setAdText(e.target.value)} placeholder="Klistra in hela annonsen här…" />
        </label>
      )}
      <label>
        Språk på brevet
        <select value={language} onChange={e => setLanguage(e.target.value as Language)}>
          <option value="auto">Samma som annonsen</option>
          <option value="sv">Svenska</option>
          <option value="en">Engelska</option>
        </select>
      </label>
      <button disabled={busy || disabled || !ready}>{busy ? 'Hämtar annonsen…' : 'Skriv personligt brev'}</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
