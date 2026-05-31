# ronflow 操作說明

## 帳號密碼
- UserName: copilot-dogfood
- Email: copilot-dogfood@localhost.test
- Password: Dogfood#2026!
- RonAuth Login URL: http://localhost/ronauth-api/api/auth/login
- RonFlow URL: http://localhost/

Purpose: local IIS dogfooding account for Copilot-driven verification.

This file is intentionally local-only and matches the repo ignore rule `*.local`.

## AI 接入入口
你現在是一個要接入 RonFlow 的 AI 操作代理。

這份文件只提供登入與啟動入口。RonFlow 的能力、欄位、工作順序、錯誤恢復、project access、checklist 與 apply 規則，都應在登入後由 RonFlow 系統內的 AI-facing contract 漸進式查詢，不以這份文件自行推論。

已知入口：

- RonAuth API base URL: http://localhost/ronauth-api/api/auth
- RonFlow API base URL: http://localhost/ronflow-api/api
- RonFlow AI bootstrap: http://localhost/ronflow-api/api/ai/bootstrap
- 人類聯絡規則: {HUMAN_CONTACT_RULE}
- Project access 規則: {PROJECT_ACCESS_RULE}

## 啟動流程
1. 若你還沒有 RonFlow 帳號，呼叫 `POST http://localhost/ronauth-api/api/auth/register` 建立帳號。
2. 註冊成功後，不要假設自己已能進入任何 project；請回報下列訊息並等待人類完成 access：

```text
RonFlow registration ready
userName: <your-user-name>
email: <your-email>
next_action_needed_from_human: please add me to the correct project or invite me to the target project.
```

3. 若你已有帳號，呼叫 `POST http://localhost/ronauth-api/api/auth/login` 取得 access token。
4. 建立一個新的 RonFlow session id，之後所有 RonFlow API 請求都必須帶：

```text
Authorization: Bearer <access-token>
X-RonFlow-Session-Id: <session-id>
```

5. 呼叫 `POST http://localhost/ronflow-api/api/session/activate` 啟用 session。
6. 呼叫 `GET http://localhost/ronflow-api/api/ai/bootstrap` 作為最小健康檢查與下一步入口。

## 後續規則
- bootstrap 成功後，依 RonFlow 系統回傳的 next steps 逐步查詢 capabilities、glossary、workflow guidance、summary 與 apply contract。
- 如果 login、session activation 或 bootstrap 失敗，先停止，不要繼續呼叫 summary、apply 或其他寫入操作。
- 如果系統 contract 與本文件描述不一致，以系統 contract 為準。
- 若系統 contract 要求詢問人類，或你無法判斷人類意圖，先停止並詢問人類。
