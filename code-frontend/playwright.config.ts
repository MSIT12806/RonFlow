import { defineConfig, devices } from '@playwright/test'

const e2eFrontendPort = 4174
const e2eBackendPort = 5079
const e2eBackendApiBaseUrl = `http://127.0.0.1:${e2eBackendPort}/api`

process.env.RONFLOW_E2E_BACKEND_API_BASE_URL = e2eBackendApiBaseUrl

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: 'html',
  use: {
    baseURL: `http://127.0.0.1:${e2eFrontendPort}`,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: `dotnet run --no-launch-profile --project ../code-backend/RonFlow.Api/RonFlow.Api.csproj --urls http://127.0.0.1:${e2eBackendPort}`,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Testing',
        DOTNET_ENVIRONMENT: 'Testing',
      },
      url: `http://127.0.0.1:${e2eBackendPort}/api/projects`,
      reuseExistingServer: false,
    },
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${e2eFrontendPort}`,
      env: {
        ...process.env,
        RONFLOW_API_PROXY_TARGET: `http://127.0.0.1:${e2eBackendPort}`,
      },
      url: `http://127.0.0.1:${e2eFrontendPort}`,
      reuseExistingServer: false,
    },
  ],
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})