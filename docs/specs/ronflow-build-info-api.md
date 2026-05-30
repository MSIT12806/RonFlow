# RonFlow Deployment Summary API

## 1. 目的

RonFlow 需要提供一個可直接查詢的 deployment summary API，讓 AI、開發者或部署流程可以一次確認目前 localhost 上正在提供服務的 frontend、RonFlow API 與 RonAuth API 分別是哪一個部署版本。

這個 API 的用途不是取代完整的健康檢查，而是提供一個低成本、machine-readable、可比對的部署訊號，避免呼叫端自行向多個 surface 拼湊版本資訊。

## 2. 端點

- `GET /api/system/build-info`

## 3. 行為

1. 此端點不需要登入即可讀取。
2. 回應應是一份 deployment summary，而不是只描述單一 backend service。
3. 回應應至少包含：
   - `environment`
   - `frontend`
   - `ronflowApi`
   - `ronauthApi`
   - `isSameDeployment`
4. `frontend`、`ronflowApi`、`ronauthApi` 每個 component 都應至少包含：
   - `application`
   - `version`
   - `informationalVersion`
   - `updatedAtUtc`
   - `sourceRevision`
5. `version` 應能代表目前部署中的 build 版本；若 deployment pipeline 有提供明確 build version，應優先回傳該值。
6. `updatedAtUtc` 應代表目前部署版本的 UTC 更新時間；若 deployment pipeline 有提供明確部署時間，應優先回傳該值。
7. `frontend`、`ronflowApi`、`ronauthApi` 應優先讀取 deployment script 寫入的 `build-info.json`，避免回傳的資訊只反映目前正在執行的單一 assembly。
8. `ronflowApi` 在缺少 deployment metadata 時，至少仍應 fallback 到 assembly 可推得的版本與更新時間，避免端點失效。
9. `isSameDeployment` 應讓呼叫端可以直接判斷三個 component 是否來自同一次 localhost 部署，而不需要自行比較多個欄位。

## 4. 驗收

1. 匿名呼叫 `GET /api/system/build-info` 時，系統回傳 `200 OK`。
2. 回應中的 `frontend`、`ronflowApi`、`ronauthApi` 都存在，且各自的 `version` 不為空字串。
3. 三個 component 的 `updatedAtUtc` 都是有效的 UTC 時間。
4. 回應中的 `isSameDeployment` 為明確布林值，呼叫端不需要自行比較三份版本資訊才能判讀。
5. 本機重新部署後，若部署腳本有寫入新的 deployment metadata，端點值應能反映新的部署結果。