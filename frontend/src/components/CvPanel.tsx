import { useEffect, useState, type ChangeEvent } from 'react'
import { api, type Contact, type Profile } from '../api'

const CONTACT_FIELDS: { key: keyof Contact; label: string; type: string; placeholder: string }[] = [
  { key: 'fullName', label: 'Namn', type: 'text', placeholder: 'För- och efternamn' },
  { key: 'email', label: 'E-post', type: 'email', placeholder: 'namn@exempel.se' },
  { key: 'phone', label: 'Telefon', type: 'tel', placeholder: '070-123 45 67' },
  { key: 'city', label: 'Ort', type: 'text', placeholder: 'Stockholm' },
  { key: 'link', label: 'LinkedIn, GitHub eller portfolio', type: 'text', placeholder: 'linkedin.com/in/…' },
]

// Ditt CV: ladda upp en fil eller klistra in texten. Agenterna läser det vid varje körning.
export default function CvPanel({ onSaved }: { onSaved: (profile: Profile) => void }) {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [text, setText] = useState('')
  const [contact, setContact] = useState<Contact>({})
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  useEffect(() => {
    api.profile().then(p => {
      setProfile(p)
      setText(p?.cvText ?? '')
      setContact(p ?? {})
    }, () => {})
  }, [])

  async function run(action: () => Promise<Profile>, done: string) {
    setBusy(true)
    setMessage(null)
    try {
      const saved = await action()
      setProfile(saved)
      setText(saved.cvText)
      setContact(saved)
      setMessage({ ok: true, text: done })
      onSaved(saved)
    } catch (err) {
      setMessage({ ok: false, text: (err as Error).message })
    } finally {
      setBusy(false)
    }
  }

  function upload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) run(() => api.uploadCv(file), `Läste in ${file.name}. Kontrollera texten nedan.`)
  }

  return (
    <div className="card form">
      <h2>Mitt CV</h2>
      <p className="muted">
        Agenterna använder bara det som står här, så se till att erfarenheter, utbildning och kontaktuppgifter är med.
        {profile && <> Senast sparat {new Date(profile.updatedAt).toLocaleString('sv-SE')}{profile.fileName && <> från {profile.fileName}</>}.</>}
      </p>
      <label className="upload">
        <span className="button secondary">{busy ? 'Läser…' : 'Ladda upp CV (PDF, Word eller text)'}</span>
        <input type="file" accept=".pdf,.docx,.txt,.md" onChange={upload} disabled={busy} hidden />
      </label>
      <label>
        Eller klistra in texten
        <textarea rows={16} value={text} onChange={e => setText(e.target.value)} placeholder="Namn, kontaktuppgifter, erfarenhet, utbildning, kompetenser…" />
      </label>
      <fieldset>
        <legend>Kontaktuppgifter i brevhuvudet (Word och PDF)</legend>
        <div className="row">
          {CONTACT_FIELDS.map(f => (
            <label key={f.key}>
              {f.label}
              <input type={f.type} value={contact[f.key] ?? ''} placeholder={f.placeholder}
                onChange={e => setContact(c => ({ ...c, [f.key]: e.target.value }))} />
            </label>
          ))}
        </div>
      </fieldset>
      <div className="actions start">
        <button disabled={busy || text.trim().length < 50 || (text === profile?.cvText && CONTACT_FIELDS.every(f => (contact[f.key] ?? '') === (profile?.[f.key] ?? '')))}
          onClick={() => run(() => api.saveCv(text, contact), 'CV:t är sparat.')}>
          Spara CV
        </button>
        {message && <span className={message.ok ? 'ok' : 'error'}>{message.text}</span>}
      </div>
    </div>
  )
}
