# AI 模拟面试与能力提升系统

面向计算机相关专业学生的多服务面试训练平台。项目采用前后端分离架构，围绕“练习 -> 追问 -> 评分 -> 报告 -> 提升建议”这条主链路，提供岗位化模拟面试、报告生成、资源推荐和知识库处理能力。

当前仓库以可本地运行、可继续开发为目标，包含 `frontend`、`backend`、`ai-service` 三个核心服务，以及 Docker Compose、Windows 一键脚本和根目录联动启动入口。

## 当前已落地的主要能力

- 用户注册、登录、JWT 刷新、个人资料更新
- 岗位列表与题库查询
- 模拟面试创建、回答提交、结束面试、历史记录查询
- 面试报告、成长趋势、资源推荐、训练计划查询
- SignalR 实时面试通道
- 管理端题库、知识库文档、AI 设置相关接口
- FastAPI AI 服务的面试、评分、报告、推荐、RAG、文档处理接口

说明：

- `Docs/ARCHITECTURE.md`、`Docs/API.md` 等文档包含更完整的目标架构与设计说明
- README 优先描述当前仓库的实际启动方式、目录结构和可落地开发路径

## 技术栈与服务

| 服务 | 技术栈 | 作用 | 默认访问地址 |
| --- | --- | --- | --- |
| `frontend` | Next.js 16 + React 19 + TypeScript + Tailwind CSS 4 | 登录、控制台、面试、报告、历史、资源、管理页面 | 本地开发默认 `http://localhost:3000`；Docker 默认 `http://localhost:3001` |
| `backend` | ASP.NET Core 8 + EF Core + SignalR + PostgreSQL + Redis | 业务主入口、认证、面试流程、报告、推荐、知识库、实时推送 | `http://localhost:8080` |
| `ai-service` | FastAPI + Pydantic + Celery + Redis | 面试问答、评分、报告、推荐、RAG、文档处理 | `http://localhost:8000` |
| `postgres` | pgvector/pg15 | 主数据库，承载结构化数据与向量能力 | `localhost:5433` |
| `redis` | Redis 7 | 缓存、会话、队列相关能力 | `localhost:6379` |

## 仓库结构

```text
.
├── Docs/                         # 架构、接口、数据库、设计等文档
├── frontend/                     # Next.js 前端
├── backend/                      # ASP.NET Core Web API
├── ai-service/                   # FastAPI AI 服务
├── scripts/                      # 根目录启动辅助脚本
├── storage/                      # PostgreSQL、Redis、上传文件、密钥等本地数据
├── docker-compose.yml            # 多服务容器编排
├── .env.example                  # 运行环境变量模板
├── start.ps1                     # Windows 一键启动脚本
└── stop.ps1                      # Windows 一键停止脚本
```

更细的代码入口：

- 前端页面：`frontend/src/app`
- 后端接口：`backend/src/AiInterview.Api/Controllers`
- AI 服务路由：`ai-service/app/api/routes`

## 环境要求

- Node.js 18+
- npm
- .NET SDK 8
- Docker Desktop
- Python 3.12+
- `uv`
- Windows PowerShell 5.1 或 PowerShell 7+（如果要使用 `start.ps1` / `stop.ps1`）

## 先准备环境变量

建议先复制运行配置文件：

```powershell
Copy-Item .env.example .env.run
```

`.env.run` 是 Docker Compose 与部分本地开发流程的统一配置来源。默认值包括：

- 前端端口：`3001`
- 后端端口：`8080`
- AI 服务端口：`8000`
- PostgreSQL：`localhost:5433`
- Redis：`localhost:6379`
- 数据库：`ai_interview`
- 默认种子数据：`SEED_ENABLED=true`

## 启动方式总览

| 场景 | 推荐命令 | 说明 |
| --- | --- | --- |
| 想最快体验整套服务 | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1 -Full` | Windows 下最省事，脚本会在后台拉起服务并写日志 |
| 想用容器跑完整环境 | `docker compose --env-file .env.run up --build -d` | 同时启动前端、后端、AI 服务、PostgreSQL、Redis、Celery Worker |
| 想本地联调前后端 | `npm run dev` | 自动确保 PostgreSQL 和 Redis 可用，然后启动前端与后端 |
| 想本地联调前后端 + AI 服务 | `npm run dev:full` | 在 `dev` 基础上额外启动 `ai-service` |

## 方式一：Docker Compose

启动：

```powershell
docker compose --env-file .env.run up --build -d
```

查看状态：

```powershell
docker compose --env-file .env.run ps
```

停止：

```powershell
docker compose --env-file .env.run down
```

默认访问地址：

- 前端：`http://localhost:3001`
- 后端：`http://localhost:8080`
- 后端 Swagger：`http://localhost:8080/swagger`
- 后端健康检查：`http://localhost:8080/health`
- AI 服务健康检查：`http://localhost:8000/health`

适合场景：

- 需要完整还原多服务协同环境
- 需要容器化数据库和 Redis
- 需要连同 `celery-worker` 一起验证编排

