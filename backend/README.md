# backend 后端说明

`backend/` 是仓库里的 ASP.NET Core 业务主入口，负责认证、面试流程、报告查询、推荐、知识库管理、管理员配置以及实时面试通道。

如果你只想快速启动后端，优先看“本地启动”一节；如果你要改接口或排查后端行为，建议顺着“目录结构”和“主要入口”往下看。

## 技术栈

- ASP.NET Core 8
- Entity Framework Core 8
- PostgreSQL
- Redis
- SignalR
- Serilog
- Swagger（仅 Development 环境启用）

## 当前职责

后端当前仍采用单项目结构：`src/AiInterview.Api` 同时承载 API、依赖注册、业务服务、仓储与基础设施接线。

当前已覆盖的后端能力主要包括：

- 用户注册、登录、JWT 刷新、个人资料维护
- 岗位与题库查询
- 模拟面试创建、答题、结束、历史与详情查询
- 报告查询、成长趋势、训练建议与资源推荐
- 管理端题库、知识库文档、AI 设置管理
- SignalR 实时面试通道
- 对 `ai-service` 的内部回调与运行时配置读取

补充说明：

- 当前仓库已经明确接受“先维持单项目、按注册职责拆薄组合根”的方案，见 [`docs-shared/decisions/0001-后端是否拆分多项目.md`](../docs-shared/decisions/0001-%E5%90%8E%E7%AB%AF%E6%98%AF%E5%90%A6%E6%8B%86%E5%88%86%E5%A4%9A%E9%A1%B9%E7%9B%AE.md)
- 如果后续边界稳定，再考虑拆成 `Api / Application / Domain / Infrastructure` 多项目

## 目录结构

```text
backend/
├── AiInterview.sln
├── Dockerfile
├── src/
│   └── AiInterview.Api/
│       ├── Controllers/           # HTTP 接口入口
│       ├── Data/                  # DbContext 与迁移
│       ├── DependencyInjection/   # 服务注册分组
│       ├── DTOs/                  # 请求/响应模型
│       ├── Hubs/                  # SignalR Hub
│       ├── Infrastructure/        # CORS、密钥保护等基础设施
│       ├── Middleware/            # 异常处理等中间件
│       ├── Models/Entities/       # 实体模型
│       ├── Options/               # 配置项绑定
│       ├── Repositories/          # 数据访问层
│       ├── Services/              # 业务服务
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Program.cs
└── tests/
    └── AiInterview.Api.Tests/     # xUnit 单元测试
```

## 主要入口

### 应用入口

- [`src/AiInterview.Api/Program.cs`](./src/AiInterview.Api/Program.cs)
  - 加载配置与环境变量
  - 注册 Swagger、认证、持久化、外部依赖和业务服务
  - 映射控制器、`/hubs/interview` 和 `/health`
  - 启动时自动执行 EF Core 迁移
  - 当 `Seed:Enabled=true` 时自动写入种子数据

### 依赖注册

- [`src/AiInterview.Api/DependencyInjection/AuthenticationExtensions.cs`](./src/AiInterview.Api/DependencyInjection/AuthenticationExtensions.cs)
- [`src/AiInterview.Api/DependencyInjection/PersistenceExtensions.cs`](./src/AiInterview.Api/DependencyInjection/PersistenceExtensions.cs)
- [`src/AiInterview.Api/DependencyInjection/ExternalDependenciesExtensions.cs`](./src/AiInterview.Api/DependencyInjection/ExternalDependenciesExtensions.cs)
- [`src/AiInterview.Api/DependencyInjection/ApplicationServicesExtensions.cs`](./src/AiInterview.Api/DependencyInjection/ApplicationServicesExtensions.cs)
- [`src/AiInterview.Api/DependencyInjection/SwaggerExtensions.cs`](./src/AiInterview.Api/DependencyInjection/SwaggerExtensions.cs)

### 实时通道

