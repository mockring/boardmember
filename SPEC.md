# 菠德桌上遊戲空間 會員系統 SPEC.md

— **文件版本：v1.0**（2026-04-15）

---

## 1. 網站概述

### 1.1 網站名稱
「菠德桌上遊戲空間」會員系統

### 1.2 網站類型
會員管理 + 服務預約 + 現場POS複合型網站

### 1.3 目標用戶
- **前台：** 桌遊店會員（消費者）
- **後台：** 桌遊店老闆/員工（管理者）

### 1.4 核心功能摘要
| 角色 | 可用功能 |
|------|----------|
| 會員 | 註冊登入、遊戲租借、空間訂位預約、活動報名、查看優惠券、遊玩時間記錄 |
| 管理者 | 會員管理、等級管理、商品管理、庫存管理、租借歸還處理、遊玩時間記錄、快速消費POS、優惠券設定、報表檢視、活動公告管理 |

---

## 2. 會員系統

### 2.1 會員等級

| 等級名稱 | 升級時數門檻 | 升級金額門檻 | 福利 |
|----------|-------------|-------------|------|
| 非會員 | — | — | 遊戲購買原價（1.0折）、場地費平日60元/hr、假日70元/hr |
| 會員 | 1,000 小時 | 100,000 元 | 遊戲購買9折（0.9折）、場地費平日50元/hr、假日60元/hr |

- 管理者可在後台新增、修改、刪除等級（含名稱、條件、福利）
- 等級採「同時滿足時數與金額門檻後自動升級」原則（會員同時滿足時自動升級；非會員為系統識別用，不自動升級）
- **非會員**為系統識別用，現場顧客不登入系統消費，福利為原價計費，**不可刪除**
- **會員**為可刪除等級，預設為新會員的等級
- **非會員消費同樣會紀錄在系統中**（由管理者幫忙建立訂單），只是不登入系統、無法升級

### 2.2 會員資料欄位
- 姓名（必填）、電話（必填）、Email（必填）、生日（選填）
- 註冊日期（系統自動產生）
- 累積遊玩時數、累積消費金額（系統自動累計）
- 當前等級（系統自動判定）、會員狀態（啟用/停用）

---

## 3. 商品與服務

### 3.1 商品與服務
- 商品類別：桌遊、零食、飲料、服務（視店家需求調整）
- 實體商品：支援圖片（URL）、名稱、說明、售價
- 服務項目（`IsService = true`）：遊玩服務、空間租借等，不控管庫存
- 低庫存警示功能（`LowStockAlert`，低於設定值主動提示）
- 進貨記錄（日期、數量、供應商可備註）

### 3.2 空間訂位預約
- 小時計費，平日/假日不同價
- 會員根據等級有不同單價（見 2.1 會員福利）
- 支援包廂（包廂費另計）
- 管理者於後台建立訂單，選擇時段與使用的會員
- **會員申請流程：** 選擇日期 → 填寫人數 → 選擇空間（訂位/包廂）→ 填寫時段 → 送出申請
- **管理員審核：** 後台審核通過後可建立訂單

### 3.3 遊戲租借
- 會員可至前台提出租借申請，填寫希望取件日期時間
- 管理者後台審核通過 → 確認借出 → 歸還
- **狀態流程：** 審核中 → 已通過 → 已借出 → 已歸還
- **押金：** 會員免押金，非會員 = 遊戲定價
- **租金計算：** ≤1,000元→100元、≤2,000元→200元、≤3,000元→300元、>3,000元→400元
- 押金不計入訂單金額，只算租金

### 3.4 在店遊玩計時
- **小時計費**（依會員等級優惠）：平日60/50元/hr、假日70/60元/hr
- **包日方案**：平日240/180元、假日280/220元（4小時以上適用）
- 遊玩起訖時間由管理者後台紀錄
- 系統自動累計會員「累積遊玩時數」

