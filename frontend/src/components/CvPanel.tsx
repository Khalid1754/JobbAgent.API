import { useEffect, useState, type ChangeEvent } from 'react'
import { api, type Profile } from '../api'

// Ditt CV: ladda upp en fil eller klistra in texten. Agenterna läser det vid varje körning.
export default function CvPanel({ onSaved }: { onSaved: (profile: Profile) => void }) {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  useEffect(() => {
    api.profile().then(p => {
      setProfile(p)
      setText(p?.cvText ?? '')
    }, () => {})
  }, [])

  async function run(action: () => Promise<Profile>, done: string) {
    setBusy(true)
    setMessage(null)
    try {
      const saved = await action()
      setProfile(saved)
      setText(saved.cvText)
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
      <div className="actions start">
        <button disabled={busy || text.trim().length < 50 || text === profile?.cvText} onClick={() => run(() => api.saveCv(text), 'CV:t är sparat.')}>
          Spara CV
        </button>
        {message && <span className={message.ok ? 'ok' : 'error'}>{message.text}</span>}
      </div>
    </div>
  )
}