- SignalR Hub：`/hubs/interview`
- Hub 实现：[`src/AiInterview.Api/Hubs/InterviewHub.cs`](./src/AiInterview.Api/Hubs/InterviewHub.cs)
- 当前提供的基础方法：
  - `JoinInterview`
  - `LeaveInterview`
  - `SendHeartbeat`

## 运行依赖

本地直接启动后端前，至少要先确保下面两个基础设施已可用：

- PostgreSQL：默认 `localhost:5433`
- Redis：默认 `localhost:6379`

如果你走仓库推荐流程，直接在仓库根目录执行即可：

```powershell
docker compose --env-file .env.run up -d postgres redis
```

## 本地启动

以下命令默认在仓库根目录执行。

### 方式一：只启动后端

```powershell
dotnet run --project backend/src/AiInterview.Api/AiInterview.Api.csproj
```

### 方式二：从后端目录启动

```powershell
cd backend
dotnet run --project .\src\AiInterview.Api\AiInterview.Api.csproj
```

### 方式三：通过仓库联调脚本启动前后端

```powershell
npm run dev
```

这条命令会先确保 PostgreSQL 和 Redis 可用，再同时启动前端与后端。

## 默认访问地址

- API：`http://localhost:8080`
- Swagger：`http://localhost:8080/swagger`
- 健康检查：`http://localhost:8080/health`
- SignalR：`http://localhost:8080/hubs/interview`

补充说明：

- Swagger 只会在 `ASPNETCORE_ENVIRONMENT=Development` 时启用
- `launchSettings.json` 中提供了 `http` 和 `https` 两套本地启动配置

## 配置说明

后端会按以下顺序读取配置：

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. 环境变量

当前最常用的配置项如下：

| 配置项 | 默认值 | 作用 |
| --- | --- | --- |
| `App__FrontendUrl` | `http://localhost:3000` | 前端来源地址，用于 CORS 校验 |
| `ConnectionStrings__Postgres` | `Host=localhost;Port=5433;Database=ai_interview;Username=postgres;Password=postgres` | PostgreSQL 连接串 |
| `ConnectionStrings__Redis` | `localhost:6379,abortConnect=false` | Redis 连接串 |
| `Jwt__Issuer` | `ai-interview` | JWT 签发者 |
| `Jwt__Audience` | `ai-interview-users` | JWT 受众 |
| `Jwt__SecretKey` | 必填环境变量，长度至少 32 | JWT 签名密钥 |
| `AiService__BaseUrl` | `http://localhost:8000` | `ai-service` 地址 |
| `AiService__ApiKey` | 与 `AI_SERVICE_API_KEY` 保持一致 | 后端内部接口鉴权密钥 |
| `Storage__KnowledgeRoot` | `storage/uploads/knowledge` | 知识库上传与处理根目录 |
| `Seed__Enabled` | `true` | 启动时是否写入种子数据 |

几个容易忽略的点：

- 数据保护密钥默认写入 `storage/dp-keys`
- 健康检查会同时探测数据库和 Redis
- 应用启动时会自动执行迁移，因此数据库结构会跟随当前代码推进
- Docker Compose 场景下，后端会把 `AiService__BaseUrl` 覆盖为容器内的 `http://ai-service:8000`

## 主要接口分组

当前控制器分组如下：

- `api/v1/auth`
  - 注册、登录、刷新令牌、当前用户、个人资料更新
- `api/v1/positions`
  - 岗位列表与岗位详情
- `api/v1/questions`
  - 题库查询
- `api/v1/interviews`
  - 创建面试、提交答案、结束面试、历史、详情
- `api/v1/reports`
  - 单场报告、成长趋势
- `api/v1/recommendations`
  - 资源推荐、训练计划
- `api/v1/dashboard`
  - 个人画像能力概览
- `api/v1/knowledge`
  - 知识库搜索
- `api/v1/admin`
  - 题目管理、知识文档上传、AI 设置读写与连接测试
- `api/v1/internal`
  - `ai-service` 回调与运行时设置读取

内部接口补充：

