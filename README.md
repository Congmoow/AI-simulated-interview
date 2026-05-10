# APTAI - AI 模拟面试与能力提升系统

面向计算机相关专业学生的多服务 AI 模拟面试平台，围绕 **练习 → 追问 → 评分 → 报告 → 提升建议** 的核心链路，提供岗位化模拟面试、多维评分报告和个性化学习推荐。

## 技术栈

| 服务 | 技术 | 端口 |
|------|------|------|
| Frontend | Next.js 16 + React 19 + TypeScript + Tailwind CSS 4 | 3000 |
| Backend | ASP.NET Core 8 + EF Core + SignalR | 8080 |
| AI Service | FastAPI + Pydantic + Celery | 8000 |
| Database | PostgreSQL 15 + pgvector | 5433 |
| Cache/Queue | Redis 7 | 6379 |

## 快速开始

### 前置条件

- Node.js 18+
- .NET 8 SDK
- Python 3.12+ / uv
- Docker Desktop

### 1. 配置环境变量

```bash
cp .env.example .env.run
```

编辑 `.env.run`，填写必要的密码和 API Key：

```env
# 数据库
POSTGRES_PASSWORD=your_password

# Redis
REDIS_PASSWORD=your_redis_password

# AI 服务内部认证（后端 ↔ AI 服务共享）
AI_SERVICE_API_KEY=your_api_key

# .NET 配置映射
AiService__ApiKey=your_api_key
AiService__BaseUrl=http://localhost:8000
Seed__UserPassword=your_user_password
Seed__AdminPassword=your_admin_password
```

### 2. 启动基础设施

```bash
docker compose --env-file .env.run up -d postgres redis
```

### 3. 启动所有服务

```bash
npm install
npm run dev:full
```

服务地址：
- 前端：http://localhost:3000
- 后端 API：http://localhost:8080
- AI 服务：http://localhost:8000
- Swagger 文档：http://localhost:8080/swagger

### 4. 配置 LLM

启动后访问管理后台 http://localhost:3000/admin/ai-settings，配置 LLM Provider。

支持所有 OpenAI 兼容接口，推荐：
- **阿里云百炼 Qwen**：`https://dashscope.aliyuncs.com/compatible-mode/v1`
- **DeepSeek**：`https://api.deepseek.com/v1`
- **OpenAI**：`https://api.openai.com/v1`

## 系统架构

```
┌─────────────┐     REST API      ┌──────────────────┐     HTTP      ┌──────────────────┐
│   Frontend   │ ──────────────── │     Backend       │ ──────────── │    AI Service     │
│  Next.js 16  │ ←── SignalR ──── │  ASP.NET Core 8   │ ←────────── │     FastAPI       │
│  :3000       │    WebSocket     │  :8080             │              │  :8000            │
└─────────────┘                   └──────────────────┘              └──────────────────┘
                                       │          │                       │
                                  ┌────▼───┐  ┌───▼───┐            ┌─────▼─────┐
                                  │Postgres│  │ Redis │            │ LLM API   │
                                  │  :5433 │  │ :6379 │            │ (Qwen等)  │
                                  └────────┘  └───────┘            └───────────┘
```

### 核心流程

1. **登录**：JWT 认证，支持注册/登录
2. **选择岗位**：Java 后端 / Web 前端，支持轻松/标准/高压三种模式
3. **面试问答**：AI 动态生成问题和追问，支持流式响应
4. **评分报告**：AI 多维评分 + 结构化报告生成（合并调用，约 16 秒）
5. **能力画像**：历史面试数据分析，雷达图展示能力维度

### 流式响应

问答环节支持流式响应（Streaming），用户提交答案后 1-3 秒内即可看到 AI 回复逐步显示：

```
用户提交答案 → 后端调用 AI 服务 → AI 服务流式调用 LLM
                                     ↓
                              SignalR 推送 content chunks
                                     ↓
                              前端逐步显示 AI 回复 + 光标动画
```

### 性能指标

| 指标 | 耗时 |
|------|------|
| 问答响应（首字节） | 1-3 秒 |
| 问答响应（完整） | 6-10 秒 |
| 报告生成（合并调用） | ~16 秒 |
| 端到端完整面试（5 轮） | ~60 秒 |

### 性能优化

后端已实施多项 API 响应时间优化：

| 优化项 | 说明 |
|--------|------|
| N+1 查询修复 | 成长趋势页面 Score 查询从 O(N) 降为 O(1) 批量查询 |
| 并行数据库查询 | Dashboard 两次 Count 查询改为 `Task.WhenAll` 并行 |
| 查询拆分 | 新增 `GetByIdLightAsync` 轻量查询，减少不必要的 JOIN |
| 题库缓存 | 面试题库 `IMemoryCache` 缓存 5 分钟，管理员变更时主动失效 |
| AI 设置缓存 | AI 配置 30 秒内存缓存，避免重复 DB 查询 |
| 响应压缩 | 启用 gzip/brotli 响应压缩，大 JSON 传输体积减少 60-80% |
| HttpClient 连接池 | AI 服务 HTTP 客户端配置 20 连接池 + gzip 自动解压 |
| L1+L2 双层缓存 | Dashboard 画像数据：MemoryCache (L1, 2 分钟) + Redis (L2, 15 分钟) |

