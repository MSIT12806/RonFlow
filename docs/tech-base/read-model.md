# Read Model

Read Model 是為了查詢與展示而整理出的資料視圖。它的重點是讓畫面、報表或 API 可以方便讀取需要的資訊，而不是承擔完整的業務規則。

因此，Read Model 不一定要和 [Domain Model](./domain-model.md) 長得一樣。它可以根據查詢用途，把多個來源的資料組合成看板、列表、明細頁或活動時間線等更容易閱讀的形狀。

在使用 [Domain Event](./domain-event.md) 的系統裡，Read Model 可能直接從資料表查詢，也可能透過事件更新。重點不在技術形式，而在它服務的是「讀取需求」，不是寫入決策。