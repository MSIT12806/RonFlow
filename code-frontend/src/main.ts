import { createApp } from 'vue'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import './style.css'
import 'primeicons/primeicons.css'
import App from './App.vue'

const app = createApp(App)

app.use(PrimeVue, {
	theme: {
		preset: Aura,
	},
})

app.mount('#app')