## 方式二：Windows 一键脚本

普通开发：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1
```

完整演示：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1 -Full
```

停止服务：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\stop.ps1
```

常用补充参数：

- 保留日志：`.\stop.ps1 -KeepLogs`
- PID 文件丢失时按端口清理：`.\stop.ps1 -CleanByPort`

脚本特点：

- 自动做依赖预检
- 自动确保 PostgreSQL / Redis 可用
- 启动成功后返还当前终端控制权
- 日志写入根目录 `.dev-logs/`
- 优先使用前端 `3000`，若被占用会回退到 `3001`

适合场景：

- Windows 本机演示
- 希望后台启动服务，同时保留当前终端继续操作

## 方式三：根目录本地联动开发

先安装根目录依赖：

```powershell
npm install
```

说明：

- 根目录 `postinstall` 会自动补齐 `frontend` 依赖
- 后端由 `dotnet run` 直接启动
- AI 服务由 `uv run` 启动，首次执行可能会花一点时间准备环境

启动前后端：

```powershell
npm run dev
```

启动前后端 + AI 服务：

```powershell
npm run dev:full
```

单独启动单个服务：

```powershell
npm run dev:frontend
npm run dev:backend
npm run dev:ai-service
```

本地联调默认地址：

- 前端：`http://localhost:3000`
- 后端：`http://localhost:8080`
- AI 服务：`http://localhost:8000`

补充说明：

- `npm run dev` / `npm run dev:full` 在启动应用前，会先执行 `scripts/predev.mjs`
- 该流程会尝试确保 Docker 中的 PostgreSQL 和 Redis 已启动
- 任一子进程退出时，其余联动子进程也会一起结束，减少残留进程

## 配置与数据说明

### 数据库与缓存默认值

后端本地开发默认连接：

- PostgreSQL：`Host=localhost;Port=5433;Database=ai_interview;Username=postgres;Password=postgres`
- Redis：`localhost:6379,abortConnect=false`

后端启动时会自动执行数据库迁移；当 `SEED_ENABLED=true` 时，会自动写入种子数据。

### AI 服务默认配置

`.env.example` 当前默认使用：

```env
AI_SERVICE_MODEL_PROVIDER=mock
```

这意味着仓库默认以本地可运行、可演示为优先，AI 服务默认走 `mock provider`。如果要接入真实模型，需要同步调整对应的 AI 服务与后端配置，而不是只修改单个环境变量。

### 本地数据目录

- `storage/postgres`：PostgreSQL 数据
- `storage/redis`：Redis 持久化数据
- `storage/uploads`：知识库上传文件
- `storage/dp-keys`：ASP.NET Core Data Protection 密钥

## 默认演示账号

在启用种子数据时可直接使用：

- 普通用户：`zhangsan / Pass1234`
- 管理员：`admin / Admin1234`

## 常用文档索引

- [接口文档](./Docs/API.md)
- [系统架构](./Docs/ARCHITECTURE.md)
- [数据库设计](./Docs/DATABASE.md)
- [设计系统](./Docs/DESIGN.md)
- [真实 AI 面试主链路交付说明](./Docs/真实AI面试主链路-交付说明.md)

阅读建议：

- 想了解业务接口，先看 `Docs/API.md`
- 想了解整体分层和职责边界，先看 `Docs/ARCHITECTURE.md`
- 想了解当前数据表和字段设计，先看 `Docs/DATABASE.md`

## 本地验证命令

前端构建：

```powershell
cd frontend
npm run build
```

后端构建：

```powershell
cd backend
dotnet build .\src\AiInterview.Api\AiInterview.Api.csproj
```

AI 服务测试：

```powershell
cd ai-service
uv run pytest
```

如果你只修改了某一层，优先跑对应层的构建或测试即可。

## 常见问题

### 1. `password authentication failed for user "postgres"`

优先检查：

1. PostgreSQL 是否真的启动在 `localhost:5433`
2. `.env.run` 里的 `POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB` 是否仍是预期值
3. 是否有旧的 `storage/postgres` 数据卷沿用了历史密码
4. 是否同时跑了 Docker Compose 全套服务和本地 `npm run dev`，造成连接目标混乱

### 2. 前端端口为什么有时是 `3000`，有时是 `3001`

- 本地 `npm run dev` 默认是 `3000`
- Docker Compose 默认把前端暴露到 `3001`
- `start.ps1` 会优先尝试 `3000`，被占用时回退到 `3001`

### 3. 本地 `npm run dev` 为什么也要求 Docker Desktop

因为本地联调默认仍依赖 Docker 中的 PostgreSQL 和 Redis。应用层是本机启动，基础设施层默认还是容器提供。

## 维护原则

如果 README 与当前代码行为冲突，请优先以最近验证过的实现为准，并同步更新文档。  
如果你正在扩展接口、页面或启动脚本，建议一并更新本文件和 `Docs/` 下对应专题文档。
