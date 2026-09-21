# Travel Info Assistant

以台灣旅客為主要使用者、可從 LINE 快速開啟的旅遊資訊助手。系統將整合台灣與海外大眾運輸、全球直飛航班、天氣、旅遊警示及當地應急資訊。

> 目前進度：Phase 1、2 已完成；Phase 3 已完成台北公車與台北捷運的 TDX 第一版。航班與海外交通仍維持明確的未整合狀態，不使用假資料冒充即時資訊。

## MVP 邊界

- 提供班次、到站預估與航班狀態查詢。
- 不提供訂票、付款、票價、導航或即時位置地圖。
- 大眾運輸依城市提供；航班支援全球任意機場間的直飛查詢。
- 第三方資料一律由後端取得並共用快取，前端不持有 API 金鑰。
- 免費額度耗盡時保留入口並安全降級，不自動升級付費方案。

完整需求請參閱 [MVP 規格](docs/MVP_SPEC.md)。

## 技術棧

| Layer | Technology |
|---|---|
| LINE entry | LIFF SDK（未設定 LIFF ID 時可用一般瀏覽器開發） |
| Frontend | Vue 3、TypeScript、Vite、Vue Router、Pinia |
| Backend | ASP.NET Core Web API、EF Core |
| Data | PostgreSQL、Redis |
| Runtime | Docker Compose、Nginx |
| CI | GitHub Actions |

## 已完成的交通功能

- 台北公車：路線搜尋、方向、站牌順序與即時到站預估。
- 台北捷運：車站搜尋、即時列車與表定時刻降級。
- 到站顯示遵守「60 分鐘以上表定、60 分鐘內即時、少於 1 分鐘即將進站」。
- TDX OAuth token 共用、Redis／記憶體雙層快取、同鍵 single-flight 防止快取擊穿。
- 每分鐘 4 次內部限流、2.7 點軟停止線、實際 requests 與 response bytes 用量估算。
- Provider 中斷時保留功能入口，合法舊快取仍可顯示並標記為備援資料。

實作與額度細節請參閱 [TDX 整合說明](docs/TDX_INTEGRATION.md)。

## 專案結構

```text
travel-info-assistant/
├── frontend/                 Vue LIFF 前端
├── backend/
│   ├── src/                  ASP.NET Core Web API
│   └── tests/                後端單元測試
├── docs/                     需求與架構文件
├── .github/workflows/        CI
└── compose.yaml              完整本機環境
```

## 本機啟動

### Docker（建議）

```bash
cp .env.example .env
# 編輯 .env，填入 TDX_CLIENT_ID 與 TDX_CLIENT_SECRET 才會取得真實交通資料
docker compose up --build
```

- Web：<http://localhost:5173>
- API：<http://localhost:8080/api/v1/health>
- Swagger：<http://localhost:8080/swagger>

### 分開開發

需要 Node.js 22+、.NET 10 SDK、PostgreSQL 及 Redis。

```bash
cd frontend
npm install
npm run dev
```

```bash
cd backend
dotnet restore
dotnet run --project src/TravelInfoAssistant.Api
```

Vite 會把 `/api` 代理到 `http://localhost:8080`。

未設定 TDX 金鑰時，系統仍可啟動，公車與捷運入口會保留並顯示「暫時無法更新」，不會產生假資料。

## 已實作 API

| Method | Endpoint | 用途 |
|---|---|---|
| GET | `/api/v1/transit/modes?cityId=` | 城市已整合交通模式 |
| GET | `/api/v1/transit/bus/routes?cityId=&q=` | 台北公車路線搜尋 |
| GET | `/api/v1/transit/bus/stops?cityId=&routeName=&direction=` | 路線方向與站牌 |
| GET | `/api/v1/transit/bus/arrivals?cityId=&routeName=&direction=&stopId=` | 公車到站資訊 |
| GET | `/api/v1/transit/metro/stations?cityId=&q=` | 台北捷運車站搜尋 |
| GET | `/api/v1/transit/metro/arrivals?cityId=&stationId=` | 捷運即時／表定資訊 |
| GET | `/api/v1/transit/tdx/status` | TDX 設定與本月估算用量 |

## 驗證

```bash
cd frontend
npm run type-check
npm run test
npm run build
```

```bash
cd backend
dotnet test
```

## 環境變數與安全

1. 從 `.env.example` 複製 `.env`。
2. 真實 API 金鑰只能存放於本機環境變數或部署平台的 Secret。
3. `.env` 已被 Git 忽略；請勿把任何 Provider 金鑰提交至 Repository。

## Roadmap

1. ✅ Phase 1：可執行的前後端、PostgreSQL、Redis、城市 API 與基礎介面。
2. ✅ Phase 2：城市能力矩陣、首頁與定位切換。
3. 🚧 Phase 3：台北 TDX 公車與捷運已完成；台鐵待實作。
4. Phase 4：全球直飛航班與 AeroDataBox 額度防護。
5. Phase 5：天氣、旅遊警示及應急資訊。
6. Phase 6：東京 ODPT。
7. Phase 7：測試、部署與履歷展示。
