# RonFlow 多人協作規格釐清

日期：2026-05-23

這份文件用來整理本輪對話中，關於 RonFlow 多人協作、即時同步、lock、presence 與 session 行為的規格釐清結果。

目前定位是暫存的規格釐清紀錄，後續應再回寫到 core spec 與必要的願望清單 / 驗收測試中。

## 已釐清問題

### 1. Project Member 的操作權限

問題：被邀請者接受邀請、成為 Project Member 之後，對 task 相關操作的權限是否應與 Project Owner 相同？

回答：現階段先完全相同即可。

補充整理：目前可理解為，除了 members / invitations 管理之外，Project Member 與 Project Owner 在 project 內的 task / reminder / board 操作權限先視為相同。

### 2. 多人協作的同步定位

問題：多人協作是否需要接近即時同步？

回答：需要接近即時同步，而且這應該是 RonFlow 的技術亮點。

### 3. 文件層級

問題：這些多人協作規則要放在哪一層文件？

回答：直接納入 core spec 的目前行為。

### 4. 哪些區域需要同步

問題：哪些畫面上的哪些變化，必須自動同步？

回答：至少要同步 board + task detail。

補充回答：pending invitations、invitation inbox 這些通知性區塊，也要有即時通知或輪詢更新能力，不能要求使用者手動重新整理才看到新內容。

### 5. 並行操作的總原則

問題：多人同時改同一筆 Task 時，系統應如何處理？

回答：需要有 lock 機制；當某位使用者正在操作該 task 時，其他人不應被允許做衝突操作。

### 6. Drawer 與 edit mode 的關係

問題：什麼情況算是正在操作 task？

回答：平常打開 Task Detail Drawer 應該只是 view mode；要另外有一個 edit 按鈕，明確進入 edit mode 才算進入內容編輯。

補充整理：

- 單純開 drawer 不自動鎖住 task。
- `content edit lock` 只在進入 edit mode 後取得。
- 拖曳 task 時另外取得 `drag lock`。

### 7. Lock 類型與互斥原則

問題：lifecycle 類操作應如何和其他 lock 互動？

回答：採先到先得。

補充整理：

- `content edit lock`、`drag lock`、`lifecycle lock` 是不同類型的 lock。
- 若先取得 `content edit lock`，其他人不能再對同一 task 做 lifecycle 類操作。
- 若先取得 `lifecycle lock`，其他人不能再進入 edit mode。
- `lifecycle lock` 與 `drag lock` 不能並存，例如 task 不能同時被 move to trash、又同時被拖曳。
- `content edit lock` 與 `drag lock` 可以並存。

### 8. Lock 的釋放條件

問題：正常情況下，lock 何時釋放？

回答：明確操作結束就釋放。

補充回答：只靠明確操作結束不足夠，還要另外偵測視窗是否關閉、使用者是否仍在系統中，以及是否長時間沒有活動。

### 9. 活動判定

問題：哪些行為算是「仍有活動」？

回答：只要這個 browser tab 內有滑鼠、鍵盤、捲動等互動，就算有活動。

### 10. 拿不到 lock 的人可以怎麼看

問題：當另一位使用者已持有某筆 task 的 lock 時，後來的人應看到什麼行為？

回答：保留查看，但禁止衝突操作。

補充整理：

- UI 不採顯示滑鼠位置。
- 採 task 外框、隨機顏色標記與 `username` label 呈現「誰正在編輯 / 拖曳這張 task」。
- 被鎖住的 task 在 board 與 task detail 都應即時反映鎖定狀態。

### 11. 未儲存草稿是否共享

問題：當 A 在 edit mode 中尚未儲存時，B 是否應看到未儲存草稿？

回答：只看到鎖定狀態，不看到未儲存草稿。

### 12. 推送與 polling 的分工

問題：board / task detail 與通知型畫面的同步機制應如何分配？

回答：採混合模式。

補充整理：

- `Project Board / Task Detail Drawer` 走即時推送。
- `pending invitations / invitation inbox` 可先用 polling，但對使用者來說必須呈現為自動更新。

### 13. 拖曳中的即時呈現

問題：當 A 正在拖曳某張 task card、但還沒放下前，B 應看到什麼？

回答：只顯示原卡片被鎖定。

補充整理：

- 其他人先看到原欄位中的 task 被反灰、加外框與 `username` 標記。
- 直到 drop 成功，board 才一次更新到新欄位或新順序。

### 14. View mode drawer 的自動更新

