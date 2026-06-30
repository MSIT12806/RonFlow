# 抽象任務分解與任務成形管理

## 訪談一：問題與使用情境收斂
目的：確認這是哪些使用者、哪些工作場景會遇到的問題。
內容：目前使用 RonFlow 時，哪些「太大、太模糊」的工作會卡住；使用者現在怎麼拆；什麼情況下會認為 RonFlow 幫上忙。
產出：需求類型判斷、核心痛點、目標使用者、成功判準、MVP 初步範圍、非目標清單。

## 訪談二：任務成形模型
目的：定義 RonFlow 要怎麼描述「任務還不夠清楚」。
內容：成熟度狀態、工作性質分類、未知/依賴/決策的資料模型、任務夠小的判斷規則。
產出：Domain glossary、狀態轉換表、工作項目類型表、Ready checklist、不可執行原因清單。

## 訪談三：使用者流程與畫面
目的：決定使用者實際如何把模糊工作拆成可執行 Task。
內容：建立抽象工作、拆解子項目、標記調查/決策/設計/實作、判斷 Ready、把 Ready 項目送進現有 Todo/Active/Review/Done。
產出：User Journey、主要畫面草稿、Task Detail Drawer 變更點、Board 或 Shaping view 的互動規則。

## 訪談四：驗收、MVP 切片與技術落地
目的：把需求切成可以開發的最小版本。
內容：驗收案例、邊界條件、AI summary 是否要承接、報表是否要納入 maturity、資料遷移、相容性。
產出：更新後 spec、Gherkin acceptance criteria、API contract 草案、前後端實作 backlog。