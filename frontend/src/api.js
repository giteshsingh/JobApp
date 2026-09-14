const BASE = '/api'

async function request(method, path, { params, body, raw } = {}) {
  let url = BASE + path
  if (params) {
    const qs = new URLSearchParams()
    for (const [k, v] of Object.entries(params)) {
      if (v === undefined || v === null || v === '') continue
      qs.set(k, String(v))
    }
    const s = qs.toString()
    if (s) url += `?${s}`
  }

  const init = { method, headers: {} }
  if (body instanceof FormData) {
    init.body = body
  } else if (body !== undefined) {
    init.headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  const res = await fetch(url, init)
  if (!res.ok) {
    let message = `${method} ${path} failed (${res.status})`
    try {
      const data = await res.json()
      message = data?.message || message
    } catch {
      /* body wasn't JSON */
    }
    throw new Error(message)
  }

  if (raw) return res
  if (res.status === 204) return null
  const text = await res.text()
  return text ? JSON.parse(text) : null
}

export const api = {
  dashboard: {
    summary: (profileId) => request('GET', '/dashboard/summary', { params: { profileId } }),
  },

  jobs: {
    list: (query) => request('GET', '/jobs', { params: query }),
    get: (id, profileId) => request('GET', `/jobs/${id}`, { params: { profileId } }),
    description: (id) => request('GET', `/jobs/${id}/description`),
    importUrl: (url) => request('POST', '/jobs/import/url', { body: { url } }),
    importText: (text, url) => request('POST', '/jobs/import/text', { body: { text, url } }),
    deepAnalyse: (id, profileId) => request('POST', `/jobs/${id}/deep-analyse`, { body: { profileId } }),
    setWorkModel: (id, workModel, officeDaysPerWeek) =>
      request('PUT', `/jobs/${id}/work-model`, { body: { workModel, officeDaysPerWeek } }),
    dismiss: (id) => request('POST', `/jobs/${id}/dismiss`),
  },

  resumes: {
    list: () => request('GET', '/resumes'),
    get: (id) => request('GET', `/resumes/${id}`),
    upload: (file, isCoverLetter) => {
      const form = new FormData()
      form.append('file', file)
      return request('POST', '/resumes/upload', { params: { isCoverLetter }, body: form })
    },
    setMaster: (id) => request('POST', `/resumes/${id}/set-master`),
    remove: (id) => request('DELETE', `/resumes/${id}`),
  },

  tailoring: {
    create: (jobId, resumeId, includeCoverLetter) =>
      request('POST', '/tailoring', { body: { jobId, resumeId, includeCoverLetter } }),
    get: (id) => request('GET', `/tailoring/${id}`),
    byJob: (jobId) => request('GET', `/tailoring/by-job/${jobId}`),
    decide: (suggestionId, accepted, editedText) =>
      request('PUT', `/tailoring/suggestions/${suggestionId}`, { body: { accepted, editedText } }),
    build: (id) => request('POST', `/tailoring/${id}/build`),
    exportUrl: (resumeId, format) => `${BASE}/tailoring/resumes/${resumeId}/export?format=${format}`,
  },

  pipeline: {
    board: (profileId) => request('GET', '/pipeline/board', { params: { profileId } }),
    track: (jobId, stage) => request('POST', `/pipeline/jobs/${jobId}/track`, { body: { stage } }),
    move: (applicationId, stage, sortOrder, note) =>
      request('PUT', `/pipeline/${applicationId}/move`, { body: { stage, sortOrder, note } }),
    update: (applicationId, patch) => request('PUT', `/pipeline/${applicationId}`, { body: patch }),
    remove: (applicationId) => request('DELETE', `/pipeline/${applicationId}`),
  },

  profiles: {
    list: () => request('GET', '/profiles'),
    get: (id) => request('GET', `/profiles/${id}`),
    create: (dto) => request('POST', '/profiles', { body: dto }),
    update: (id, dto) => request('PUT', `/profiles/${id}`, { body: dto }),
    remove: (id) => request('DELETE', `/profiles/${id}`),
  },

  ingestion: {
    run: () => request('POST', '/ingestion/run'),
    triage: () => request('POST', '/ingestion/triage'),
    spend: () => request('GET', '/ingestion/spend'),
  },
}