## 项目结构

```
├── frontend/                    # Next.js 前端
│   └── src/
│       ├── app/                 # App Router 页面
│       ├── components/          # 通用组件
│       ├── features/            # 功能模块
│       ├── services/            # API 服务层
│       ├── stores/              # Zustand 状态管理
│       └── types/               # TypeScript 类型
│
├── backend/                     # ASP.NET Core 后端
│   └── src/AiInterview.Api/
│       ├── Controllers/         # API 控制器
│       ├── Services/            # 业务服务层
│       ├── Models/              # 实体模型
│       ├── DTOs/                # 数据传输对象
│       ├── Hubs/                # SignalR Hub
│       └── Repositories/        # 数据访问层
│
├── ai-service/                  # FastAPI AI 服务
│   └── app/
│       ├── api/routes/          # API 路由
│       ├── providers/           # LLM Provider（含 Mock）
│       ├── services/            # 业务服务
│       ├── schemas/             # Pydantic 数据模型
│       └── workers/             # Celery 异步任务
│
├── docker-compose.yml           # 容器编排
├── .env.example                 # 环境变量模板
└── package.json                 # 根目录脚本编排
```

## 常用命令

```bash
# 启动所有服务（前端 + 后端 + AI 服务）
npm run dev:full

# 仅启动前端 + 后端
npm run dev

# 单独启动
npm run dev:frontend
npm run dev:backend
npm run dev:ai-service

# 测试
npm run test
npm run test:backend
npm run test:ai

# 代码检查
npm run lint

# 构建
npm run build
```

## API 接口

### 认证

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/auth/login` | 登录 |
| POST | `/api/v1/auth/register` | 注册 |

### 面试

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/positions` | 获取岗位列表 |
| POST | `/api/v1/interviews` | 创建面试 |
| POST | `/api/v1/interviews/{id}/answers` | 提交回答 |
| POST | `/api/v1/interviews/{id}/finish` | 结束面试 |
| GET | `/api/v1/interviews/{id}` | 获取面试详情 |

### 报告

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/reports/{interviewId}` | 获取报告 |
| GET | `/api/v1/reports/growth` | 成长趋势 |
| GET | `/api/v1/dashboard/insights` | 仪表盘数据 |

### 管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/admin/ai-settings` | AI 配置 |
| PUT | `/api/v1/admin/ai-settings` | 更新 AI 配置 |
| POST | `/api/v1/admin/ai-settings/test` | 测试连接 |

## 默认账号

种子数据自动创建（密码在 `.env.run` 中配置）：
- 普通用户：`zhangsan`
- 管理员：`admin`

## 环境变量说明

| 变量 | 说明 | 示例 |
|------|------|------|
| `POSTGRES_PASSWORD` | PostgreSQL 密码 | `devpostgres` |
| `REDIS_PASSWORD` | Redis 密码 | `devredis` |
| `JWT_SECRET_KEY` | JWT 签名密钥（32+ 字符） | `your-secret-key...` |
| `AI_SERVICE_API_KEY` | 后端↔AI 服务内部认证 | `your-api-key` |
| `AiService__ApiKey` | .NET 映射（同上） | `your-api-key` |
| `AiService__BaseUrl` | AI 服务地址 | `http://localhost:8000` |
| `Seed__UserPassword` | 种子用户密码 | `dev123456` |
| `Seed__AdminPassword` | 种子管理员密码 | `admin123456` |

## 本地数据目录

- `storage/postgres`：PostgreSQL 数据
- `storage/redis`：Redis 持久化数据
- `storage/uploads`：知识库上传文件
- `storage/dp-keys`：ASP.NET Core Data Protection 密钥

## 常见问题

### PostgreSQL 认证失败

检查 `.env.run` 中的 `POSTGRES_PASSWORD` 是否正确，以及是否有旧的 `storage/postgres` 数据卷沿用了历史密码。

### 前端端口不一致

- 本地 `npm run dev`：默认 `3000`
- Docker Compose：默认 `3001`
- `start.ps1`：优先 `3000`，被占用时回退 `3001`

### AI 服务使用 Mock 模式

`.env.run` 中 `AI_SERVICE_MODEL_PROVIDER=mock` 为默认值。接入真实 LLM 需要在管理后台配置 API Key 和模型信息。

### 后端连接 AI 服务失败

确保 `AiService__ApiKey` 和 `AI_SERVICE_API_KEY` 值一致，且 `AiService__BaseUrl` 指向正确的 AI 服务地址。

## 相关文档

- [架构设计](Docs/ARCHITECTURE.md)
- [数据库设计](Docs/DATABASE.md)
- [API 文档](Docs/API.md)
- [UI 设计](Docs/DESIGN.md)
- [决策记录](docs-shared/decisions/)
- [共享文档入口](docs-shared/README.md)
