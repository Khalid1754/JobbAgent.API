import { describe, expect, it } from 'vitest'
import type { Step } from './api'
import { agentStates, scoreClass } from './agents'
import { applyEvent, emptyRun } from './runState'

const step = (sequence: number, agent: string, kind: Step['kind'], toAgent?: string): Step =>
  ({ sequence, agent, kind, toAgent, summary: '', createdAt: '' })

describe('applyEvent', () => {
  it('sorterar steg och hoppar över dubbletter', () => {
    let state = applyEvent(emptyRun, { type: 'step', step: step(2, 'Tolkare', 'completed') })
    state = applyEvent(state, { type: 'step', step: step(1, 'Tolkare', 'started') })
    state = applyEvent(state, { type: 'step', step: step(2, 'Tolkare', 'completed') })
    expect(state.steps.map(s => s.sequence)).toEqual([1, 2])
  })

  it('sparar status och fel', () => {
    const state = applyEvent(emptyRun, { type: 'status', status: 'failed', error: 'Lägg in ditt CV först.' })
    expect(state).toMatchObject({ status: 'failed', error: 'Lägg in ditt CV först.' })
  })
})

describe('agentStates', () => {
  it('visar vem som jobbar och vem som är klar', () => {
    const states = agentStates('letter', [
      step(1, 'Tolkare', 'started'),
      step(2, 'Tolkare', 'completed'),
      step(3, 'Tolkare', 'handoff', 'Matchare'),
      step(4, 'Matchare', 'started'),
    ])
    expect(states).toEqual({ Tolkare: 'done', Matchare: 'working', Skribent: 'waiting', Granskare: 'waiting' })
  })

  it('Skribenten jobbar igen när Granskaren skickar tillbaka brevet', () => {
    const states = agentStates('letter', [
      step(1, 'Skribent', 'completed'),
      step(2, 'Granskare', 'handoff', 'Skribent'),
      step(3, 'Skribent', 'started'),
    ])
    expect(states.Skribent).toBe('working')
    expect(states.Granskare).toBe('done')
  })

  it('sökningen har bara två agenter', () => {
    expect(Object.keys(agentStates('search', []))).toEqual(['Sökare', 'Matchare'])
  })
})

describe('scoreClass', () => {
  it('delar in poängen i tre nivåer', () => {
    expect([85, 70, 69, 40, 39].map(scoreClass)).toEqual(['high', 'high', 'mid', 'mid', 'low'])
  })
})
