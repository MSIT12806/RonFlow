import { createApp } from 'vue'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
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
app.use(ToastService)

app.mount('#app')