問題：如果 A 成功儲存 task，B 此時正開著同一筆 task 的 drawer 但在 view mode，B 應如何更新？

回答：直接自動更新內容。

### 15. Archive / Trash 時的即時行為

問題：如果 A 把 task archive 或 move to trash，而 B 正在看 board 或開著 drawer，B 的畫面應如何更新？

回答：分情境。

補充整理：

- 其他人的 board 應立即移除該 task。
- 若其他人正開著 drawer，drawer 不直接關閉，而是切到對應生命週期的 read-only 狀態。

### 16. Restore 時的即時行為

問題：如果 A 把 task 從 Archived Tasks View 或 Trash View 還原，B 的畫面應如何更新？

回答：

- 看 board 的人：task 立即回到 board。
- 看 archived / trash view 的人：該 task 立即從清單消失。
- 若正開著 drawer：drawer 自動切回 active task 的可編輯 / 可查看狀態，但不強制導頁。

### 17. 邀請相關事件的提示策略

問題：邀請相關事件除了自動更新資料外，是否需要主動提示使用者？

回答：

- Owner 送出邀請後：只自動更新，不提示。
- Invitee 收到新邀請時：自動更新 + badge。
- Invitee 的邀請被 accept / reject / 過期時：自動更新 + 輕量提示。
- Owner 端的 pending invitation 被 accept 時：自動更新 + 輕量提示。

### 18. 因 timeout / 中斷失去 edit lock 的畫面行為

問題：如果使用者在 edit mode 中因 timeout、失焦或中斷而失去 content edit lock，畫面應如何更新？

回答：自動退回 view mode，直接丟棄未儲存草稿，改顯示最新已儲存內容。

### 19. Edit 與 drag 並存時的 drawer 更新

問題：如果 A 正在 edit mode，B 同時拖曳同一筆 task，A 的 drawer 應如何表現？

回答：drawer 內的 `目前狀態` 與 `activity timeline` 直接自動更新。

補充整理：title / description / due date 的未儲存草稿仍不共享。

### 20. 即時推送失效時的降級策略

問題：如果 board / task detail 的即時推送通道暫時中斷，RonFlow 應怎麼做？

回答：自動降級。

補充整理：

- 顯示輕量提示，例如即時同步暫時中斷、正在重新連線。
- 暫時退回 polling。
- 恢復後再切回即時模式。

### 21. Reminder 是否屬於內容編輯

問題：新增 reminder / 刪除 reminder 是否必須先進入 edit mode 並取得 content edit lock？

回答：是。

補充整理：reminder 屬於 task 內容編輯的一部分，必須先進入 edit mode、取得 `content edit lock` 才能新增或刪除。

### 22. Create Task 草稿是否共享

問題：當 A 開啟 Create Task Modal 但尚未建立成功時，B 是否應看到任何草稿痕跡？

回答：完全不共享。

補充整理：只有 A 成功建立後，board 才即時出現新卡片。

### 23. Lock / Presence 是否寫進 Activity Timeline

問題：lock、presence、heartbeat 等協作狀態，是否寫進 Activity Timeline？

回答：不寫進 Activity Timeline。

### 24. Lock 綁定單位

問題：lock 應該綁在 user 還是 tab / session？

回答：綁 tab / session。

### 25. 同一使用者跨不同操作端的衝突規則

問題：同一個使用者跨不同 tab / session 操作同一筆 task，是否也算衝突？

回答：視為正常衝突。

### 26. Lock 釋放後的恢復方式

問題：當 lock 被釋放後，其他人應如何恢復可操作狀態？

回答：立即自動恢復。

補充整理：

- task 上的反灰、外框、`username` 標記要立刻消失。
- 原本被禁止的操作也要立刻恢復可用。

### 27. 斷線與寬限期

問題：短暫斷線對 lock 的影響為何？

回答：保留短暫寬限期，統一採 30 秒；斷線也視為 inactivity。

### 28. 通知型區塊的更新時效

問題：pending invitations、invitation inbox、project list badge 等非即時推送區塊，最晚多久要看到更新？

回答：10 秒。

### 29. Project Members Panel 的同步分類

問題：Project Members Panel 應視為 board 即時面，還是通知面？

回答：視為通知面。

補充整理：同步時效先以 10 秒內自動更新為目標。

### 30. Board 對內容更新的表現

問題：如果 A 在 Task Detail Drawer 儲存了 task 內容變更，而 B 只停留在 Project Board，B 的卡片應如何更新？