### 3.5 優惠券
- 管理者可新增優惠券（名稱、折扣金額/比例、適用範圍、發行數量、有效期限）
- 優惠券類型：滿額折扣、單品折扣、場地費折抵等
- 生日禮：當月3人同行，壽星免場地費（需湊滿3人）
- **不限張數優惠券**（`TotalQuantity == null`）：可重複使用，只遞增 `UsedCount`
- 會員於現場消費時可套用優惠券（透過POS系統）

---

## 4. 金流與優惠

### 4.1 交易方式
- 現場收款為主（管理者直接收取現金或轉帳）
- 轉帳對帳：會員轉帳後，管理者於後台確認付款

### 4.2 優惠券功能
- 管理者可新增優惠券（名稱、折扣金額/比例、適用範圍、發行數量、有效期限）
- 會員於現場消費時可套用優惠券（透過POS系統）
- 優惠券類型：滿千折百、單品折扣、場地費折抵等

---

## 5. 後台功能

### 5.1 會員管理
- 檢視會員列表（姓名、等級、累積時數、註冊日期）
- 搜尋、篩選會員
- 編輯會員資料、調整等級（手動升級/降級）
- 停用/啟用會員

### 5.2 等級管理
- 新增/編輯/刪除會員等級
- 設定名稱、升級條件（門檻、福利內容）

### 5.3 商品管理
- 商品上下架、編輯（名稱、說明、價格、圖片、是否為服務）
- 低庫存提醒（低於 `LowStockAlert` 設定值主動提示）
- 進貨記錄

### 5.4 訂單管理
- 檢視所有訂單（商品購買、空間租借、遊戲租借、遊玩紀錄）
- 篩選條件：日期範圍、會員（姓名/電話）、訂單類型、付款狀態
- 訂單詳情（含明細、金額計算）
- 列表底部顯示篩選結果合計（原價/折扣/實收）
- 刪除訂單（僅限未付款訂單）

### 5.5 儀表板（Admin 首頁）
儀表板分為四個資訊區塊，各區塊標題均可點擊連結至對應管理頁面：

| 區塊 | 內容 |
|------|------|
| 🎮 進行中的遊玩 | 遊玩中消費者名單（狀態：遊玩中/待結帳），含「結束遊玩」與「結帳」按鈕 |
| 🎲 遊戲租借 | 審核中/已通過/已借出三種狀態的租借申請，含操作按鈕流程 |
| 📅 訂位預約 | 今日及未來的已預訂/審核中訂位預約，含狀態 badge |
| 📋 報名中的活動 | 報名中的活動列表（Status=RegistrationOpen），連結至活動管理頁 |

### 5.5.1 快速消費（POS）
- **位置：** 儀表板首頁醒目按鈕，一鍵啟動
- **流程：**
  1. 輸入電話號碼，系統快速搜尋會員；若無搜尋結果，則為非會員
  2. 選擇商品或服務項目（商品、空間租借、遊戲租借等）
  3. 選擇適用的折扣（會員等級折扣、優惠券）
  4. 系統顯示應收金額
  5. 確認後寫入訂單，完成交易
- **會員搜尋：** `/Pos/SearchMember` API，回傳會員資料與可用優惠券
- **PlayCheckout 模式：** 選擇「遊玩服務」時，由 `PlayRecords` 带出待結帳項目，可同時新增其他商品

### 5.5.2 現場遊玩紀錄
- **時區：** 所有時間統一使用台灣時區（UTC+8），避免伺服器時區差異
- **計費說明：** 從資料庫動態讀取會員等級費率，非寫死
- **已玩時數顯示：** 動態顯示「X 時 Y 分」格式
- **同一會員限制：** 同一會員同一時間僅能有一筆「遊玩中」紀錄，無法重複開單
- **平日/假日判定：** 以「開始時間」的日期為準；國定假日由管理者設定

**時數計算（無條件進位）：**
```
實際遊玩分鐘數 = (結束時間 - 開始時間).TotalMinutes
計費時數 = ceil(實際遊玩分鐘數 / 60)
```

