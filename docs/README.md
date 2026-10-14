# Travel Info Assistant — Documentation Map

這個專案文件同時包含目前架構、各 Provider 整合細節與早期產品規格。第一次閱讀建議依下列順序：

## 建議閱讀順序

1. [Repository README](../README.md) — 專案定位、目前整合範圍與架構摘要
2. [MVP Spec](./MVP_SPEC.md) — 系統邊界與資料流；文件頂端已標示目前 Web App 方向
3. [TDX Integration](./TDX_INTEGRATION.md) — 台灣交通 Provider、快取與額度策略
4. [Flights Integration](./FLIGHTS_INTEGRATION.md) — 全球航班 Provider、快取與 quota handling
5. [Security Policy](../SECURITY.md) — API key 與 credential 管理原則

## 閱讀原則

- README 描述目前作品定位；
- `MVP_SPEC.md` 保留部分歷史設計，因此 LINE / LIFF 相關段落應以文件頂端的 CURRENT PRODUCT DIRECTION 為準；
- Provider 額度、方案與外部 API 條件可能改變，文件中的數字都有查核日期，不應視為永久規則；
- 實際可執行狀態以 `main` branch source code、tests 與 CI 為準。
