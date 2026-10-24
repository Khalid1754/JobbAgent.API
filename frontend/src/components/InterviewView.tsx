import { useState } from 'react'
import type { InterviewPrep } from '../api'
import { useLang } from '../i18n'

// Intervjucoachens förberedelse: troliga frågor med tips och exempelsvar, frågor att ställa och luckor.
export default function InterviewView({ prep }: { prep: InterviewPrep }) {
  const { t } = useLang()
  const [practice, setPractice] = useState(false)

  return (
    <>
      <div className="card">
        <div className="actions">
          <h3>{t('Inför intervjun')}</h3>
          <label className="check">
            <input type="checkbox" checked={practice} onChange={e => setPractice(e.target.checked)} />
            {t('Övningsläge (dölj svaren)')}
          </label>
        </div>
        <p>{prep.summary}</p>
        <ol className="questions">
          {prep.questions.map(q => (
            <li key={q.question}>
              <details open={!practice}>
                <summary>
                  <span className="chip-label">{q.category}</span> <b>{q.question}</b>
                </summary>
                <p className="muted">{q.whyTheyAsk}</p>
                <ul>{q.tips.map(tip => <li key={tip}>{tip}</li>)}</ul>
                <blockquote>{q.exampleAnswer}</blockquote>
              </details>
            </li>
          ))}
        </ol>
      </div>

      <div className="card columns">
        <div>
          <h3>{t('Frågor du kan ställa')}</h3>
          <ul>{prep.questionsToAsk.map(q => <li key={q}>{q}</li>)}</ul>
        </div>
        {prep.gaps.length > 0 && (
          <div>
            <h3>{t('Om de frågar om det som saknas')}</h3>
            <ul>{prep.gaps.map(g => <li key={g.gap}><b>{g.gap}:</b> {g.howToHandle}</li>)}</ul>
          </div>
        )}
      </div>
    </>
  )
}
