# Travel Info Assistant

以台灣旅客為主要使用者、可從 LINE 快速開啟的旅遊資訊助手。系統將整合台灣與海外大眾運輸、全球直飛航班、天氣、旅遊警示及當地應急資訊。

> 目前進度：Phase 1～5 已完成，Phase 6 已完成東京地下鐵、香港港鐵、香港巴士、香港電車與香港渡輪，以及新加坡 MRT／巴士的城市交通 MVP；另包含台北公車、台北捷運、台鐵、高鐵、全球直飛航班、單一航班編號、全球天氣、BOCA 旅遊警示與台北／東京應急資訊。尚未整合的服務仍會明確標示，不使用假資料冒充即時資訊。

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
- 台北捷運：先選路線與方向，再選車站；提供即時列車、表定時刻降級與路線官方營運狀態。
- 台鐵：全台車站搜尋與方向篩選預設只讀取官方當日班表；使用者按下「更新即時資訊」後才查詢一次誤點與月台，查詢失敗或額度不足仍保留班表。
- 高鐵：從台灣區域與縣市按鈕選擇車站，依官方真實站序顯示往北／往南各最多十班當日表定班次；不呼叫即時端點，當日班次結束後以每日班表顯示末班駛離時間。
- 東京地下鐵：以 ODPT 官方資料搜尋 Tokyo Metro 與都營地下鐵四線的路線、日／英文車站名稱、車站代碼、方向及當日表定班次；都營地下鐵在有效的 `odpt:Train` 資料可配對時，以表定時間加上 `odpt:delay` 秒數提供即時預估，資料缺失或過期即回退表定時間。Tokyo Metro 維持表定班次加官方營運狀態，不以本機倒數冒充即時資料；列車位置不納入核心卡片體驗。
- 都營地下鐵資料依 CC BY 4.0 標示來源：東京都交通局・公共交通オープンデータ協議会。
- 香港港鐵：以香港鐵路有限公司及 DATA.GOV.HK 官方開放資料提供 10 條主要路線、繁體中文／英文車站、雙向最多四班即時到站、目的地、月台、延誤與服務警示；即時卡片每 15 秒更新，無需 API 金鑰。
- 香港巴士：整合九巴／龍運一般 `service_type=1` 路線、城巴及新大嶼山巴士，提供營運業者、官方行車方向與真實站序，以及每方向最多三班 ETA；同號路線以業者識別碼分流，新大嶼山巴士另保留各官方方向／變體。當 ETA 已無班次且香港運輸署 GTFS 顯示該方向末班已過，會標示官方班表的起點末班時間；單一業者暫時中斷時仍保留其他業者資料。小巴暫不納入。
- 香港電車：以香港運輸署 GTFS 提供六條主要路線、兩個行車方向及完整沿途站序；班次區塊只顯示最多十班起點表定發車並清楚標示非中途站 ETA。當日班次結束後，以每日班表顯示末班車駛離起點站的時間，不參考延誤或推測中途站時間。
- 香港渡輪：以香港運輸署 GTFS 提供渡輪與街渡路線、資料實際存在的行駛方向及沿途碼頭；每方向顯示最多十班表定開航，並在官方資料存在時顯示目的地表定抵達時間。當日船班結束後顯示每日班表的末班開航時間，不冒充即時航行資訊。
- 新加坡 MRT：以 LTA DataMall 的 GTFS Schedule、GTFS-Realtime Trip Updates 與 Service Alerts 提供去重後的公共路線、台灣繁體／英文車站、方向、雙向最多四班到站、目的地、資料源有提供時的月台、延誤／取消／不停靠及路線服務警示；有即時預估時覆蓋表定時間，否則明確回退至官方班表，到站卡片每 15 秒、警示卡片每 30 秒更新。
- 新加坡巴士：以 LTA DataMall 官方路線、站牌與到站資料提供雙向真實站序及最多三班到站資訊，並以每日首末班資料處理末班公告。
- 台灣交通的共同演進原則為「官方班表與共用快取優先、即時資訊由使用者按需更新、額度不足自動退回班表／合法舊快取」；表定倒數不得標示為即時 ETA。既有台北交通會依後續獨立切片逐步套用此原則。
- 交通介面以台灣繁體為主要顯示語言；官方只提供日文或英文自由文字時，先顯示繁體中文狀態摘要，原文收合保留供核對。公車、捷運與台鐵班次均依方向分組，不混合顯示反方向車次；公車與捷運的即時到站卡片目前每 15 秒向自有 API 更新並共用後端快取，台鐵即時資訊則只在使用者按鈕操作時更新一次。
- 到站顯示在 60 分鐘內統一採分鐘倒數、少於 1 分鐘顯示即將進站；官方即時 ETA 與表定倒數會清楚標示，表定鐘點保留在卡片下方。分鐘倒數由前端依已取得的絕對時間自行更新，不會因此持續呼叫外部 Provider。
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
# 編輯 .env；TDX、ODPT、LTA DataMall 與 AeroDataBox 金鑰分別控制交通及全球航班真實資料
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
| GET | `/api/v1/transit/bus/routes?cityId=&q=` | 台北／香港／新加坡公車與巴士路線搜尋 |
| GET | `/api/v1/transit/bus/stops?cityId=&routeName=&direction=` | 路線方向與站牌 |
| GET | `/api/v1/transit/bus/arrivals?cityId=&routeName=&direction=&stopId=` | 公車到站資訊 |
| GET | `/api/v1/transit/tram/routes?cityId=` | 香港電車六條主要路線與雙向端點 |
| GET | `/api/v1/transit/tram/stops?cityId=&routeId=&direction=` | 香港電車指定方向的官方沿途站序 |
| GET | `/api/v1/transit/tram/departures?cityId=&routeId=&direction=` | 香港電車起點每日班表、最多十班表定發車與末班公告 |
| GET | `/api/v1/transit/ferry/routes?cityId=` | 香港渡輪及街渡路線與官方行駛方向 |
| GET | `/api/v1/transit/ferry/stops?cityId=&routeId=&direction=` | 香港渡輪指定方向的沿途碼頭順序 |
| GET | `/api/v1/transit/ferry/journeys?cityId=&routeId=&direction=` | 香港渡輪表定開航、抵達時間與末班公告 |
| GET | `/api/v1/transit/metro/routes?cityId=&q=` | 台北／東京／香港／新加坡地鐵路線搜尋 |
| GET | `/api/v1/transit/metro/stations?cityId=&q=` | 台北／東京／香港／新加坡捷運車站搜尋 |
| GET | `/api/v1/transit/metro/arrivals?cityId=&stationId=` | 捷運即時／表定資訊 |
| GET | `/api/v1/transit/metro/status?cityId=&routeId=` | 台北／東京／香港／新加坡地鐵官方運行狀態與有效期限 |
| GET | `/api/v1/transit/rail/stations?cityId=&q=` | 台鐵車站搜尋 |
| GET | `/api/v1/transit/rail/arrivals?cityId=&stationId=&includeRealtime=` | 台鐵當日班表；`includeRealtime=true` 時按需查詢一次誤點與月台 |
| GET | `/api/v1/transit/rail/high-speed/stations?cityId=&q=` | 高鐵車站搜尋 |
| GET | `/api/v1/transit/rail/high-speed/arrivals?cityId=&stationId=` | 高鐵當日表定班次與末班公告 |
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
4. 新加坡 MRT 只使用 `LTA_DATAMALL_ACCOUNT_KEY`（API Account Key）；SDK Account Key 不需要。可由 [LTA DataMall 官方申請頁](https://datamall.lta.gov.sg/content/datamall/en/request-for-api.html) 申請或輪替。

## Roadmap

1. ✅ Phase 1：可執行的前後端、PostgreSQL、Redis、城市 API 與基礎介面。
2. ✅ Phase 2：城市能力矩陣、首頁與定位切換。
3. ✅ Phase 3：台北 TDX 公車、捷運與台鐵查詢。
4. ✅ Phase 4：全球直飛航班與 AeroDataBox 額度防護。
5. ✅ Phase 5：全球天氣、BOCA 旅遊警示與台北／東京應急資訊。
6. 🚧 Phase 6：城市交通擴充（已完成東京 ODPT、香港港鐵、香港巴士、香港電車、香港渡輪、新加坡 MRT／巴士與台灣高鐵；維持卡片體驗且不加入車輛位置地圖，後續依旅遊需求與官方資料品質評估其他業者與城市）。
7. Phase 7：測試、部署與履歷展示。
