import { currentLang, t } from './i18n'
export type RunStatus = 'queued' | 'running' | 'completed' | 'failed'
export type RunKind = 'search' | 'letter' | 'interview'
export type StepKind = 'started' | 'completed' | 'handoff' | 'failed'
export type Language = 'auto' | 'sv' | 'en'

export type Stage = 'saved' | 'applied' | 'interview' | 'offer' | 'rejected'

export interface Application {
  id: string
  title: string
  company?: string
  location?: string
  url?: string
  platsbankenId?: string
  deadline?: string
  matchScore?: number
  letterRunId?: string
  stage: Stage
  notes?: string
  appliedAt?: string
  interviewAt?: string
  createdAt: string
  stageChangedAt: string
}

export interface NewApplication {
  title?: string
  company?: string
  location?: string
  url?: string
  platsbankenId?: string
  deadline?: string
  matchScore?: number
  letterRunId?: string
  stage?: Stage
}

export interface Contact {
  fullName?: string
  email?: string
  phone?: string
  city?: string
  link?: string
}

export interface Cv extends Contact {
  id: number
  name: string
  isDefault: boolean
  cvText: string
  fileName?: string
  updatedAt: string
}

export interface Step {
  sequence: number
  agent: string
  toAgent?: string
  kind: StepKind
  summary: string
  detail?: string
  createdAt: string
}

export interface RunSummary {
  id: string
  kind: RunKind
  title: string
  company?: string
  status: RunStatus
  matchScore?: number
  sourceRunId?: string
  applicationId?: string
  watchId?: string
  createdAt: string
  finishedAt?: string
}

export interface SearchHit {
  id: string
  title: string
  employer?: string
  municipality?: string
  url: string
  deadline?: string
  score: number
  reason: string
  isNew?: boolean
}

export interface Watch {
  id: string
  title: string
  location?: string
  keywords?: string
  remoteOnly: boolean
  minScore: number
  enabled: boolean
  cvId?: number
  lastRunAt?: string
  lastRunId?: string
  newMatches: number
}

export interface SaveWatch {
  location?: string
  keywords?: string
  remoteOnly: boolean
  minScore?: number
  enabled?: boolean
  cvId?: number
}

export interface Requirements {
  title: string
  company: string
  location: string
  summary: string
  mustHave: string[]
  niceToHave: string[]
  responsibilities: string[]
  traits: string[]
  contactPerson: string
  language: string
}

export interface Match {
  score: number
  summary: string
  matches: { requirement: string; evidence: string }[]
  gaps: string[]
  highlights: string[]
  cvTips?: { section: string; suggestion: string; example: string }[]
}

export interface InterviewQuestion {
  category: string
  question: string
  whyTheyAsk: string
  tips: string[]
  exampleAnswer: string
}

export interface InterviewPrep {
  summary: string
  questions: InterviewQuestion[]
  questionsToAsk: string[]
  gaps: { gap: string; howToHandle: string }[]
}

export interface Run extends RunSummary {
  location?: string
  keywords?: string
  remoteOnly: boolean
  adUrl?: string
  adText?: string
  platsbankenId?: string
  language: Language
  cvId?: number
  hits?: SearchHit[]
  requirements?: Requirements
  match?: Match
  letter?: string
  interview?: InterviewPrep
  error?: string
  steps: Step[]
}

export type RunEvent =
  | { type: 'step'; step: Step }
  | { type: 'status'; status: RunStatus; error?: string }

export interface NewSearch {
  location?: string
  keywords?: string
  remoteOnly: boolean
  cvId?: number
}

export interface NewLetter {
  adText?: string
  adUrl?: string
  platsbankenId?: string
  language?: Language
  cvId?: number
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body instanceof FormData
      ? { 'Accept-Language': currentLang(), ...init.headers }
      : { 'Content-Type': 'application/json', 'Accept-Language': currentLang(), ...init?.headers },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? t('Servern svarade {status}', { status: response.status }))
  }
  return response.status === 204 ? (undefined as T) : response.json()
}

const json = (method: string, body: unknown): RequestInit => ({ method, body: JSON.stringify(body) })

export const api = {
  status: () => request<{ aiEnabled: boolean; hasCv: boolean }>('/api/status'),
  listCvs: () => request<Cv[]>('/api/cvs'),
  createCv: (cv: Contact & { name: string; cvText: string }) => request<Cv>('/api/cvs', json('POST', cv)),
  updateCv: (cv: Cv) => request<Cv>(`/api/cvs/${cv.id}`, json('PUT', cv)),
  // Utan id blir filen ett nytt CV, annars ersätter den texten i det CV:t.
  uploadCv: (file: File, id?: number) => {
    const form = new FormData()
    form.append('file', file)
    return request<Cv>(id ? `/api/cvs/${id}/upload` : '/api/cvs/upload', { method: 'POST', body: form })
  },
  setDefaultCv: (id: number) => request<void>(`/api/cvs/${id}/default`, { method: 'POST' }),
  deleteCv: (id: number) => request<void>(`/api/cvs/${id}`, { method: 'DELETE' }),
  listRuns: () => request<RunSummary[]>('/api/runs'),
  getRun: (id: string) => request<Run>(`/api/runs/${id}`),
  search: (search: NewSearch) => request<RunSummary>('/api/searches', json('POST', search)),
  createLetter: (letter: NewLetter) => request<RunSummary>('/api/letters', json('POST', letter)),
  createInterview: (from: { letterRunId?: string; applicationId?: string; cvId?: number }) => request<RunSummary>('/api/interviews', json('POST', from)),
  letterFileUrl: (id: string, format: 'docx' | 'pdf') => `/api/runs/${id}/letter.${format}`,
  saveLetter: (id: string, letter: string) => request<void>(`/api/runs/${id}/letter`, json('PUT', { letter })),
  deleteRun: (id: string) => request<void>(`/api/runs/${id}`, { method: 'DELETE' }),
  eventsUrl: (id: string) => `/api/runs/${id}/events`,
  listWatches: () => request<Watch[]>('/api/watches'),
  createWatch: (w: SaveWatch) => request<Watch>('/api/watches', json('POST', w)),
  updateWatch: (id: string, w: SaveWatch) => request<Watch>(`/api/watches/${id}`, json('PUT', w)),
  runWatch: (id: string) => request<RunSummary>(`/api/watches/${id}/run`, { method: 'POST' }),
  markWatchSeen: (id: string) => request<void>(`/api/watches/${id}/seen`, { method: 'POST' }),
  deleteWatch: (id: string) => request<void>(`/api/watches/${id}`, { method: 'DELETE' }),
  listApplications: () => request<Application[]>('/api/applications'),
  createApplication: (a: NewApplication) => request<Application>('/api/applications', json('POST', a)),
  updateApplication: (a: Application) => request<Application>(`/api/applications/${a.id}`, json('PUT', a)),
  deleteApplication: (id: string) => request<void>(`/api/applications/${id}`, { method: 'DELETE' }),
}