**計費方式：**
| 方案 | 條件 | 金額 |
|------|------|------|
| 小時計費 | 計費時數 < 4 小時 | 計費時數 × 會員等級時單價 |
| 包日計費 | 計費時數 ≥ 4 小時 | 會員等級時單價 × 4（固定） |

**結帳流程（POS）：**
1. 消費者遊玩結束，管理者輸入結束時間 → 寫入 `PlayRecords`（狀態：已完成待結帳）
2. 管理者進入 POS → 選擇同一會員 → 選擇「遊玩服務」品項（由 PlayRecords 带出）
3. 可同時新增其他商品或優惠券
4. 確認後寫入 `Orders` 與 `OrderItems`，狀態改為「已結帳」
5. 會員「累積遊玩時數」正確增加

**狀態標記：** 遊玩中（Playing）、已完成待結帳（Completed）、已結帳（CheckedOut）

### 5.6 優惠券設定
- 優惠券：新增、修改、發放、查看使用情形
- 支援不限張數（`TotalQuantity == null`）
- POS 自動過濾已用完或已過期的 coupon

### 5.7 報表功能（每月報表）
| 報表類型 | 內容 |
|----------|------|
| 營收報表 | 總營收、成本、獲利（毛利） |
| 暢銷商品排行 | 商品銷售TOP10 |
| 會員消費排行 | 會員消費金額TOP10 |
| 等級分布統計 | 各等級會員人數 |

- 支援 CSV 匯出

### 5.8 活動公告
- 管理者可發布活動公告（標題、內容、圖片URL、活動日期、報名截止時間、人數上限）
- **圖片上傳：** 廢除，改為直接輸入圖片 URL（與商品一致）
- 行事曆檢視（每月月曆，活動日期標記）
- **報名人數：** 日曆上顯示各活動目前報名人數/上限
- **報名流程：** 前台「我要報名」按鈕 → 登入判斷 → 預填會員姓名電話（readonly）→ 送出報名
- **防重複報名：** 同電話+同活動不可重複報名
- **會員報名列表：** 會員可查看自己所有報名的活動

---

## 6. 前台會員功能

### 6.1 會員註冊與登入
- 填寫基本資料註冊
- **註冊頁警語：** 成為正式會員須至店面支付 200 元工本費
- 登入後導向 `/Member`
- 管理員登入導向 `/Admin`

### 6.2 會員專區（我的頁面）
- 查看會員資料與當前等級
- 查看優惠券擁有情形
- 遊戲租借記錄查詢（含申請、查詢）
- 空間訂位預約申請
- **側邊攔統一：** 所有會員頁面使用同一側邊導航 `_MemberNav`（6 項目：我的資料/我的訂單/我的優惠券/訂位預約/遊戲租借/活動報名）

### 6.3 價目表頁
- 前台公開，無需登入即可瀏覽
- 導航列「活動行事曆」旁邊連結至此頁
- 內容含：現場遊玩場地費（平日/假日）、包日方案、包廂費、遊戲購買/租借說明、優惠券說明、其他收費規則

---

## 7. 非功能性需求

### 7.1 技術建議
- 使用 ASP.NET Core MVC（.NET 8）
- 資料庫：SQLite，未來可遷移至 PostgreSQL（Render 已驗證可行）
- 前台響應式設計（支援手機操作）
- 會員密碼使用 BCrypt 雜湊儲存（cost factor 12）

### 7.2 效能需求
- 會員人數：100人
- 訂單量：每月500筆以內

---

## 8. 部署架構

### 8.1 採用方案
**Render 免費方案 + 免費子網域（onrender.com）**

- ASP.NET Core MVC + SQLite 架構，部署至 Render
- Render 自動提供 HTTPS / SSL 憑證
- 免費版實例會睡眠（15分鐘無流量），靜態檔案（uploads/）不持久化
- **注意：** 圖片請使用 URL 方式儲存，避免檔案丢失

