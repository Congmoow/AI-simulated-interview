# AI 模拟面试与能力提升软件

当前仓库已经收敛到可启动、可联调、可演示的 MVP 形态。默认演示基线使用 Docker Compose 和 `mock` AI provider，重点保证以下链路稳定可跑通：

`登录 -> 选岗位 -> 创建面试 -> 两轮问答 -> 结束面试 -> 查看报告 -> 查看历史 -> 查看趋势`

## 仓库结构

```text
.
├─ Docs/                  # 需求、架构与数据库文档
├─ frontend/              # Next.js App Router + TypeScript + Tailwind
├─ backend/               # ASP.NET Core Web API + SignalR + EF Core
├─ ai-service/            # FastAPI + Celery
├─ storage/               # 本地卷目录
├─ docker-compose.yml
└─ .env.example
```

## 当前 MVP 范围

- 用户链路：登录、岗位选择、创建面试、文本回答、结束面试、查看报告、查看历史与成长趋势
- 管理链路：管理员登录、题目录入、知识库文档上传、文档列表查看
- 系统能力：JWT 认证、统一响应结构、PostgreSQL 持久化、Redis、SignalR、文档处理回调、5E 超时 `processing` 自动收敛

## 快速启动

### 1. 准备环境变量

推荐直接复制为 `.env.run`：

```powershell
Copy-Item .env.example .env.run
```

如需调整端口或密钥，请编辑根目录 `.env.run`。

### 2. 启动全部服务

```powershell
docker compose --env-file .env.run up --build -d
```

查看运行状态：

```powershell
docker compose --env-file .env.run ps
```

### 3. 默认端口

- 前端：http://localhost:3001
- 后端：http://localhost:8080
- 后端 Swagger：http://localhost:8080/swagger
- AI 服务健康检查：http://localhost:8000/health
- PostgreSQL：`localhost:5433`
- Redis：`localhost:6379`

### 4. 健康检查

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:8080/health
Invoke-WebRequest -UseBasicParsing http://localhost:8000/health
```

## 默认演示账号

- 普通用户：`zhangsan / Pass1234`
- 管理员：`admin / Admin1234`

这些账号由后端启动时的种子逻辑自动初始化，前提是 `SEED_ENABLED=true`。

## 主演示步骤

### 用户主链路

1. 打开 `http://localhost:3001/login`
2. 使用 `zhangsan / Pass1234` 登录
3. 进入仪表盘后点击“开始模拟面试”
4. 选择岗位并创建面试
5. 完成两轮文本回答
6. 点击“结束并生成报告”
7. 在报告页查看综合结论、维度分数、训练计划、推荐资源
8. 打开 `/history` 查看历史记录与成长趋势

### 管理端演示边界

1. 使用 `admin / Admin1234` 登录
2. 打开 `/admin`
3. 可演示题目录入、文档上传、知识库文档列表
4. 文档上传后可看到 `processing / ready / failed` 状态
5. 当前版本不继续深挖完整 RAG 检索质量，管理端只作为 MVP 辅助演示项

## 构建与验收命令

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

### 一次性日志命令

```powershell
docker logs --tail 100 ai-interview-frontend 2>&1
docker logs --tail 100 ai-interview-backend 2>&1
docker logs --tail 100 ai-interview-ai-service 2>&1
docker logs --tail 100 ai-interview-celery-worker 2>&1
```

## AI provider 说明

- 当前主验收基线固定为 `AI_SERVICE_MODEL_PROVIDER=mock`
- `mock` 已覆盖题目生成、追问、评分、报告、推荐、训练计划和文档处理占位结果
- 当前代码库里只有 `mock` provider 的实际实现
- 即使环境变量改成非 `mock`，当前服务仍不会接入真实模型
- 因此“真实模型切换”目前属于未实现项，不作为 MVP 演示阻塞条件

## 已知未验证或非阻塞项

- 真实模型接入未实现
- Celery 目前只承担知识库文档处理，报告生成仍由 backend 同步编排
- RAG 检索结果仍是 `mock` 占位内容
- 前端主链路以文本回答为准，语音输入未纳入本轮演示验收

## 本地开发

### 前端

```powershell
cd frontend
npm install
npm run dev
```

### 后端

```powershell
cd backend
dotnet run --project .\src\AiInterview.Api\AiInterview.Api.csproj
```

### AI 服务

```powershell
cd ai-service
uv sync --extra dev
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

### Celery Worker

```powershell
cd ai-service
uv run celery --app app.workers.celery_app:celery_app worker --loglevel=info
```

## 文档优先级

实现与验收以仓库当前代码和运行结果为准；如文档存在冲突，优先参考离实现最近且已经被运行验证过的内容。
