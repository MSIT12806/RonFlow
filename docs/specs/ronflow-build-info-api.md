# RonFlow Build Info API

## 1. 目的

RonFlow 需要提供一個可直接查詢的 build info API，讓開發者或部署流程可以快速確認目前正在執行的 API 是否已更新到最新部署版本。

這個 API 的用途不是取代完整的健康檢查，而是提供一個低成本、可比對、可觀察的部署訊號。

## 2. 端點

- `GET /api/system/build-info`

## 3. 行為

1. 此端點不需要登入即可讀取。
2. 回應應至少包含：
   - `application`
   - `environmentName`
   - `version`
   - `informationalVersion`
   - `updatedAtUtc`
3. `version` 應能代表目前部署中的 build 版本；若 deployment pipeline 有提供明確 build version，應優先回傳該值。
4. `updatedAtUtc` 應代表目前部署版本的 UTC 更新時間；若 deployment pipeline 有提供明確部署時間，應優先回傳該值。
5. 若沒有額外 deployment metadata，系統至少仍應回傳 assembly 可推得的版本與更新時間，避免端點失效。

## 4. 驗收

1. 匿名呼叫 `GET /api/system/build-info` 時，系統回傳 `200 OK`。
2. 回應中的 `version` 不為空字串。
3. 回應中的 `updatedAtUtc` 為有效的 UTC 時間。
4. 本機重新部署後，若 pipeline 或部署腳本有寫入新的 deployment metadata，端點值應能反映新的部署結果。