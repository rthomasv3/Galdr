import { createWebHashHistory, createRouter } from 'vue-router'

const DashboardView = () => import('./views/DashboardView.vue')

const routes = [
  {
    path: '/',
    name: 'dashboard',
    component: DashboardView,
  },
]

const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

export default router