### 8.2 資料備份
- SQLite 資料庫檔案（`.db`）每次部署時自動持久化
- 建議每月手動匯出 CSV 留存

### 8.3 安全性建議
- 後台路徑加強驗證（登入頁 + 複雜密碼）
- 管理者密碼使用 BCrypt 雜湊儲存

---

## 9. 後台 UI 統一規範

所有使用 `_AdminLayout` 的後台頁面，統一以下規範：

| 元素 | 規範 |
|------|------|
| 頁面標題 | `<div class="d-flex justify-content-between align-items-center mb-4"><h3>標題</h3>[按鈕]</div>` |
| 內容卡片 | `<div class="card"><div class="card-body">[內容]</div></div>` |
| 表格 | `<table class="table table-striped table-hover">` |
| 主要按鈕 | `btn btn-primary` |
| 次要按鈕 | `btn btn-outline-secondary` |
| 新增按鈕 | `+ 新增XXX`，`btn btn-primary`（無 btn-sm） |
| 匯入按鈕 | `匯入`，`btn btn-outline-secondary`，位置在新增左邊 |
| Alert | `class="alert alert-xxx mb-4"` |

---

## 10. 前台、後台頁面架構

### 10.1 前台頁面架構

```
首頁 /
├── 登入 / 帳號註冊
├── 會員專區 /Member
│   ├── 我的資料
│   ├── 我的訂單
│   ├── 我的優惠券
│   ├── 訂位預約
│   ├── 遊戲租借
│   └── 活動報名
├── 價目表 /Pricing
├── 活動行事曆 /Events
│   ├── 活動詳情 /Events/Details/{id}
│   └── 報名頁 /Events/RegisterPage/{id}
└── 會員登出
```

### 10.2 後台頁面架構

```
/Admin
├── 儀表板（首頁）
│   ├── 🎮 進行中的遊玩
│   ├── 🎲 遊戲租借（審核中/已通過/已借出）
│   ├── 📅 訂位預約
│   └── 📋 報名中的活動
│
├── 會員管理 /Members
├── 等級管理 /Levels
├── 商品管理 /Products
├── 快速消費 POS /Pos
├── 遊玩紀錄 /PlayRecords
├── 遊戲租借 /GameRentals
├── 訂單管理 /Orders
├── 優惠券管理 /Coupons
├── 報表 /Reports
├── 活動管理 /Events
└── 管理者登出
```

### 10.3 URL 路由對照表

| 功能 | 前台路由 | 後台路由 |
|------|----------|----------|
| 首頁 | `/` | `/Admin` |
| 登入 | `/Account/Login` | `/Admin/Login` |
| 註冊 | `/Account/Register` | — |
| 會員專區 | `/Member` | `/Members` |
| 價目表 | `/Pricing` | — |
| 活動行事曆 | `/Events` | `/Events` |
| 活動報名 | `/Events/MyRegistrations` | — |
| 遊戲租借（會員） | `/GameRentals` | `/GameRentals` |
| 訂位預約（會員） | `/SpaceReservations` | `/SpaceReservations` |
| 快速消費 | — | `/Pos` |
| 優惠券 | `/Member/Coupons` | `/Coupons` |
| 訂單 | — | `/Orders` |
| 報表 | — | `/Reports` |

---

## 11. 資料庫結構

### 11.1 實體關聯圖

```
Members ─────< Orders
Levels ────< Members
Members ─────< MemberCoupons ────< Coupons
Members ─────< PlayRecords
Members ─────< GameRentals
Members ─────< SpaceReservations
Members ─────< EventRegistrations
Orders ─────< OrderItems
Products ────< OrderItems
Products ────< RestockRecords
Products ─────< GameRentals
Events ────< EventRegistrations
```

### 11.2 資料表欄位定義

