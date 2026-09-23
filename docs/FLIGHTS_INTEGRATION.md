# AeroDataBox 全球航班整合

- 實作版本：Phase 4
- 文件更新：2026-09-23
- 資料來源：AeroDataBox 航班 API、OurAirports 機場資料
- 範圍：全球直飛航班與單一航班編號查詢；不含票價、訂位與轉機組合

## 啟用方式

預設使用 AeroDataBox 的 RapidAPI gateway。先在對應 Marketplace 訂閱可用方案，再將金鑰放入 `.env`：

```dotenv
AERODATABOX_GATEWAY=RapidApi
AERODATABOX_BASE_URL=https://aerodatabox.p.rapidapi.com/
AERODATABOX_API_KEY=your-key
AERODATABOX_BILLING_CYCLE_DAY=1
```

若使用 AeroDataBox Direct API，將 gateway 改為 `Direct`，並把 base URL 與金鑰換成 Direct API 帳號提供的值。金鑰只由後端讀取，不能寫入前端或提交 Git。

修改設定後重建 API：

```bash
docker compose -f compose.yaml -f compose.codespaces.yaml build api
docker compose -f compose.yaml -f compose.codespaces.yaml up -d --no-deps --force-recreate api
```

## 內部 API

| Method | Endpoint | 外部成本 |
|---|---|---:|
| GET | `/api/v1/airports?q=taipei&limit=10` | 0；本地字典 |
| GET | `/api/v1/flights/search?origin=TPE&destination=NRT&date=2026-09-23` | 快取未命中時 2 次 Tier 2 |
| GET | `/api/v1/flights/BR198?date=2026-09-23` | 快取未命中時 1 次 Tier 2 |
| GET | `/api/v1/flights/provider/status` | 0；內部用量狀態 |

所有回應沿用統一格式：

```json
{
  "data": [],
  "meta": {
    "dataStatus": "scheduled",
    "source": "AeroDataBox",
    "sourceUpdatedAt": null,
    "fetchedAt": "2026-09-23T00:00:00Z",
    "stale": false,
    "message": null
  }
}
```

`dataStatus` 可能為 `realtime`、`scheduled`、`cached` 或 `unavailable`。Provider 中斷時，有效保留期內的舊快取會以 `cached` 加上 `stale: true` 回傳；沒有真實快取時回傳空陣列，不補假資料。

## 查詢與快取策略

### 機場搜尋

`Data/airports.min.json` 由 `tools/build-airport-catalog.py` 從 OurAirports 公開資料產生，只收錄具 IATA 代碼且有定期服務的機場。搜尋支援 IATA、ICAO、機場名、城市、國家碼與關鍵字，並優先排列精確代碼。

更新機場字典：

```bash
python3 tools/build-airport-catalog.py /path/to/airports.csv \
  backend/src/TravelInfoAssistant.Api/Data/airports.min.json \
  --source-commit OURAIRPORTS_COMMIT
```

### 航線搜尋

AeroDataBox FIDS 一次最多查詢 12 小時，因此一個當地日期拆成 `00:00–11:59` 與 `12:00–23:59`。快取鍵不含目的地：

```text
flight:aerodatabox:fids:{origin}:{date}:{block}:v1
```

後端取得出發機場班表後才依目的地篩選。相同出發機場與日期的不同目的地查詢可共用快取，避免重複消耗 units。

### 航班編號

航班編號會移除空白與符號、轉為大寫，再以航班編號與當地日期查詢。快取鍵：

```text
flight:aerodatabox:status:{flightNumber}:{date}:v1
```

### 新鮮度與保留期

| 資料 | 新鮮時間 | 最長保留 |
|---|---:|---:|
| 今日／前後一日 FIDS | 10 分鐘 | 7 天 |
| 未來 FIDS | 6 小時 | 7 天 |
| 歷史 FIDS | 1 天 | 7 天 |
| 近期航班編號 | 5 分鐘 | 1 天 |
| 其他航班編號 | 6 小時 | 1 天 |

Redis 是主要共用快取；Redis 中斷時改用程序內記憶體。相同快取鍵由 single-flight 鎖合併並行請求。

## 額度保護

Endpoint tier、方案 units、速率及流量條件可能改變。下列數值是專案預設保護值，不代表 Marketplace 永久承諾：

| 環境變數 | 預設 | 行為 |
|---|---:|---|
| `AERODATABOX_MONTHLY_SOFT_LIMIT_UNITS` | 360 | 停止新的航線 FIDS 查詢 |
| `AERODATABOX_MONTHLY_HARD_LIMIT_UNITS` | 400 | 停止所有外部航班查詢 |
| `AERODATABOX_MONTHLY_TRAFFIC_SOFT_LIMIT_BYTES` | 9000000000 | 達線後停止外部查詢 |
| `AERODATABOX_REQUESTS_PER_SECOND` | 1 | 程序內速率閘門 |
| `AERODATABOX_BILLING_CYCLE_DAY` | 1 | UTC 計費週期起始日（1～28） |

每次外部請求前依 endpoint tier 預扣 units；航線搜尋只能使用軟停止線內額度，航班編號查詢可使用保留到硬停止線的額度。request count、units 與回應 bytes 會保存在 Redis，狀態可由 provider status endpoint 查看。

部署前應在訂閱後台確認方案、計費週期、endpoint tier、流量與 overage，並把內部上限設得不高於實際方案。`AllowPaidOverage` 預設為 `false`；本專案不會自動購買額度或升級方案。

## 驗證

未設定金鑰也可驗證機場字典與安全降級：

```bash
curl -sS "http://localhost:8080/api/v1/airports?q=TPE"
curl -sS "http://localhost:8080/api/v1/flights/provider/status"
curl -sS "http://localhost:8080/api/v1/flights/search?origin=TPE&destination=NRT&date=2026-09-23"
```

設定金鑰後，最後一個請求應回傳真實資料或空結果，`meta.source` 為 `AeroDataBox`，且 status endpoint 的 `requestCount`／`usedUnits` 會增加。若 Provider 無法連線或額度已達停止線，`meta.dataStatus` 會是 `unavailable`，而不是產生範例航班。
