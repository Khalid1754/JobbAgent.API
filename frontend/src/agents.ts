import type { RunKind, Step } from './api'

export interface Agent {
  name: string
  icon: string
  label: string
}

export const AGENTS: Record<RunKind, Agent[]> = {
  search: [
    { name: 'Sökare', icon: '🔎', label: 'Söker i Platsbanken' },
    { name: 'Matchare', icon: '🎯', label: 'Jämför med ditt CV' },
  ],
  letter: [
    { name: 'Tolkare', icon: '📄', label: 'Läser annonsen' },
    { name: 'Matchare', icon: '🎯', label: 'Jämför med ditt CV' },
    { name: 'Skribent', icon: '✍️', label: 'Skriver brevet' },
    { name: 'Granskare', icon: '✅', label: 'Granskar' },
  ],
  interview: [
    { name: 'Tolkare', icon: '📄', label: 'Läser annonsen' },
    { name: 'Matchare', icon: '🎯', label: 'Hittar styrkor och luckor' },
    { name: 'Intervjucoach', icon: '🗣️', label: 'Förbereder frågor och svar' },
  ],
}

export type AgentState = 'waiting' | 'working' | 'done' | 'failed'

const ICONS: Record<string, string> = Object.fromEntries(Object.values(AGENTS).flat().map(a => [a.name, a.icon]))
export const KIND_ICON: Record<RunKind, string> = { search: '🔎', letter: '✉️', interview: '🗣️' }
export const iconFor = (agent: string) => ICONS[agent] ?? '🤖'

// En agents läge avgörs av det senaste steget den själv gjorde.
export function agentStates(kind: RunKind, steps: Step[]): Record<string, AgentState> {
  const states: Record<string, AgentState> = Object.fromEntries(AGENTS[kind].map(a => [a.name, 'waiting']))
  for (const step of [...steps].sort((a, b) => a.sequence - b.sequence)) {
    states[step.agent] = step.kind === 'started' ? 'working' : step.kind === 'failed' ? 'failed' : 'done'
  }
  return states
}

// Färg på matchningspoängen: grön för stark, gul för möjlig, grå för svag.
export const scoreClass = (score: number) => (score >= 70 ? 'high' : score >= 40 ? 'mid' : 'low')