#### 1. Members（會員）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Name | string(100) | | 姓名 |
| Phone | string(20) | | 電話（唯一） |
| Email | string(255) | | Email（唯一） |
| PasswordHash | string(255) | | BCrypt 密碼 |
| Birthday | DateTime | ✓ | 生日（選填） |
| TotalPlayHours | decimal(10,2) | | 累積遊玩時數，預設0 |
| TotalSpending | decimal(10,2) | | 累積消費金額，預設0 |
| LevelId | int | | 當前等級FK，預設1 |
| Status | bool | | 啟用/停用，預設true |
| CreatedAt | DateTime | | 註冊日期 |
| UpdatedAt | DateTime | | 更新時間 |

#### 2. Levels（會員等級）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Name | string(50) | | 等級名稱 |
| UpgradeThresholdHours | decimal(10,2) | | 升級門檻（時數），預設0 |
| UpgradeThresholdAmount | decimal(10,2) | | 升級門檻（金額），預設0 |
| GameDiscount | decimal(3,2) | | 遊戲購買折扣（0.90=9折），預設1.0 |
| WeekdayHourlyRate | decimal(10,0) | | 平日場地費/小時 |
| HolidayHourlyRate | decimal(10,0) | | 假日場地費/小時 |
| SortOrder | int | | 排序，數字越小越前面 |
| IsDefault | bool | | 是否為預設等級（供新會員使用） |
| IsDeletable | bool | | 是否可刪除（非會員不可刪） |
| CreatedAt | DateTime | | 建立時間 |

**預設資料：**
| 等級 | UpgradeThresholdHours | UpgradeThresholdAmount | GameDiscount | WeekdayRate | HolidayRate | IsDefault | IsDeletable |
|------|----------------------|------------------------|--------------|-------------|-------------|----------|-------------|
| 非會員 | 0 | 0 | 1.00 | 60 | 70 | true | false |
| 會員 | 1000 | 100000 | 0.90 | 50 | 60 | false | true |

#### 3. Products（商品）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Category | string(50) | | 類別（桌遊/零食/飲料/服務） |
| Name | string(200) | | 名稱 |
| Description | string(1000) | ✓ | 說明 |
| Price | decimal(10,0) | | 售價 |
| Stock | int | ✓ | 庫存，實體商品預設0，服務類型為 null |
| LowStockAlert | int | | 低庫存警示值，預設1 |
| ImageUrl | string(500) | ✓ | 圖片URL |
| IsActive | bool | | 上架/下架，預設true |
| IsService | bool | | 是否為服務項目，預設false |
| CreatedAt | DateTime | | 建立時間 |
| UpdatedAt | DateTime | | 更新時間 |

**服務項目識別（IsService）：**
- `IsService = false`：實體商品，POS 結帳時需扣減庫存
- `IsService = true`：服務項目（例：遊玩服務、空間租借），不扣庫存

#### 4. Orders（訂單主檔）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| OrderType | string(50) | | 類型（Product/Play/Space/GameRental） |
| MemberId | int | ✓ | 會員FK（非會員null） |
| MemberName | string(100) | ✓ | 消費者姓名 |
| MemberPhone | string(20) | ✓ | 電話 |
| TotalAmount | decimal(10,0) | | 應收金額（原價合計） |
| DiscountAmount | decimal(10,0) | | 折扣金額 |
| FinalAmount | decimal(10,0) | | 實收金額 |
| PointsUsed | int | | 使用積分，預設0 |
| PointsEarned | int | | 獲得積分，預設0 |
| CouponId | int | ✓ | 使用優惠券FK |
| PaymentStatus | string(50) | | 付款狀態（Paid/Unpaid），預設Paid |
| PaymentMethod | string(50) | ✓ | 付款方式（Cash/Transfer） |
| Notes | string(500) | ✓ | 備註 |
| CreatedAt | DateTime | | 建立時間 |