回答：只更新 board 上本來就看得到的資訊。

### 31. Lock 衝突時的提示方式

問題：當使用者嘗試對已被鎖住的 task 做衝突操作時，RonFlow 應如何提示？

回答：只顯示 disabled 狀態。

補充整理：搭配 task 上的外框、顏色與 `username` 標記，不另外跳 toast、dialog 或阻擋訊息。

### 32. 是否顯示 project-level presence

問題：除了 task 上的 lock 標記外，是否顯示目前有哪些成員正在這個 Project 裡？

回答：顯示簡單在線名單。

### 33. 在線名單的計算範圍

問題：哪些人算是在這個 Project 內在線？

回答：只有目前真的在這個 Project 內的人才算在線。

### 34. 在線名單的更新時效

問題：當有人進入或離開這個 Project，在線名單應如何更新？

回答：10 秒內自動更新。

### 35. Project presence 的 scope

問題：哪些畫面中的使用者，應被算進這個 Project 的在線名單？

回答：算整個 project scope。

補充整理：包含 `Project Board`、`Task Detail Drawer`、`Archived Tasks View`、`Trash View`、`Project Members Panel`。

### 36. 主動離開 project scope 的效果

問題：如果使用者主動離開這個 Project，例如回到 Project List、切到別的 Project、或進入 Invitation Inbox，presence 與 lock 應如何處理？

回答：立即釋放。

補充整理：一旦主動離開 project scope，該 Project 的 presence 與所有 lock 都要立即釋放。

### 37. Task 被鎖住時是否仍可開 drawer

問題：如果某筆 task 已被別人持有 content edit lock 或 lifecycle lock，其他人還能不能點開 Task Detail Drawer 進入 view mode？

回答：可以。

### 38. 儲存成功後 edit mode 與 lock 如何收尾

問題：當使用者在 edit mode 中儲存成功後，RonFlow 應如何處理 edit mode 與 content edit lock？

回答：儲存成功後，drawer 保持開啟，但退出 edit mode、回到 view mode，同時立即釋放 `content edit lock`。

### 39. 同一使用者多開 tab 時的在線名單顯示

問題：如果同一個使用者用兩個不同 tab / session 同時進入同一個 Project，在在線名單裡應怎麼顯示？

回答：去重顯示使用者。

### 40. Single active session 政策

問題：如果該使用者在另一個 session / 裝置登入，舊 session 應如何處理？

回答：前一個 session 應整個被 lock 住，不讓該 session 使用任何功能。

補充整理：這已經是系統層級的 `single active session policy`，不只是 task-level collaboration lock。

### 41. 舊 session 失效時的 UX

問題：當舊 session 因為新登入而失效時，畫面應該怎麼表現？

回答：先顯示失效訊息，再自動導回登入頁。

補充整理：在這段過渡期間，失效 session 不得再操作任何 RonFlow 功能。

## 未釐清問題

### A. 同一個瀏覽器、同一份登入狀態下開新 tab，算不算新的 session？

這題尚未回答，但它會直接影響：

- `single active session policy` 的真實邊界。
- 前面 `lock 綁 tab / session` 的實作模型。
- 同一使用者多 tab 是否還能共存。

目前有待釐清的方向是：

- 同一瀏覽器沿用同一份登入狀態開新 tab，是否仍算同一個 session。
- 只有重新登入產生新 session 時，才踢掉舊 session；或是只要新畫面出現就視為新 session。

### B. Project List 的近即時同步細節

雖然前面已經釐清 invitation inbox、badge、notification surface 的更新時效，但 `Project List` 本身的規則還沒有最後拍板。

目前仍待釐清的重點包括：

- 當使用者停留在 `Project List` 時，接受邀請後，專案應在多久內自動出現。
- `Project List` 是否要在 10 秒內同步更新 role，例如 `專案成員`。
- `Project List` 是否要同步反映與 invitation badge 相關的周邊狀態。

### C. 舊 session 因新登入而失效時，是否應立即釋放該 session 的 project presence 與所有 task lock？

從目前規則推論，答案應傾向是「要立即釋放」，但這點沒有被明確問答確認。

由於這會影響：

- project 在線名單是否立即移除該使用者。
- task 上的外框、反灰與 `username` lock 標記是否立即消失。
- 其他使用者是否能立刻取得該 task 的操作權。

因此建議後續在正式 spec 前，再把這一條顯式確認。
