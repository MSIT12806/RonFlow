# Layered Architecture

Layered Architecture 是分層架構，指的是把系統依責任切分成幾個不同層次，常見像是介面層、[Application Layer](./application-layer.md)、[Domain Model](./domain-model.md) 與 [Infrastructure](./infrastructure.md)。每一層處理的問題不同，目的是讓程式碼更容易理解、修改與替換。

分層的核心不只是把資料夾分開，而是讓責任邊界更清楚。當畫面流程、商業規則與技術細節混在一起時，系統很快就會難以維護；而適當分層能降低牽一髮動全身的機率。

不過，Layered Architecture 也不是只要分層就一定比較好。如果每一層只是機械式轉手資料，卻沒有清楚責任，那麼分層反而只會增加複雜度。因此，真正重要的是邊界是否合理，而不只是圖畫得好不好看。
