# 前端说明

本目录承载模拟面试系统的前端应用，基于 Next.js App Router 构建，负责页面渲染、交互组织、前端状态管理、接口调用与实时连接，不负责后端业务规则、持久化和 AI 推理实现。

## 职责边界

- 负责页面展示、路由组织与交互流程。
- 负责登录态持久化、401 失效处理与前端会话恢复。
- 负责通过 HTTP 调用 ASP.NET Core 后端接口。
- 负责通过 SignalR 接收面试过程中的实时消息。
- 不负责数据库访问、服务端业务规则、AI Provider 调度和最终落库。

## 技术栈

- Next.js 16 + React 19 + TypeScript
- Tailwind CSS 4
- Axios：统一 HTTP 请求封装
- Zustand：前端认证与会话状态管理
- React Hook Form + Zod：表单管理与校验
- SignalR：实时面试消息连接
- ECharts：图表展示

视觉样式基于 `src/styles/tokens.css` 与 Tailwind 扩展统一管理。

## 目录结构

```text
src/
  app/          App Router 页面与布局
  components/   通用组件与业务组件
  features/     按领域划分的前端功能逻辑
  hooks/        自定义 Hook
  lib/          通用运行时辅助代码
  services/     HTTP、SignalR 等外部接口封装
  stores/       Zustand 状态管理
  styles/       全局样式与设计 Token
  types/        类型定义
  utils/        工具函数
```

当前 `app/` 下已经包含 `dashboard`、`interview`、`report`、`reports`、`admin`、`resources`、`history` 等页面模块。

## 与根目录开发入口的关系

前端目录可以单独运行，但仓库的主开发入口在根目录：

- 根目录 `npm run dev`：同时启动前端与 ASP.NET Core 后端。
- 根目录 `npm run dev:full`：同时启动前端、后端与 `ai-service`。
- 根目录 `npm run dev:frontend`：等价于在本目录执行 `npm run dev`。
- 根目录 `predev` / `predev:full`：会先执行本地基础设施检查，再自动补装 `frontend` 依赖。

如果你只改前端界面，可以在本目录单独启动；如果要联调接口或完整体验主流程，优先使用根目录脚本。

## 本地启动

### 仅启动前端

```powershell
npm install
npm run dev
```

默认访问地址为 `http://localhost:3000`。

### 从仓库根目录启动推荐开发流程

```powershell
npm install
npm run dev
```

这会先执行前置准备，再同时启动前端和后端。需要联调 AI 服务时使用：

```powershell
npm run dev:full
```

## 常用命令

在 `frontend/` 目录内可直接使用：

```powershell
npm run dev
npm run lint
npm run test
npm run build
npm run start
```

在仓库根目录也有对应入口：

```powershell
npm run dev:frontend
npm run lint:frontend
npm run test:frontend
npm run build:frontend
```

## 关键环境变量

### `NEXT_PUBLIC_API_BASE_URL`

- 用途：前端请求后端 API 与 SignalR Hub 的基础地址。
- 默认值：`http://localhost:8080`
- 使用位置：`src/services/http.ts`

未显式配置时，前端会默认把后端视为运行在 `http://localhost:8080`。

## 运行与构建说明

- `next.config.ts` 启用了 `output: "standalone"`，用于生成独立部署产物。
- `frontend/Dockerfile` 也按 standalone 方式构建和运行。
- Docker 构建阶段支持通过 `NEXT_PUBLIC_API_BASE_URL` 注入后端基础地址。

## 当前前端承担的关键客户端逻辑

- 请求层统一在 `src/services/http.ts` 处理基础地址与认证头。
- 收到 `401` 时会清空本地会话并唤起登录流程。
- 实时面试连接由 `src/services/interview-hub.ts` 负责创建与关闭。
- 认证状态通过 `src/stores/auth-store.ts` 持久化到浏览器存储。
- 面试进入流程与参数整理位于 `src/features/interview/`。

## 说明

本 README 只描述 `frontend/` 目录本身及其与仓库根脚本的关系。更完整的跨服务联调、后端与 AI 服务说明，请查看仓库根目录文档与对应子目录文档。
