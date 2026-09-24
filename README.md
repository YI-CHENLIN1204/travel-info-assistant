# Travel Info Assistant

以台灣旅客為主要使用者、可從 LINE 快速開啟的旅遊資訊助手。系統將整合台灣與海外大眾運輸、全球直飛航班、天氣、旅遊警示及當地應急資訊。

> 目前進度：Phase 1～5 已完成；包含台北公車、台北捷運、台鐵、全球直飛航班、單一航班編號、全球天氣、BOCA 旅遊警示與台北／東京應急資訊。尚未整合的服務仍會明確標示，不使用假資料冒充即時資訊。

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
- 台鐵：全台車站搜尋、即時列車、誤點與月台資訊，以及表定時刻降級。
- 到站顯示遵守「60 分鐘以上表定、60 分鐘內即時、少於 1 分鐘即將進站」。
- TDX OAuth token 共用、Redis／記憶體雙層快取、同鍵 single-flight 防止快取擊穿。
- 每分鐘 4 次內部限流、2.7 點軟停止線、實際 requests 與 response bytes 用量估算。
- Provider 中斷時保留功能入口，合法舊快取仍可顯示並標記為備援資料。

實作與額度細節請參閱 [TDX 整合說明](docs/TDX_INTEGRATION.md)。

## 已完成的全球航班功能

- 以本地 OurAirports 字典搜尋全球 4,000 多座有定期服務且具 IATA 代碼的機場，不消耗航班 API 額度。
- 依出發機場、抵達機場與日期查詢直飛航班，或依航班編號與日期查詢。
- 顯示表定／預估／實際當地時間、跨日、狀態、航廈、登機門、航空公司與機型。
- AeroDataBox FIDS 全日查詢拆成兩個 12 小時區段，快取鍵以「出發機場＋日期＋區段」共用。
- Redis／記憶體雙層快取、同鍵 single-flight、Tier units 預扣、速率限制、流量與計費週期用量保護。
- Provider 未設定、額度用完或連線中斷時不產生假航班；合法舊快取仍可安全降級顯示。

設定方式、快取與額度策略請參閱 [AeroDataBox 航班整合說明](docs/FLIGHTS_INTEGRATION.md)。

## 已完成的天氣功能

- 以目前城市或瀏覽器定位座標查詢 MET Norway 全球天氣預報。
- 顯示目前體感、濕度、降雨、風向風速，以及未來 12 小時與 6 日預報。
- 依來源的 `Expires` 共用快取，座標統一至四位小數，避免相同地點重複請求。
- Provider 中斷時優先顯示合法舊快取並標示降級狀態，不產生假天氣資料。

## 已完成的旅遊警示與應急資訊

- 從外交部領事事務局 BOCA 官方 RSS 取得旅遊警示，依目前城市及熱門目的地呈現最高警示級別。
- 旅遊警示以一小時共用快取保護官方來源；暫時中斷時可顯示三日內的合法舊快取並明確標示。
- 台北及東京的警察、消防／救護、旅客服務與外交部緊急電話均以純文字顯示，不會自動撥號。
- 東京提供駐日本代表處館址、總機及急難救助聯絡方式。
- 護照遺失、財物遭竊及重大事故處理步驟存於 PostgreSQL，每筆資料均附官方來源與人工確認日期。

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
# 編輯 .env；TDX 與 AeroDataBox 金鑰分別控制大眾運輸及全球航班真實資料
docker compose up --build
```

- Web：<http://localhost:5173>
- API：<http://localhost:8080/api/v1/health>
- Swagger：<http://localhost:8080/swagger>

### GitHub Codespaces

Codespaces 使用專用 Compose 覆寫檔：

```bash
docker compose -f compose.yaml -f compose.codespaces.yaml up -d --build
```

- `.devcontainer/devcontainer.json` 會自動將 Web 的 `5173` 轉送為 HTTP，並只開啟一次瀏覽器。
- Codespaces 覆寫檔讓 Web 的 Nginx 直接監聽主機 `5173`，不再經過 Docker port publishing／`docker-proxy`。
- 瀏覽器中的 Codespaces 網址仍會顯示 `https://...app.github.dev`；這是 GitHub 對外提供的 HTTPS，容器內的 Nginx 仍使用 HTTP，請勿將 5173 的 Port Protocol 改成 HTTPS。
- PostgreSQL `5432`、Redis `6379` 與 API `8080` 只供 Codespace 內部服務使用，不會自動對外轉送。
- 既有 Codespace 第一次取得這項設定時，請執行一次 **Codespaces: Rebuild Container**；重建後 5173 會由 `forwardPorts` 管理，不再恢復舊的「使用者轉送」。

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

未設定第三方金鑰時系統仍可啟動；相關入口會保留並明確顯示無法更新，不會產生假資料。機場自動完成不需要 AeroDataBox 金鑰。

## 已實作 API

| Method | Endpoint | 用途 |
|---|---|---|
| GET | `/api/v1/transit/modes?cityId=` | 城市已整合交通模式 |
| GET | `/api/v1/transit/bus/routes?cityId=&q=` | 台北公車路線搜尋 |
| GET | `/api/v1/transit/bus/stops?cityId=&routeName=&direction=` | 路線方向與站牌 |
| GET | `/api/v1/transit/bus/arrivals?cityId=&routeName=&direction=&stopId=` | 公車到站資訊 |
| GET | `/api/v1/transit/metro/stations?cityId=&q=` | 台北捷運車站搜尋 |
| GET | `/api/v1/transit/metro/arrivals?cityId=&stationId=` | 捷運即時／表定資訊 |
| GET | `/api/v1/transit/rail/stations?cityId=&q=` | 台鐵車站搜尋 |
| GET | `/api/v1/transit/rail/arrivals?cityId=&stationId=` | 台鐵即時／表定資訊 |
| GET | `/api/v1/transit/tdx/status` | TDX 設定與本月估算用量 |
| GET | `/api/v1/airports?q=&limit=` | 全球機場自動完成 |
| GET | `/api/v1/flights/search?origin=&destination=&date=` | 全球直飛航班查詢 |
| GET | `/api/v1/flights/{flightNumber}?date=` | 航班編號查詢 |
| GET | `/api/v1/flights/provider/status` | AeroDataBox 設定與本期用量 |
| GET | `/api/v1/weather?cityId=` | 城市中心天氣預報 |
| GET | `/api/v1/weather/location?lat=&lon=` | 指定座標天氣預報 |
| GET | `/api/v1/alerts?cityId=` | 目前城市所屬地區旅遊警示 |
| GET | `/api/v1/alerts/popular` | 熱門目的地最高旅遊警示 |
| GET | `/api/v1/emergency?cityId=` | 當地緊急電話、駐外館處與處理指引 |

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
3. ✅ Phase 3：台北 TDX 公車、捷運與台鐵查詢。
4. ✅ Phase 4：全球直飛航班與 AeroDataBox 額度防護。
5. ✅ Phase 5：全球天氣、BOCA 旅遊警示與台北／東京應急資訊。
6. Phase 6：東京 ODPT。
7. Phase 7：測試、部署與履歷展示。
