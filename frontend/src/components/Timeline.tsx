import type { Step } from '../api'
import { iconFor } from '../agents'
import { useLang } from '../i18n'
import Markdown from './Markdown'

const time = (iso: string, locale: string) => new Date(iso).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' })

// Allt som hänt i körningen. Överlämningar visas som pilar mellan agenterna.
export default function Timeline({ steps }: { steps: Step[] }) {
  const { t, locale } = useLang()
  return (
    <ol className="timeline">
      {steps.map(step => (
        <li key={step.sequence} className={step.kind}>
          <span className="icon">{step.kind === 'handoff' ? '↪' : iconFor(step.agent)}</span>
          <div>
            <div>
              <b>{t(step.agent)}</b>
              {step.toAgent && <> → <b>{t(step.toAgent)}</b></>}
              <span className="meta"> · {time(step.createdAt, locale)}</span>
            </div>
            {step.detail ? (
              <details>
                <summary>{step.summary}</summary>
                <Markdown>{step.detail}</Markdown>
              </details>
            ) : (
              <div>{step.summary}</div>
            )}
          </div>
        </li>
      ))}
    </ol>
  )
}