#### 5. OrderItems（訂單明細）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| OrderId | int | | 訂單FK |
| ItemType | string(50) | | 項目類型（Product/Play/Space/GameRental） |
| ItemId | int | | 項目ID |
| ItemName | string(200) | | 項目名稱（留存） |
| UnitPrice | decimal(10,0) | | 單價 |
| Quantity | int | | 數量，預設1 |
| Subtotal | decimal(10,0) | | 小計 |

#### 6. Coupons（優惠券）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Name | string(100) | | 名稱 |
| CouponType | string(50) | | 類型（FixedAmount/Percentage） |
| DiscountValue | decimal(10,0) | | 折扣值 |
| MinPurchase | decimal(10,0) | | 最低消費，預設0 |
| ApplicableTo | string(50) | | 適用範圍（All/Product/Play/Space） |
| TotalQuantity | int | ✓ | 發行數量，null=不限張數 |
| UsedCount | int | | 已使用數量，預設0 |
| ValidFrom | DateTime | | 開始日期 |
| ValidUntil | DateTime | | 截止日期，null=不限時間 |
| IsActive | bool | | 是否啟用，預設true |
| CreatedAt | DateTime | | 建立時間 |

**Seed Data（自動建立）：**
- ID=1：會員:購買桌遊9折（ApplicableTo=Product，DiscountValue=10，Percentage）
- ID=2：生日禮:生日當月3人同行壽星免場地費（ApplicableTo=Play，DiscountValue=100，Percentage）

#### 7. MemberCoupons（會員持有優惠券）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| MemberId | int | | 會員FK |
| CouponId | int | | 優惠券FK |
| ReceivedAt | DateTime | | 取得時間 |
| UsedAt | DateTime | ✓ | 使用時間 |
| OrderId | int | ✓ | 使用訂單FK |

#### 8. PointTransactions（積分異動紀錄）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| MemberId | int | | 會員FK |
| OrderId | int | ✓ | 訂單FK |
| Type | string(50) | | 類型（Earn/Redeem/Expire/Adjust） |
| Points | int | | 積分（正負） |
| Balance | int | | 異動後餘額 |
| Description | string(200) | ✓ | 說明 |
| CreatedAt | DateTime | | 時間 |

#### 9. PlayRecords（遊玩紀錄）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| MemberId | int | ✓ | 會員FK（非會員null） |
| MemberName | string(100) | ✓ | 消費者姓名 |
| MemberPhone | string(20) | ✓ | 電話 |
| StartTime | DateTime | | 開始時間 |
| EndTime | DateTime | ✓ | 結束時間 |
| TotalHours | decimal(10,2) | ✓ | 總時數（無條件進位） |
| HourlyRate | decimal(10,0) | | 時單價（依等級） |
| Amount | decimal(10,0) | | 金額 |
| OrderId | int | ✓ | 訂單FK |
| Status | string(50) | | 狀態（Playing/Completed/CheckedOut），預設Playing |
| CreatedAt | DateTime | | 建立時間 |

#### 10. GameRentals（遊戲租借）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| MemberId | int | ✓ | 會員FK（非會員null） |
| ProductId | int | | 遊戲商品FK |
| PickupDate | DateTime | ✓ | 希望取件日期時間 |
| BorrowDate | DateTime | | 實際借出日期 |
| DueDate | DateTime | | 應還日期 |
| ReturnDate | DateTime | ✓ | 實際歸還日期 |
| Deposit | decimal(10,0) | | 押金，預設0 |
| RentalFee | decimal(10,0) | | 租借費用 |
| Status | string(50) | | 狀態（Pending/Approved/Borrowed/Renewed/Returned/Overdue/Rejected） |
| OrderId | int | ✓ | 訂單FK |
| CreatedAt | DateTime | | 建立時間 |

**狀態流程：** 審核中（Pending）→ 已通過（Approved）→ 已借出（Borrowed）→ 已歸還（Returned）
**押金：** 會員免押金（LevelId > 1），非會員 = 遊戲定價
**OrderId：** 押金不計入訂單金額，只算租金

