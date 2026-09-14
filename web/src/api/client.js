import axios from 'axios'

// Vite's dev proxy forwards /api to the ASP.NET backend; in production the
// same origin serves both, so a relative base works in both modes.
const http = axios.create({ baseURL: '/api' })

// The exception-handling middleware on the backend always returns
// { message }; surface that instead of axios's generic "Request failed".
http.interceptors.response.use(
  (res) => res,
  (err) => {
    const message = err.response?.data?.message || err.message || 'Request failed'
    return Promise.reject(new Error(message))
  },
)

export const api = {
  // ---- Profiles ----------------------------------------------------------
  listProfiles: () => http.get('/profiles').then((r) => r.data),
  getProfile: (id) => http.get(`/profiles/${id}`).then((r) => r.data),
  createProfile: (dto) => http.post('/profiles', dto).then((r) => r.data),
  updateProfile: (id, dto) => http.put(`/profiles/${id}`, dto).then((r) => r.data),
  deleteProfile: (id) => http.delete(`/profiles/${id}`),

  // ---- Jobs ----------------------------------------------------------
  listJobs: (params) => http.get('/jobs', { params }).then((r) => r.data),
  getJob: (id, profileId) => http.get(`/jobs/${id}`, { params: { profileId } }).then((r) => r.data),
  getJobDescription: (id) => http.get(`/jobs/${id}/description`).then((r) => r.data),
  importUrl: (url) => http.post('/jobs/import/url', { url }).then((r) => r.data),
  importText: (text, url) => http.post('/jobs/import/text', { text, url }).then((r) => r.data),
  deepAnalyse: (jobId, profileId) =>
    http.post(`/jobs/${jobId}/deep-analyse`, { profileId }).then((r) => r.data),
  setWorkModel: (jobId, workModel, officeDaysPerWeek) =>
    http.put(`/jobs/${jobId}/work-model`, { workModel, officeDaysPerWeek }),
  dismissJob: (jobId) => http.post(`/jobs/${jobId}/dismiss`),

  // ---- Resumes ----------------------------------------------------------
  listResumes: () => http.get('/resumes').then((r) => r.data),
  getResume: (id) => http.get(`/resumes/${id}`).then((r) => r.data),
  uploadResume: (file, isCoverLetter) => {
    const form = new FormData()
    form.append('file', file)
    return http
      .post('/resumes/upload', form, { params: { isCoverLetter } })
      .then((r) => r.data)
  },
  setMasterResume: (id) => http.post(`/resumes/${id}/set-master`).then((r) => r.data),
  deleteResume: (id) => http.delete(`/resumes/${id}`),

  // ---- Tailoring ----------------------------------------------------------
  createTailoring: (jobId, resumeId, includeCoverLetter) =>
    http.post('/tailoring', { jobId, resumeId, includeCoverLetter }).then((r) => r.data),
  getTailoring: (id) => http.get(`/tailoring/${id}`).then((r) => r.data),
  tailoringByJob: (jobId) => http.get(`/tailoring/by-job/${jobId}`).then((r) => r.data),
  decideSuggestion: (suggestionId, accepted, editedText) =>
    http.put(`/tailoring/suggestions/${suggestionId}`, { accepted, editedText }).then((r) => r.data),
  buildTailoredResume: (sessionId) => http.post(`/tailoring/${sessionId}/build`).then((r) => r.data),
  exportResumeUrl: (resumeId, format) => `/api/tailoring/resumes/${resumeId}/export?format=${format}`,

  // ---- Pipeline ----------------------------------------------------------
  board: (profileId) => http.get('/pipeline/board', { params: { profileId } }).then((r) => r.data),
  trackJob: (jobId, stage) => http.post(`/pipeline/jobs/${jobId}/track`, { stage }).then((r) => r.data),
  moveCard: (applicationId, stage, sortOrder, note) =>
    http.put(`/pipeline/${applicationId}/move`, { stage, sortOrder, note }),
  updateCard: (applicationId, patch) => http.put(`/pipeline/${applicationId}`, patch),
  removeCard: (applicationId) => http.delete(`/pipeline/${applicationId}`),

  // ---- Dashboard / ingestion ----------------------------------------------------------
  summary: (profileId) => http.get('/dashboard/summary', { params: { profileId } }).then((r) => r.data),
  runIngestion: () => http.post('/ingestion/run').then((r) => r.data),
  runTriage: () => http.post('/ingestion/triage').then((r) => r.data),
  spend: () => http.get('/ingestion/spend').then((r) => r.data),
}
