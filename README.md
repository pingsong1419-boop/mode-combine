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


🔴 严重问题（可能导致运行时错误或安全隐患）
1. 前端：使用未定义的变量 isPushing
位置： src/App.vue 第 1208–1209 行

<button :disabled="... || isPushing" @click="handleFinalConfirm">
  {{ isPushing ? '正在上传...' : '确认并提交' }}
</button>
问题： 模板中引用了 isPushing，但在 <script setup> 中从未定义。这会导致 Vue 运行时抛出警告/错误，且按钮的禁用状态和文案逻辑完全失效。

建议： 在 App.vue 的 script 顶部添加：

const isPushing = ref(false)
并在 handleFinalConfirm 中正确管理该状态。

2. 后端：Proxy 端点存在 SSRF 漏洞
位置： backend/MesScanner.Backend/Program.cs 第 89–114 行

app.MapPost("/api/proxy", async (HttpContext context, IHttpClientFactory clientFactory) => {
    var targetUrl = context.Request.Query["url"].ToString();
    // ...
    var response = await client.PostAsync(targetUrl, content);
});
问题： /api/proxy 接受任意 URL 且没有白名单校验。攻击者可利用此端点访问内网服务、扫描内网端口，或攻击其他内部系统。

建议： 增加 URL 白名单校验，或限制只能访问已配置的 MES 域名/IP。

3. 后端：CORS 配置过于宽松
位置： backend/MesScanner.Backend/Program.cs 第 15–24 行

policy.SetIsOriginAllowed(_ => true)
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
问题： AllowAnyOrigin + AllowCredentials 的组合在生产环境中是高危配置，会导致会话劫持等安全风险。

建议： 显式配置允许的源（Origin），而非允许全部。

4. 后端：配置文件写入缺乏并发控制
位置： backend/MesScanner.Backend/Program.cs 第 133–146、158–171 行

问题： /api/config 和 /api/recipe/config 直接进行文件写操作，没有文件锁。若两个请求同时到达，可能导致配置文件损坏。

建议： 使用 SemaphoreSlim 或文件锁进行串行化写操作。

🟠 中等问题（影响可维护性与稳定性）
5. 前端：App.vue 过于庞大（1466 行）
位置： src/App.vue

问题： 该文件几乎承载了整个应用的核心状态、业务逻辑、矩阵计算、SignalR 通信、多个 Tab 的渲染逻辑以及大量 CSS。严重违反单一职责原则，维护成本极高。

建议：

将条码矩阵相关逻辑抽取为 useBarcodeMatrix() composable
将 SignalR 连接逻辑抽取为 useSignalR() composable
将 CSS 拆分到独立样式文件或各子组件中
6. 前端/后端：多处使用 any 类型，类型安全不足
位置：

src/App.vue：SignalR 回调（ReceiveHeartbeat、ReceivePlcTrigger 等）均使用 data: any
src/services/mesApi.ts：checkSingleMaterial、checkDuplicateBarcode、getCellData 返回 Promise<any>
src/components/MaterialScanner.vue：(ws as any).workStepMaterialList
问题： 丢失了 TypeScript 的静态类型检查优势，重构时极易引入隐性 Bug。

建议： 为 MES 接口返回的数据结构补充准确的 Interface/Type，并替换所有 any。

7. 前端：barcodeValidationResults 存在内存泄漏风险
位置： src/App.vue 第 204 行

const barcodeValidationResults = reactive<Record<string, { single: string, duplicate: string }>>({})
问题： 该字典随着每次新工单、新条码的扫描会无限累积，旧的条码校验结果不会被清理。长时间运行可能导致内存持续增长。

建议： 在 resetAll() 或切换新工单时，清空该对象。

8. 后端：PlcService 中 PLC 连接缺少超时控制
位置： backend/MesScanner.Backend/Services/PlcService.cs 第 136 行

await Task.Run(() => _plc.Open());
问题： S7.Net 的 Open() 方法是同步阻塞调用，若 PLC 网络不可达，可能长时间挂起，导致后台服务线程池耗尽。

建议： 使用带超时的连接策略，或在独立线程中进行连接并设置超时取消。

9. 后端：异常被静默吞没
位置：

PlcService.cs：LogToFrontend、LogToMonitor 使用空 catch { }
PlcService.cs：ReadLoopAsync 的 catch { }
TorqueControllerService.cs：ReadLoopAsync 的 catch { }
问题： 异常被完全吞掉，出现问题时没有任何日志记录，极难排查故障。

建议： 至少记录异常信息，如 _logger.LogError(ex, "...")。

10. 前端：handleScan 存在竞态条件
位置： src/App.vue 第 456–552 行

问题： 用户在查询过程中再次扫描（或快速按 Enter），会触发多次并行的 handleScan 执行。虽然设置了 orderLoading 标志，但内部的 await 间隙可能导致状态覆盖或重复弹窗。

建议： 在函数入口处立即检查并返回：

if (orderLoading.value) return
🟡 轻微问题与优化建议
11. 前端：handleFinalConfirm 逻辑与 UI 文案不符
位置： src/App.vue 第 845–847 行

async function handleFinalConfirm() {
  setOK() 
}
问题： 按钮文字是"确认并提交"，但实际只调用了 setOK()，没有任何"提交/上传"动作。用户可能误以为数据已上报 MES。

12. 前端：plcMonitorRef 声明未使用
位置： src/App.vue 第 28 行

const plcMonitorRef = ref<any>(null)
问题： 该 ref 没有绑定到任何组件或 DOM 元素，属于死代码。

13. 后端：配置路径强依赖运行时目录结构
位置： Program.cs 第 77 行、PlcService.cs 第 431 行

var rootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../"));
问题： 使用 ../../ 向上回溯寻找 Config 文件夹。若部署时更改了工作目录（如作为 Windows 服务运行），路径将失效。

建议： 使用 .NET 配置系统（IConfiguration、IHostEnvironment.ContentRootPath）或环境变量来定位配置目录。

14. 后端：TorqueControllerService 硬编码 IP/端口
位置： backend/MesScanner.Backend/Services/TorqueControllerService.cs 第 15–16 行

private readonly string _ip = "192.168.5.212";
private readonly int _port = 4545;
建议： 从 appsettings.json 或配置中心读取。

15. 后端：ReadCellBarcodesAsync 硬编码每节 40 字节
位置： PlcService.cs 第 250 行

int totalBytesToRead = layers * 40;
问题： 40 字节是西门子 String 类型的最大长度定义，但此魔法数字没有说明，且不可配置。

建议： 提取为常量或配置项，并添加注释说明。

16. 前端/后端：日志记录方式不统一
问题： 后端中同时使用 _logger.LogXxx 和 Console.WriteLine，不利于统一收集和过滤。

建议： 统一使用 ILogger 接口。

17. 前端：CSS 过度使用 !important
位置： src/App.vue 样式块（如 .tab-pane::-webkit-scrollbar、.text-center 等）

问题： 大量 !important 会降低样式可维护性，增加后续覆盖难度。



