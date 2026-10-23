# 在地有效警報整合

## 定位

在地有效警報與外交部 BOCA 國外旅遊警示是兩種不同資料：

- 在地有效警報：目前城市正在生效或即將生效的天候、天然災害、交通、民生設施、健康環境及公共安全事件。
- BOCA 旅遊警示：台灣外交部針對海外目的地發布的第 1～4 級旅遊風險。

前端會分區顯示，不會把 BOCA 等級套用到颱風、豪雨或交通停駛。

## 共用 API

```http
GET /api/v1/alerts/local?cityId={cityId}
```

回應使用跨國共用的 `LocalAlertResponse`，包含：

- 警報類型與官方類別
- 正規化嚴重程度；來源未提供時明確標示為 `unknown`
- 標題、摘要與影響地區
- 更新、生效及到期時間
- 發布機關與官方內容網址

Provider 透過 `ILocalAlertsProvider` 依國家接入。未整合的國家會回傳 `unavailable` 與明確訊息，不產生假警報。

## 台灣 NCDR

目前台北使用國家災害防救科技中心的「生效中的示警（適合 web 引用）」Atom feed：

```text
https://alerts.ncdr.nat.gov.tw/webapi/RssAtomFeed.ashx?County=臺北市
```

此 feed 會依官方平台現有資料回傳所有類別，不限定颱風。系統會：

1. 每五分鐘最多重新取得一次並共用快取。
2. 暫時無法更新時，最多保留六小時的合法舊快取。
3. 排除 `Test`、`Cancel` 與已過期事件。
4. 保留官方發布機關、摘要、生效／到期時間及 CAP 原文連結。
5. 不從自由文字推測嚴重程度；Atom feed 未提供 CAP severity 時顯示「官方生效中」。

## 日本 JMA

目前東京使用日本氣象廳「氣象廳防災資訊 XML」官方 PULL feed：

```text
https://www.data.jma.go.jp/developer/xml/feed/extra_l.xml
https://www.data.jma.go.jp/developer/xml/feed/eqvol_l.xml
```

系統先讀取 Atom feed，再只下載需要的官方 XML 電文，並將不同格式正規化成共用警報 Contract：

1. 氣象：只取 Atom 摘要屬於東京都的最新警報／注意報電文，解除或沒有有效種類時不顯示。
2. 地震：讀取近 24 小時的「震源・震度」電文，只顯示東京都實際觀測達震度 3 以上的事件；卡片於發布 24 小時後到期。
3. 海嘯：依東京灣內灣、伊豆諸島及小笠原諸島等東京相關預報區，保留最新警報／注意報，取消或解除後移除。
4. 火山：依東京都所屬伊豆／小笠原火山與島嶼篩選噴火警報、噴火速報及活動解說；一般活動解說於 24 小時後到期，未解除的警報則保留官方狀態。
5. 日文自由文字保留為官方摘要；類別、嚴重程度與常見警報名稱轉成繁體中文顯示，避免改寫官方細節。
6. 每十分鐘最多重新整理一次，暫時中斷時可使用六小時內的合法舊快取；回應前仍會排除已到期卡片。

JMA PULL feed 不需要 API 金鑰。程式限制每次讀取的近期電文數量，驗證連結必須是 JMA 官方 HTTPS 網域，並以最多八個並行下載避免對官方來源產生不必要負載。

## 香港 HKO

目前香港使用香港天文台官方開放資料 API 的繁體中文資料：

```text
https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=warnsum&lang=tc
https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=warningInfo&lang=tc
https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=swt&lang=tc
https://data.weather.gov.hk/weatherAPI/opendata/earthquake.php?dataType=feltearthquake&lang=tc
```

系統會將所有現正生效的官方警告正規化，而不是只篩選颱風：

1. 警告總覽與詳細內容配對，涵蓋熱帶氣旋、暴雨、雷暴、水浸、山泥傾瀉、海嘯、火災危險、酷熱、寒冷、霜凍及強烈季候風等 HKO 現行類別。
2. 「特別天氣提示」以獨立卡片顯示；例如預告八號風球、局部大雨或政府安全提醒。
3. 本地有感地震只保留發生後 24 小時內的報告，避免長期把歷史事件當成目前警報。
4. HKO 官方顏色與風球級別會正規化成跨國嚴重程度，但官方繁體中文標題與摘要仍保留。
5. 每五分鐘最多重新整理一次，來源暫時中斷時可使用六小時內的合法舊快取；回應前仍排除已到期項目。

HKO 開放資料 API 不需要 API 金鑰。

## 新加坡 MSS（部分涵蓋）

已接入 NEA 官方 RSS 清單列出的 MSS 豪雨警告 Atom feed：
`https://www.weather.gov.sg/files/rss/rssHeavyRain_new.xml`。

- 最新電文為 `NIL`、明確取消或無豪雨警告時，回傳空清單。
- 保留官方英文摘要，依其中明確的有效終止時間（新加坡 UTC+8）排除過期警告；無法解析有效時間時回報來源無法更新，不宣稱目前安全。
- 每五分鐘共用快取；一小時內舊快取僅保留仍未到期卡片。
- 畫面明確標示僅涵蓋豪雨。閃電、水浸、煙霾、地震與交通異常尚未整合，不能將空清單解讀為所有風險均已解除。
- 已取得的真實來源樣本為 Atom `summary=NIL`；有效警告時間格式使用模擬案例測試，仍需真實生效電文驗證。

## 後續 Provider

後續切片會沿用相同 Contract 與 UI，依序接入：

- 日本交通：ODPT 鐵路營運異常（另立切片，不與天然災害警報混在本次整合）。
- 香港交通：現有交通 Provider 的營運異常。
- 新加坡：其餘 NEA／MSS、PUB 警示及 LTA Service Alerts。
