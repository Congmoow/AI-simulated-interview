# AI 模拟面试与能力提升系统

这是一个多服务仓库，核心服务包括 `frontend`、`backend` 和 `ai-service`。

仓库保留 Docker Compose 启动方式，同时新增了根目录本地开发联动入口，方便直接在本地调试前后端。

## 仓库结构

```text
.
├── Docs/                  # 需求、架构与数据库文档
├── frontend/              # Next.js App Router + TypeScript + Tailwind
├── backend/               # ASP.NET Core Web API + SignalR + EF Core
├── ai-service/            # FastAPI + Celery
├── storage/               # 本地数据卷目录
├── docker-compose.yml
├── .env.example
└── .env.run
```

## 演示前检查清单

1. **Docker Desktop 已启动**（必须，后端依赖 PostgreSQL / Redis 容器）
2. **Node.js ≥ 18、.NET SDK ≥ 8、uv 已安装**（Full 模式额外需要 uv）
3. **已创建 `.env.run`**：`Copy-Item .env.example .env.run`
4. **端口未被其他进程占用**：3000（前端，被占时自动回退 3001）、8080（后端）、8000（AI 服务）
5. **如曾用 `docker compose up` 起过全套服务**，请先停止 frontend/backend/ai-service 容器，避免端口冲突

---

## 快速启动

### 1. 准备环境变量

建议先复制一份运行环境文件：

```powershell
Copy-Item .env.example .env.run
```

`.env.run` 是本地 Docker Compose 的统一来源，里面定义了 PostgreSQL、Redis、前端端口和各类服务地址。

### 2. 启动整套服务

```powershell
docker compose --env-file .env.run up --build -d
```

查看运行状态：

```powershell
docker compose --env-file .env.run ps
```

### 3. 默认访问地址

- 前端：http://localhost:3001
- 后端：http://localhost:8080
- 后端 Swagger：http://localhost:8080/swagger
- AI 服务健康检查：http://localhost:8000/health
- PostgreSQL：localhost:5433
- Redis：localhost:6379

## 本地开发

### 1. 安装根目录依赖

先在仓库根目录执行：

```powershell
npm install
```

首次执行时，根目录 `postinstall` 会自动补齐 `frontend` 依赖。

### 2. Windows 一键启动（推荐演示方式）

`start.ps1` 会在后台启动各服务，健康检查通过后**返还终端控制权**，不会永久阻塞当前窗口。

**环境要求：**

- Windows PowerShell 5.1 或 PowerShell 7+
- Node.js / npm
- .NET SDK 8
- Docker Desktop（用于 PostgreSQL / Redis）
- `uv`（仅 `-Full` 模式需要）
- 根目录已存在 `.env.run`

**普通开发（frontend + backend）：**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1
```

**完整演示（frontend + backend + ai-service）：**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1 -Full
```

启动后脚本输出各服务地址和 PID，终端即时解锁。日志写入项目根目录 `.dev-logs/`。

