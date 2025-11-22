# Node Server

這個伺服器包含 DDS 服務與 Realtime AI 控制服務。

## 技術與工具

- Node.js
- Jest（測試框架）

## 指令

### 安裝相依

```bash
npm i
```

### 啟動（Production）

```bash
npm start
```

### 開發模式（含檔案變更監聽）

```bash
npm run dev
```

### 測試

專案使用 **Jest** 作為測試框架：

```bash
npm test
# 或
npm t
```

Jest 會自動尋找 `*.test.ts` / `*.test.js` 等測試檔並執行。
