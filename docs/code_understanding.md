# 代码理解文档 (Code Understanding)

本文档旨在梳理“南京线体标准界面”项目的核心架构、组件功能及交互逻辑。

## 1. 总体架构
本项目是一个全栈工业监控与扫码系统，采用 **前后端分离** 架构：
- **前端**: Vue 3 + TypeScript + Vite + Tailwind CSS (或 Scss)。
- **后端**: .NET 8 (ASP.NET Core Minimal API) + SignalR + S7.Net。
- **通讯**:
    - **MES**: 通过 HTTP API 进行工单查询与工艺路线获取。
    - **PLC**: 通过 S7 协议（S7.Net）实现西门子 PLC 的寄存器读写。
    - **扭矩枪**: 通过 OpenProtocol (TCP) 与马头 (Desoutter) 控制器通讯。
    - **实时日志**: 通过 SignalR (TorqueHub) 将后端状态实时推送至前端。

## 2. 前端核心组件 (src/)

| 组件 / 文件 | 职责说明 |
| :--- | :--- |
| `App.vue` | 项目主入口，负责全局状态管理（工单、工步、日志）、扫码逻辑及整体布局。 |
| `PlcInteraction.vue` | PLC 调试与监控面板，支持输入地址进行即时读写测试。 |
| `TorqueInteraction.vue` | 扭矩枪通讯面板，处理拧紧程序的下发与结果接收。 |
| `MaterialScanner.vue` | 物料扫码验证逻辑，确保生产物料与工艺要求匹配。 |
| `ConfigModal.vue` | 系统参数配置（API 地址、PLC/扭矩枪 IP 等），数据持久化在 LocalStorage。 |
| `services/mesApi.ts` | 封装 MES 系统相关的 Axios 请求。 |

## 3. 后端核心服务 (backend/)

### 核心逻辑
- **Program.cs**: 配置中间件、CORS、SignalR 映射及 Minimal API 路由。
- **PlcService.cs**: 继承 `BackgroundService` 的后台任务，维护与 PLC 的长连接，提供 `ReadValueAsync` 和 `WriteValueAsync` 方法。
- **TorqueControllerService.cs**: 处理扭矩枪 TCP 通讯，解析 OpenProtocol 数据包。

### 关键 API 端点
- **PLC 操作**:
    - `POST /api/plc/config`: 更新并重连 PLC 配置。
    - `GET /api/plc/read`: 读取指定地址的数值。
    - `POST /api/plc/write`: 向指定地址写入数值。
- **扭矩枪操作**:
    - `POST /api/command`: 发送扭矩程序指令 (MID 0018, 0043 等)。

## 4. 关键业务流程
1. **扫码查询**: 用户扫描产品条码 -> `App.vue` 调用 `mesApi` -> 获取工单及工艺工步 -> 自动展开“定扭判定矩阵”。
2. **实时反馈**: 后端服务（PLC/Torque）在执行过程中，通过 `HubContext` 向前端推送 `ReceiveLog` 事件，前端实时更新“操作日志”标签页。
3. **PLC 交互**: 前端通过 `PlcInteraction` 组件发送 HTTP 请求到后端，后端利用 `S7.Net` 与硬件通讯后返回结果。

## 5. 待优化点 (基于 Karpathy Guidelines)
- **代码重复**: 定扭判定矩阵的逻辑目前主要在 `App.vue` 中，可考虑进一步组件化。
- **错误处理**: PLC 读写中的地址校验可以更健壮，避免非法字符串导致后端报错。
- **状态同步**: 扭矩枪的连接状态可增加心跳机制并显示在 UI 显著位置。
