import { useState } from 'react'
import { api, type Application, type NewApplication, type Run, type Stage } from '../api'
import { STAGES } from '../board'
import { useLang } from '../i18n'
import Score from './Score'

interface Props {
  run: Run
  application?: Application
  onSaveJob: (job: NewApplication) => Promise<void>
  onPrepareInterview: () => Promise<void>
}

export default function LetterView({ run, application, onSaveJob, onPrepareInterview }: Props) {
  const { t } = useLang()
  const [letter, setLetter] = useState(run.letter ?? '')
  const [saved, setSaved] = useState(run.letter ?? '')
  const [message, setMessage] = useState<string | null>(null)
  const match = run.match

  async function save() {
    try {
      await api.saveLetter(run.id, letter)
      setSaved(letter)
      flash(t('Sparat!'))
    } catch (err) {
      flash((err as Error).message)
    }
  }

  async function copy() {
    await navigator.clipboard.writeText(letter)
    flash(t('Kopierat!'))
  }

  // Word och PDF skapas av servern från det sparade brevet, så ändringar sparas först.
  async function download(format: 'docx' | 'pdf' | 'txt') {
    if (format === 'txt') {
      const url = URL.createObjectURL(new Blob([letter], { type: 'text/plain;charset=utf-8' }))
      const name = `${run.language === 'en' ? 'Cover letter' : 'Personligt brev'} - ${run.company ?? run.title}.txt`.replace(/[\\/:*?"<>|]/g, '')
      Object.assign(document.createElement('a'), { href: url, download: name }).click()
      URL.revokeObjectURL(url)
      return
    }
    try {
      if (letter !== saved) {
        await api.saveLetter(run.id, letter)
        setSaved(letter)
      }
      Object.assign(document.createElement('a'), { href: api.letterFileUrl(run.id, format) }).click()
    } catch (err) {
      flash((err as Error).message)
    }
  }

  async function track(stage: Stage) {
    try {
      await onSaveJob({ letterRunId: run.id, stage })
      flash(stage === 'applied' ? t('Markerat som sökt på tavlan!') : t('Sparat på tavlan!'))
    } catch (err) {
      flash((err as Error).message)
    }
  }

  function flash(text: string) {
    setMessage(text)
    setTimeout(() => setMessage(null), 2000)
  }

  return (
    <>
      {match && (
        <div className="card match">
          <Score value={match.score} large />
          <div>
            <p>{match.summary}</p>
            <div className="columns">
              <div>
                <h3>{t('Det här passar')}</h3>
                <ul>{match.matches.map(m => <li key={m.requirement}><b>{m.requirement}:</b> {m.evidence}</li>)}</ul>
              </div>
              {match.gaps.length > 0 && (
                <div>
                  <h3>{t('Det här saknas')}</h3>
                  <ul>{match.gaps.map(g => <li key={g}>{g}</li>)}</ul>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {match?.cvTips && match.cvTips.length > 0 && (
        <div className="card">
          <h3>{t('Anpassa ditt CV för jobbet')}</h3>
          <p className="muted">{t('Matcharens förslag bygger bara på det som redan står i ditt CV.')}</p>
          <ul className="tips">
            {match.cvTips.map(tip => (
              <li key={tip.section + tip.suggestion}>
                <span className="chip-label">{tip.section}</span> {tip.suggestion}
                {tip.example && (
                  <div className="example">
                    <q>{tip.example}</q>
                    <button className="secondary small" onClick={() => navigator.clipboard.writeText(tip.example).then(() => flash(t('Kopierat!')))}>{t('Kopiera')}</button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="card">
        <div className="actions">
          <h3>{t('Personligt brev')}</h3>
          {message && <span className="ok">{message}</span>}
          <button className="secondary" disabled={letter === saved || letter.trim().length === 0} onClick={save}>{t('Spara ändringar')}</button>
          <button className="secondary" onClick={copy}>{t('Kopiera')}</button>
          <button className="secondary" onClick={() => download('docx')} title={t('Med namn och kontaktuppgifter överst')}>Word</button>
          <button className="secondary" onClick={() => download('pdf')} title={t('Med namn och kontaktuppgifter överst')}>PDF</button>
          <button className="secondary" onClick={() => download('txt')}>{t('Text')}</button>
        </div>
        <textarea className="letter" rows={22} value={letter} onChange={e => setLetter(e.target.value)} />
        <p className="meta">{t('Läs alltid igenom brevet och gör det till ditt eget innan du skickar det.')}</p>
        <div className="actions start">
          {application ? (
            <span className="muted">{t('På ansökningstavlan:')} {STAGES.find(s => s.id === application.stage)?.icon} {t(STAGES.find(s => s.id === application.stage)?.label ?? '')}</span>
          ) : (
            <>
              <button onClick={() => track('applied')}>📨 {t('Jag har sökt jobbet')}</button>
              <button className="secondary" onClick={() => track('saved')}>⭐ {t('Spara på tavlan')}</button>
            </>
          )}
          <button className="secondary" onClick={() => onPrepareInterview().catch(err => flash((err as Error).message))}>🗣️ {t('Förbered intervju')}</button>
        </div>
      </div>

      {run.adText && (
        <div className="card">
          <details>
            <summary><b>{t('Annonsen')}</b>{run.adUrl && <> · <a href={run.adUrl} target="_blank" rel="noreferrer">{t('öppna originalet')}</a></>}</summary>
            <p className="ad">{run.adText}</p>
          </details>
        </div>
      )}
    </>
  )
}
