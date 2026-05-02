# RonFlow Frontend

這個目錄是 RonFlow 的前端專案，已使用下列技術完成初始化：

- Vue 3
- TypeScript
- Vite
- Tailwind CSS
- PrimeVue
- Playwright

## 常用指令

安裝相依套件：

```powershell
npm install
```

啟動開發伺服器：

```powershell
npm run dev
```

正式建置：

```powershell
npm run build
```

執行 E2E 測試：

```powershell
npx playwright install chromium
npm run test:e2e
```

開啟 Playwright UI：

```powershell
npm run test:e2e:ui
```

## 目前內容

- RonFlow 首頁骨架
- PrimeVue 主題與元件整合
- Tailwind CSS 樣式管線
- Playwright 首頁驗證測試