#### 11. SpaceReservations（訂位預約）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| MemberId | int | ✓ | 會員FK |
| Name | string(100) | | 姓名 |
| Phone | string(20) | | 電話 |
| ReservationDate | DateTime | | 預約日期 |
| StartTime | TimeSpan | | 開始時段 |
| EndTime | TimeSpan | | 結束時段 |
| PeopleCount | int | | 預約人數，預設2 |
| SpaceType | string(20) | | 空間類型（訂位/包廂），預設「訂位」 |
| Hours | int | | 時數 |
| HourlyRate | decimal(10,0) | | 時單價 |
| TotalAmount | decimal(10,0) | | 金額 |
| Status | string(50) | | 狀態（Pending/Approved/Rejected/Cancelled） |
| OrderId | int | ✓ | 訂單FK |
| Notes | string(500) | ✓ | 備註 |
| CreatedAt | DateTime | | 建立時間 |

#### 12. Events（活動公告）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Title | string(200) | | 標題 |
| Content | string(4000) | | 內容（支援換行，`white-space: pre-line`） |
| ImageUrl | string(500) | ✓ | 圖片URL（非上傳） |
| EventDate | DateTime | ✓ | 活動日期 |
| MaxParticipants | int | ✓ | 人數上限，null=無上限 |
| RegistrationDeadline | DateTime | ✓ | 報名截止時間 |
| Status | string(50) | | 狀態（RegistrationOpen/RegistrationClosed/Ended） |
| CreatedAt | DateTime | | 建立時間 |
| UpdatedAt | DateTime | | 更新時間 |

#### 12b. EventRegistrations（活動報名）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| EventId | int | | 活動FK |
| Name | string(100) | | 報名人姓名 |
| Phone | string(20) | | 報名人電話 |
| CreatedAt | DateTime | | 報名時間 |

> **防重複報名：** 同電話+同活動不可重複報名
> **會員報名配對：** 目前用電話配對，報名頁預填會員姓名電話（readonly）

#### 13. RestockRecords（進貨記錄）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| ProductId | int | | 商品FK |
| Quantity | int | | 進貨數量 |
| Supplier | string(200) | ✓ | 供應商 |
| Phone | string(20) | ✓ | 供應商電話 |
| Notes | string(500) | ✓ | 備註 |
| CreatedAt | DateTime | | 建立時間 |

#### 14. PointSettings（積分系統設定）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| EarnRate | decimal | | 消費獲得積分比率（每消費1元獲得多少積分），預設1 |
| RedeemRate | decimal | | 積分折抵金額比率（每1積分折抵多少元），預設1 |
| MinRedeemPoints | int | | 最低折抵積分門檻，預設100 |
| ApplicableLevelId | int | | 適用會員等級（0=全部會員），預設0 |
| IsEnabled | bool | | 功能開關，預設false（停用） |
| Description | string(500) | ✓ | 設定說明備註 |
| CreatedAt | DateTime | | 建立時間 |
| UpdatedAt | DateTime | | 更新時間 |

#### 15. Admins（管理者帳號）
| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| Id | int | | 主鍵，自動流水 |
| Username | string(100) | | 帳號（唯一） |
| PasswordHash | string(255) | | BCrypt 密碼（cost factor 12） |
| Name | string(100) | | 姓名 |
| Role | string(50) | | 角色（Owner/Staff），預設Owner |
| IsActive | bool | | 啟用狀態，預設true |
| CreatedAt | DateTime | | 建立時間 |

**預設管理者：** admin / admin123

### 11.3 備註
- **升級邏輯：** 會員同時滿足「時數門檻」與「金額門檻」時自動升級（非會員除外）
- **非會員識別：** MemberId 為 null 者為非會員，姓名電話可不填
- **BCrypt：** 密碼一律使用 BCrypt 雜湊（cost factor 12）

---

_本文件將隨開發推進持續更新，若有新增需求或修改，請告知研助。_
