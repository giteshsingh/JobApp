import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '../views/DashboardView.vue'
import JobsView from '../views/JobsView.vue'
import PipelineView from '../views/PipelineView.vue'
import ResumesView from '../views/ResumesView.vue'
import SettingsView from '../views/SettingsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/jobs', name: 'jobs', component: JobsView },
    { path: '/pipeline', name: 'pipeline', component: PipelineView },
    { path: '/resumes', name: 'resumes', component: ResumesView },
    { path: '/settings', name: 'settings', component: SettingsView },
  ],
})

export default router
