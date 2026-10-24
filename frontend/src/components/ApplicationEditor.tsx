import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { Application, Stage } from '../api'
import { STAGES } from '../board'
import { useLang } from '../i18n'

interface Props {
  application: Application
  onSave: (a: Application) => Promise<void>
  onDelete?: () => Promise<void>
  onClose: () => void
  onOpenLetter?: () => void
  onPrepareInterview?: () => Promise<void>
}

// <input type="date"> vill ha ÅÅÅÅ-MM-DD och datetime-local ÅÅÅÅ-MM-DDTHH:MM i lokal tid.
const pad = (n: number) => String(n).padStart(2, '0')
const toDate = (iso?: string) => {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
const toDateTime = (iso?: string) => (iso ? `${toDate(iso)}T${pad(new Date(iso).getHours())}:${pad(new Date(iso).getMinutes())}` : '')
const fromInput = (value: string) => (value ? new Date(value).toISOString() : undefined)

export default function ApplicationEditor({ application, onSave, onDelete, onClose, onOpenLetter, onPrepareInterview }: Props) {
  const { t } = useLang()
  const [a, setA] = useState(application)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const dialog = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const d = dialog.current
    d?.showModal()
    return () => d?.close()
  }, [])

  const set = <K extends keyof Application>(key: K, value: Application[K]) => setA(x => ({ ...x, [key]: value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onSave(a)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <dialog ref={dialog} onClose={onClose} onClick={e => e.target === dialog.current && onClose()}>
      <form className="form" onSubmit={submit}>
        <h2>{application.id ? t('Ändra jobb') : t('Lägg till jobb')}</h2>
        <label>
          {t('Titel')}
          <input required value={a.title} maxLength={300} onChange={e => set('title', e.target.value)} />
        </label>
        <div className="row">
          <label>
            {t('Arbetsgivare')}
            <input value={a.company ?? ''} maxLength={200} onChange={e => set('company', e.target.value)} />
          </label>
          <label>
            {t('Ort')}
            <input value={a.location ?? ''} maxLength={100} onChange={e => set('location', e.target.value)} />
          </label>
        </div>
        <label>
          {t('Länk till annonsen')}
          <input type="url" value={a.url ?? ''} onChange={e => set('url', e.target.value)} placeholder="https://…" />
        </label>
        <div className="row">
          <label>
            {t('Steg')}
            <select value={a.stage} onChange={e => set('stage', e.target.value as Stage)}>
              {STAGES.map(s => <option key={s.id} value={s.id}>{s.icon} {t(s.label)}</option>)}
            </select>
          </label>
          <label>
            {t('Sista ansökningsdag')}
            <input type="date" value={toDate(a.deadline)} onChange={e => set('deadline', fromInput(e.target.value))} />
          </label>
        </div>
        <div className="row">
          <label>
            {t('Sökt den')}
            <input type="date" value={toDate(a.appliedAt)} onChange={e => set('appliedAt', fromInput(e.target.value))} />
          </label>
          <label>
            {t('Intervju')}
            <input type="datetime-local" value={toDateTime(a.interviewAt)} onChange={e => set('interviewAt', fromInput(e.target.value))} />
          </label>
        </div>
        <label>
          {t('Anteckningar')}
          <textarea rows={5} maxLength={4000} value={a.notes ?? ''} onChange={e => set('notes', e.target.value)}
            placeholder={t('Kontaktperson, vad ni pratade om, nästa steg…')} />
        </label>
        {error && <p className="error">{error}</p>}
        <div className="actions">
          {onDelete && <button type="button" className="danger" onClick={onDelete}>{t('Ta bort')}</button>}
          {a.url && <a href={a.url} target="_blank" rel="noreferrer">{t('Annonsen')}</a>}
          {onOpenLetter && <button type="button" className="secondary" onClick={onOpenLetter}>{t('Visa brevet')}</button>}
          {onPrepareInterview && (
            <button type="button" className="secondary" disabled={busy} onClick={() => {
              setBusy(true)
              onPrepareInterview().catch(err => { setError((err as Error).message); setBusy(false) })
            }}>🗣️ {t('Förbered intervju')}</button>
          )}
          <span className="spacer" />
          <button type="button" className="secondary" onClick={onClose}>{t('Avbryt')}</button>
          <button disabled={busy || a.title.trim().length === 0}>{busy ? t('Sparar…') : t('Spara')}</button>
        </div>
      </form>
    </dialog>
  )
}
