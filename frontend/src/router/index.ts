import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import TransitView from '@/views/TransitView.vue'
import FlightsView from '@/views/FlightsView.vue'
import WeatherView from '@/views/WeatherView.vue'
import ModulePlaceholderView from '@/views/ModulePlaceholderView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomeView, meta: { title: '首頁' } },
    { path: '/transit', name: 'transit', component: TransitView, meta: { title: '大眾運輸' } },
    { path: '/flights', name: 'flights', component: FlightsView, meta: { title: '航班查詢' } },
    {
      path: '/weather',
      name: 'weather',
      component: WeatherView,
      meta: { title: '天氣' },
    },
    {
      path: '/alerts',
      name: 'alerts',
      component: ModulePlaceholderView,
      props: { module: 'alerts' },
      meta: { title: '旅遊警示' },
    },
    {
      path: '/emergency',
      name: 'emergency',
      component: ModulePlaceholderView,
      props: { module: 'emergency' },
      meta: { title: '應急資訊' },
    },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.afterEach((to) => {
  document.title = `${String(to.meta.title ?? '旅途通')}｜旅途通`
})

export default router
