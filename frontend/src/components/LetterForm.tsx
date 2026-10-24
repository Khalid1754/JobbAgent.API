import { useState, type FormEvent } from 'react'
import type { Cv, Language, NewLetter } from '../api'
import { useLang } from '../i18n'
import CvSelect from './CvSelect'

export default function LetterForm({ cvs, onCreate, disabled }: { cvs: Cv[]; onCreate: (letter: NewLetter) => Promise<void>; disabled: boolean }) {
  const { t } = useLang()
  const [mode, setMode] = useState<'url' | 'text'>('url')
  const [adUrl, setAdUrl] = useState('')
  const [adText, setAdText] = useState('')
  const [language, setLanguage] = useState<Language>('auto')
  const [cvId, setCvId] = useState<number>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const ready = mode === 'url' ? /^https?:\/\/\S+$/.test(adUrl.trim()) : adText.trim().length >= 100

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onCreate(mode === 'url' ? { adUrl: adUrl.trim(), language, cvId } : { adText, language, cvId })
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
      <h2>{t('Skriv brev till en annons')}</h2>
      <p className="muted">
        <b>📄 {t('Tolkaren')}</b> {t('plockar ut kraven,')} <b>🎯 {t('Matcharen')}</b> {t('jämför dem med ditt CV,')}{' '}
        <b>✍️ {t('Skribenten')}</b> {t('skriver brevet och')} <b>✅ {t('Granskaren')}</b> {t('kontrollerar att inget är påhittat.')}
      </p>
      <div className="tabs small">
        <button type="button" className={mode === 'url' ? 'active' : ''} onClick={() => setMode('url')}>{t('Länk')}</button>
        <button type="button" className={mode === 'text' ? 'active' : ''} onClick={() => setMode('text')}>{t('Klistra in text')}</button>
      </div>
      {mode === 'url' ? (
        <label>
          {t('Länk till annonsen (Platsbanken fungerar bäst)')}
          <input type="url" value={adUrl} onChange={e => setAdUrl(e.target.value)} placeholder="https://arbetsformedlingen.se/platsbanken/annonser/…" />
        </label>
      ) : (
        <label>
          {t('Annonstexten')}
          <textarea rows={10} value={adText} maxLength={20000} onChange={e => setAdText(e.target.value)} placeholder={t('Klistra in hela annonsen här…')} />
        </label>
      )}
      <CvSelect cvs={cvs} value={cvId} onChange={setCvId} />
      <label>
        {t('Språk på brevet')}
        <select value={language} onChange={e => setLanguage(e.target.value as Language)}>
          <option value="auto">{t('Samma som annonsen')}</option>
          <option value="sv">{t('Svenska')}</option>
          <option value="en">{t('Engelska')}</option>
        </select>
      </label>
      <button disabled={busy || disabled || !ready}>{busy ? t('Hämtar annonsen…') : t('Skriv personligt brev')}</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