- [`src/AiInterview.Api/Controllers/InternalController.cs`](./src/AiInterview.Api/Controllers/InternalController.cs)
- 内部接口默认拒绝未鉴权请求，不再因为 `AiService__ApiKey` 为空而自动放开
- 只有在 `Development` 环境且显式设置 `AiService__AllowInsecureDevAuthBypass=true` 时，才允许开发绕过
- 正常运行时，`ai-service` 调用内部接口必须带上 `Authorization: Bearer <ApiKey>`，且该密钥应与 `AI_SERVICE_API_KEY` 使用同一份来源

## 数据与持久化

持久化层入口见 [`src/AiInterview.Api/Data/ApplicationDbContext.cs`](./src/AiInterview.Api/Data/ApplicationDbContext.cs)。

当前已知特点：

- 使用 PostgreSQL + EF Core
- 启用了 `UseSnakeCaseNamingConvention()`
- 启用了 `pgvector`
- 迁移文件位于 `src/AiInterview.Api/Data/Migrations`
- 刷新令牌存储依赖 Redis

## 测试与验证

以下命令默认在仓库根目录执行：

### 运行后端测试

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check\run-backend-tests.ps1
```

或直接使用 npm 包装命令：

```powershell
npm run test:backend
```

### 构建后端

```powershell
dotnet build .\backend\AiInterview.sln --configuration Release --nologo
```

### 仅构建 API 项目

```powershell
dotnet build .\backend\src\AiInterview.Api\AiInterview.Api.csproj
```

测试补充说明：

- 当前测试框架为 `xUnit`
- 主要覆盖服务层、仓储层和基础设施规则
- 部分仓储测试使用 EF Core InMemory 提供程序

## Docker

如果你只关心后端镜像本身，可以在仓库根目录执行：

```powershell
docker build -f .\backend\Dockerfile -t ai-interview-backend .\backend
```

容器内默认监听端口：

- `8080`

仓库推荐的完整容器化启动方式仍然是：

```powershell
docker compose --env-file .env.run up --build -d
```

## 可选种子用户

当 `Seed__Enabled=true` 时，启动后端会自动写入岗位、题库、学习资源等种子数据。

仅当显式提供以下配置时，才会创建种子用户：

- `Seed__UserPassword`
- `Seed__AdminPassword`

若启用该种子，则创建以下用户名：

- 普通用户：`zhangsan`
- 管理员：`admin`

若开发环境未提供这两个密码，后端会记录告警并跳过演示用户初始化。

如果你发现账号不可用，优先确认：

- 当前数据库是否为新的开发库
- 是否真的执行了启动迁移与 Seed
- 是否复用了历史 `storage/postgres` 数据卷

## 常见排查

### Swagger 打不开

优先检查：

- 当前环境是否为 `Development`
- 后端是否真的启动在 `8080`
- 启动日志中是否出现异常中断

### `/health` 显示 `degraded`

`/health` 会同时检查数据库与 Redis。常见原因是：

- PostgreSQL 没启动或连接串不对
- Redis 没启动或连接串不对
- 本地端口被占用，实际连到的不是预期服务

### 前端调用被 CORS 拦截

优先检查：

- `App__FrontendUrl` 是否与当前前端实际地址一致
- 本地前端到底跑在 `3000` 还是 `3001`
- 是否把 Docker 场景和本机场景的地址混用了

### 知识库或内部回调异常

优先检查：

- `AiService__BaseUrl` 是否可达
- `AiService__ApiKey` 是否和 `ai-service` 侧保持一致
- `Storage__KnowledgeRoot` 是否指向可读写目录

## 维护建议

- 变更接口、配置项、启动方式或默认端口时，请同步更新本文件和根目录 [`README.md`](../README.md)
- 如果只是补充共享决策或协作背景，请优先放到 [`docs-shared/README.md`](../docs-shared/README.md)
- 如果 README 描述与代码行为冲突，请以最近验证过的代码行为为准，并及时修正文档
