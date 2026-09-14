import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/', name: 'dashboard', component: () => import('./views/DashboardView.vue') },
  { path: '/jobs', name: 'jobs', component: () => import('./views/JobsView.vue') },
  { path: '/pipeline', name: 'pipeline', component: () => import('./views/PipelineView.vue') },
  {
    path: '/tailor/:jobId',
    name: 'tailor',
    component: () => import('./views/TailoringView.vue'),
    props: true,
  },
  { path: '/resumes', name: 'resumes', component: () => import('./views/ResumesView.vue') },
  { path: '/settings', name: 'settings', component: () => import('./views/SettingsView.vue') },
]

export default createRouter({
  history: createWebHistory(),
  routes,
})