**停止所有服务：**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\stop.ps1
```

如需保留日志：`stop.ps1 -KeepLogs`  
如 PID 文件丢失，按端口强制清理：`stop.ps1 -CleanByPort`

**前端端口说明：** `start.ps1` 优先使用 3000，被占时 Next.js 自动回退 3001；脚本会从日志解析实际端口并在状态表中输出。

**脚本行为摘要：**

- `start.ps1` 启动前会先执行依赖预检，并确保 PostgreSQL / Redis 可用
- 若检测到上一次运行遗留的项目进程，会先按 PID 文件和项目端口清理旧进程
- 清理逻辑只针对项目相关端口与进程，已排除 Docker / 系统关键进程
- 子进程以独立隐藏窗口启动，避免控制台输出混叠
- 启动成功后会输出服务地址、PID 和日志目录 `.dev-logs/`
- `stop.ps1` 默认按 PID 树停止本项目服务；`-CleanByPort` 仅在 PID 文件缺失或残留时使用
- `stop.ps1 -KeepLogs` 会保留 `.dev-logs/`，便于排查问题

> 如果在已有 PowerShell 会话中运行，可直接 `.\start.ps1` 或 `.\start.ps1 -Full`；  
> 若 ExecutionPolicy 拦截，使用上方完整 `powershell.exe` 命令。

### 3. 启动前后端联动

```powershell
npm run dev
```

这个命令会同时启动：

- `frontend`
- `backend`

启动前会先检查并自动拉起 Docker PostgreSQL 和 Redis，然后再启动前端和后端。

输出日志会在同一个终端里显示，并带上清晰前缀：

- `[frontend]`
- `[backend]`

任一子进程退出时，`concurrently` 会一并结束其余子进程，减少孤儿进程残留。

本地开发默认端口如下：

- 前端：http://localhost:3000
- 后端：http://localhost:8080
- AI 服务：http://localhost:8000

### 4. 启动前后端 + AI 服务

```powershell
npm run dev:full
```

这个命令会在 `dev` 的基础上额外启动 `ai-service`。

如果你只需要前后端联调，不必使用这个命令。

### 5. 单独启动单个服务

```powershell
npm run dev:frontend
npm run dev:backend
npm run dev:ai-service
```

### 6. 本地开发依赖说明

后端本地开发默认依赖 PostgreSQL 和 Redis。  
后端实际读取的是 `backend/src/AiInterview.Api/appsettings.json` 与 `backend/src/AiInterview.Api/appsettings.Development.json`，然后再叠加环境变量。  
当前开发配置已经统一到和 `.env.run` / `docker-compose.yml` 一致的本地 PostgreSQL：

- 用户名：`postgres`
- 密码：`postgres`
- 数据库：`ai_interview`
- 本地端口：`5433`

Redis 默认使用：

- `localhost:6379,abortConnect=false`

如果本机没有这两个服务，可以直接运行 `npm run dev`，脚本会自动尝试启动 Docker Compose 的 `postgres` 和 `redis` 服务。

如果你已经用 `docker compose --env-file .env.run up --build -d` 起过整套前后端，请先停掉 `frontend / backend / ai-service`，否则本地 `npm run dev` 会和 3000 / 8080 / 8000 端口冲突。

## 默认演示账号

- 普通用户：`zhangsan / Pass1234`
- 管理员：`admin / Admin1234`

这些账号会在后端启动时由种子逻辑自动初始化，前提是 `SEED_ENABLED=true`。

## 常见错误

### `password authentication failed for user "postgres"`

这个错误通常表示你连到的不是当前仓库预期的本地 PostgreSQL，或者本地 `storage/postgres` 数据卷是用旧密码初始化的。

优先检查：

1. `docker compose --env-file .env.run up -d postgres redis` 是否已经启动
2. 你访问的是否是 `localhost:5433`
3. `POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB` 是否仍然是 `.env.run` 里的默认值
4. 如果你以前改过 PostgreSQL 密码，旧的数据卷不会自动刷新，需要重建本地 Postgres 数据后再试
5. 如果同时运行了整套 Docker Compose 前后端，先停掉它们再启动本地联动脚本

## 构建与验证

### 前端

```powershell
cd frontend
npm run build
```

### 后端

```powershell
cd backend
dotnet build .\src\AiInterview.Api\AiInterview.Api.csproj
```

### AI 服务

```powershell
cd ai-service
uv run pytest
```

## 启动脚本验收结果

以下结果已经完成，可直接用于 PR 描述或交付说明：

- `start.ps1` 连续两轮启动验证通过，退出码为 `0`
- `stop.ps1` 连续两轮停止验证通过，退出码为 `0`
- `frontend` 默认运行在 `3000`，`backend` 默认运行在 `8080`
- `start.ps1` 在启动前会自动停止旧服务并清理残留项目端口进程
- `stop.ps1` 可正确按 PID 树停止项目进程，无残留僵尸进程
- 第二轮 `start -> stop` 循环可在干净状态下再次成功启动
- 清理逻辑已显式保护 Docker / 系统关键进程，不会误杀
- 子进程已改为独立隐藏窗口，控制台输出不会互相混叠

**建议复验命令：**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start.ps1 -Full
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\stop.ps1
```

## 文档优先级

实现与验收以仓库当前代码和运行结果为准；如果文档与实际行为冲突，优先参考最近且已经验证过的实现。
