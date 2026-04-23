# MES 工序扫码与定扭集成系统 (MES Scanner System)

## 📌 项目简介
本项目是一个基于 Vue 3 + Vite 的前端应用，结合 .NET Core C# 后端，用于现代工厂车间或流水线上的**工序扫码防误**、**物料验证**和**定扭矩扳手（如马头 Desoutter 等遵循 Open Protocol 的控制器）的数据交互与集成**。

系统不仅能够调用厂内 MES 层 Web API 获取生产工单和工艺路线，还能基于工步清单校验投料操作。当进入拧紧工序时，结合后台的 TCP 长连接模块，能够向底层扳手发送控制指令并实时获取作业的力矩数据，最后将检测结果（OK/NG）下发至外部中控系统（例如 LabVIEW）。

---

## 📚 项目指南与规范
为方便后续维护和理解，项目包含以下详细说明：
*   **[项目理解文档](./docs/project_understanding.md)**：深入介绍系统架构、MES 对接逻辑及硬件集成详情。
*   **[开发规范与规则](./docs/coding_rules.md)**：后端 C# 与前端 Vue 3 协作时的命名、异步、类型等细则。
*   **[开发修改日志](./docs/development_log.md)**：记录项目每一步的修改记录与时间节点。


---


## 🏗️ 架构与技术栈

### 前端平台
- **核心框架:** Vue 3 (Composition API, `<script setup>`) + Vite
- **开发语言:** TypeScript
- **界面样式:** 原生 CSS / CSS Variables，独立模块化开发
- **MES 集成:** 开发环境下通过 Vite 配置了 HTTP Proxy 解决 `/mes-api` 的跨域阻断，请求实际的 MES 平台服务端点。
- **外部联动:** 利用 HTTP + SignalR 连接本地 C# 后端服务获取定扭扳手数据；利用预留的底层库（`labviewSignal.js`）向外部工控设备输送测试放行信号。

### 底层通信后端平台 (`MesScanner.Backend`)
- **核心框架:** .NET Core API
- **网络协议:** TCP/IP (底层适配 Open Protocol)、SignalR (WebSocket 降级推送)、HTTP REST
- **核心组件:** 
  - `TorqueControllerService.cs`: 实现了 `BackgroundService` 后台驻留任务的底层网络服务。可维持定扭控制器的 TCP 物理长连接，并通过发送 MID 9999 自主管理链路心跳。该模块实时解析控制箱上传的裸报文组合（如提取 MID 0061 里的实施力矩数值 `torqueVal`、角度 `angleVal` 等业务数据），再通过内部管道向 SignalR 广播放大。另外也暴露了控制器预制交互接口（MID 0018 下发 PSET 变更、MID 0043 工具使能）。
  - `TorqueHub.cs`: SignalR 连接枢纽，处理前后端事件传递。

---

## 📁 目录结构摘要
```text
MES-SCANNER/
├── backend/                   -> .NET Core 底层通信后端端源码
│   └── MesScanner.Backend/
│       ├── Services/          -> TorqueControllerService (与硬件TCP通信核心)
│       ├── Hubs/              -> SignalR Hub
│       └── Program.cs         -> 路由与应用入口设置
├── src/                       -> Vue 3 业务前端源码
│   ├── components/            -> 页面抽离模块 (MaterialScanner, TorqueInteraction, RouteTable, ApiDetail)
│   ├── services/              -> HTTP API 封装层 (与 MES 服务器业务交互)
│   ├── utils/                 -> 基础设施与底层库 (如 labviewSignal，对接本地控制台IO等)
│   └── App.vue                -> 整个 SPA 的顶层状态组装与界面总线
├── vite.config.ts             -> Vite 开发配置和 HTTP Proxy 核心配置
└── package.json               -> Node 构建包依赖清单
```

---

## 🚀 启动与调试开发

### 1. 运行前端业务工程
确保已经在本地安装 Node.js (推荐 v18+)。在项目根目录运行：
```bash
npm install   # 或 pnpm install
npm run dev
```
前端将在 `http://localhost:5173` 启动。

> **注意：** 若要连接真实产线的 MES 节点，你需要自行调整 `vite.config.ts` 中的 `proxy.target` （譬如将其置于本地调试内网环境里的目标 IP 等）。

### 2. 运行本地定扭通信服务
此服务为了绕开浏览器限制与设备局域网架构设计，须在运行设备上或者产线边缘机单独驻留。进入 `backend/MesScanner.Backend/` 目录：
```bash
cd backend/MesScanner.Backend
dotnet run
```
默认会在 `http://localhost:5246` 监听 HTTP 和 SignalR 通信。前端将自动尝试连接此 `http://localhost:5246/torqueHub` 节点并请求报文。


---

## 💡 核心业务模块逻辑简析
1. **条码匹配与请求下发配置**
   系统操作者在左侧面板扫描产品条码（`productCode`）。主业务通过 MES 系统 `/mes-api/api/OrderInfo/GetOtherOrderInfoByProcess` 请求，得到对应的当前工单参数，再提取其中的 `route_No` 通过接口 `GetTechRouteListByCode` 读取所需要经历的基础工步组集。
   
2. **物料清单 (BOM) 防错验证 (`MaterialScanner.vue`)**
   用户扫码后通过 “物料验证” 界面，程序会依照拿到的配方工步强校验当前操作人员投料操作或配件扫描的合法性（扫描值和系统预置清单比较等），以防范用错配件或遗漏。

3. **智能拧紧交互集控 (`TorqueInteraction.vue`)**
   进入 “定扭交互” Tab 时，开始和定扭网络进行握手。前端触发对应的流程并基于底层指令通过 API 设置螺栓程序的预设批号 (Job / Pset)。随后一线工人的真实下压作业将通过 Open Protocol 底层（MID 0061 包含测量值）经 SignalR 隧道传输到页面。

4. **IO 放行鉴定 / LabVIEW 下发**
   界面将组合判定整体批次结果 OK/NOK，并在完成业务后使用 `writeSignal` 方法推送释放和告警信号供底层产线系统（PLC、LabVIEW上位机等）捕获操作放行状态。
