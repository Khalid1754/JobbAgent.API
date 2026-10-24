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

export interface Profile {
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
}

export interface NewLetter {
  adText?: string
  adUrl?: string
  platsbankenId?: string
  language?: Language
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body instanceof FormData ? init.headers : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `Servern svarade ${response.status}`)
  }
  return response.status === 204 ? (undefined as T) : response.json()
}

const json = (method: string, body: unknown): RequestInit => ({ method, body: JSON.stringify(body) })

export const api = {
  status: () => request<{ aiEnabled: boolean; hasCv: boolean }>('/api/status'),
  profile: () => fetch('/api/profile').then(r => (r.status === 404 ? null : r.json() as Promise<Profile>)),
  saveCv: (cvText: string) => request<Profile>('/api/profile', json('PUT', { cvText })),
  uploadCv: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<Profile>('/api/profile/upload', { method: 'POST', body: form })
  },
  listRuns: () => request<RunSummary[]>('/api/runs'),
  getRun: (id: string) => request<Run>(`/api/runs/${id}`),
  search: (search: NewSearch) => request<RunSummary>('/api/searches', json('POST', search)),
  createLetter: (letter: NewLetter) => request<RunSummary>('/api/letters', json('POST', letter)),
  createInterview: (from: { letterRunId?: string; applicationId?: string }) => request<RunSummary>('/api/interviews', json('POST', from)),
  saveLetter: (id: string, letter: string) => request<void>(`/api/runs/${id}/letter`, json('PUT', { letter })),
  deleteRun: (id: string) => request<void>(`/api/runs/${id}`, { method: 'DELETE' }),
  eventsUrl: (id: string) => `/api/runs/${id}/events`,
  listApplications: () => request<Application[]>('/api/applications'),
  createApplication: (a: NewApplication) => request<Application>('/api/applications', json('POST', a)),
  updateApplication: (a: Application) => request<Application>(`/api/applications/${a.id}`, json('PUT', a)),
  deleteApplication: (id: string) => request<void>(`/api/applications/${id}`, { method: 'DELETE' }),
}
