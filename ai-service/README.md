# AI 服务

`ai-service` 是本项目内部使用的 FastAPI 服务，负责承接面试主流程中的 AI 能力调用，并为文档处理提供同步与异步入口。

它当前不直接连接业务数据库，但并不是一个完全独立的服务：运行时模型配置依赖后端提供，部分处理结果也需要回调后端接口。

## 服务职责

当前代码中，这个服务主要负责以下职责：

- 面试首题生成：`POST /interview/start`
- 面试追问与流程推进：`POST /interview/answer`
- 面试评分：`POST /evaluation/score`
- 面试报告生成：`POST /report/generate`
- 文档处理同步入口：`POST /document/process`
- 文档处理异步入口：`POST /document/enqueue`

同时还保留了以下业务入口，但当前 `OpenAICompatibleProvider` 尚未接到真实 AI：

- 资源推荐：`POST /recommend/resources`
- 训练计划：`POST /recommend/training-plan`
- RAG 检索：`POST /rag/search`

## 当前 Provider 行为

当前运行路径默认不是 `mock provider`。

服务会在需要调用模型时执行以下流程：

1. 通过 `app.services.dependencies.get_provider()` 从后端读取 runtime AI settings。
2. 请求地址为：`{AI_SERVICE_BACKEND_URL}/api/v1/internal/ai/runtime-settings`
3. `AI_SERVICE_API_KEY` 是后端与 AI 服务共享的内部鉴权密钥来源；正常运行时会以 `Authorization: Bearer <key>` 方式请求后端。
4. 获取到配置后，创建 `OpenAICompatibleProvider`。
5. runtime settings 会在进程内缓存 60 秒。

如果 runtime settings 为空或拉取失败，服务不会自动回退到 mock provider，而是抛出异常。

有一个例外：

- `POST /interview/start` 在 provider 不可用时，会在路由层捕获异常，并让面试首题逻辑降级到模板首题流程。

这意味着：

- 当前主链路依赖后端返回有效的运行时模型配置。
- mock provider 不是默认运行模式，主要只出现在测试或显式替换依赖时。

## 接口入口

应用入口是：

```text
app.main:app
```

默认暴露端口：

```text
8000
```

已挂载的路由前缀如下：

- `/interview`
- `/evaluation`
- `/report`
- `/recommend`
- `/rag`
- `/document`

健康检查接口：

```text
GET /health
```

`/health` 不要求内部鉴权，返回服务名、状态和当前配置中的 `model_provider`。

## 鉴权方式

除 `/health` 之外，业务路由都通过 `verify_internal_request` 进行内部鉴权。

鉴权规则如下：

- 如果配置了 `AI_SERVICE_API_KEY`，调用方必须携带：

```text
Authorization: Bearer <AI_SERVICE_API_KEY>
```

否则会返回 `401 Unauthorized`。
- 如果未配置 `AI_SERVICE_API_KEY`，默认同样拒绝未鉴权请求；只有在 `AI_SERVICE_APP_ENV=development` 且显式设置 `AI_SERVICE_ALLOW_INSECURE_DEV_AUTH_BYPASS=true` 时，才允许开发绕过。

## 依赖管理

这个服务使用 `uv` 管理 Python 依赖，不是 `requirements.txt` 工作流。

依赖定义文件：

- `ai-service/pyproject.toml`
- `ai-service/uv.lock`

当前核心运行依赖包括：

- `fastapi`
- `uvicorn[standard]`
- `httpx`
- `pydantic-settings`
- `python-multipart`
- `celery[redis]`

开发测试依赖包括：

- `pytest`
- `pytest-asyncio`

常用安装命令：

```powershell
cd ai-service
uv sync
```

如果只安装运行依赖，可参考 Docker 构建里的做法：

```powershell
cd ai-service
uv sync --frozen --no-dev
```

## 启动命令

### 仅启动 AI 服务

仓库根目录：

```powershell
npm run dev:ai-service
```

它实际执行的是：

```powershell
cd ai-service
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload --reload-dir app
```

### 启动前端、后端与 AI 服务联调

仓库根目录：

```powershell
npm run dev:full
```

### Docker 运行入口

`ai-service/Dockerfile` 当前使用的启动命令是：

```text
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000
```

## 测试命令

仓库根目录可直接运行：

```powershell
npm run test:ai
```

它会执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/check/run-ai-tests.ps1
```

脚本内部实际调用：

```powershell
uv run --directory ai-service python -m pytest
```

如果只想在服务目录内直接运行测试：

```powershell
cd ai-service
uv run python -m pytest
```

## Celery / Redis 异步链路

文档处理除了同步接口外，还支持基于 Celery 的异步处理链路。

相关组件如下：

- Broker / Result Backend：`AI_SERVICE_REDIS_URL`
- Celery 应用：`app.workers.celery_app`
- 异步任务名：`knowledge.process_document`
- 入队接口：`POST /document/enqueue`

异步流程如下：

1. 调用 `/document/enqueue`
2. 路由将任务投递到 Celery
3. Worker 执行 `process_knowledge_document_task`
4. 任务内部调用 `DocumentService(get_provider())`
5. 处理完成后，向后端回调文档结果

回调地址格式：

```text
{AI_SERVICE_BACKEND_URL}/api/v1/internal/knowledge/documents/{document_id}/callback
```

回调请求同样会在配置了 `AI_SERVICE_API_KEY` 时带上 Bearer Token。

当前任务实现包含以下行为：

- `acks_late=true`
- `worker_prefetch_multiplier=1`
- 文档处理任务最多重试 3 次
- 回调发送内部也带有最多 3 次重试

## 与后端的依赖关系

这个服务与 ASP.NET Core 后端之间存在明确的运行时依赖，而不只是“结果回传”关系。

当前依赖点主要有两类：

### 1. 运行时配置依赖

AI 服务会从后端拉取如下运行时配置：

- provider
- base URL
- model
- API key
- temperature
- max tokens
- system prompt

如果后端没有返回可用配置，真实 provider 无法创建，相关 AI 能力会失败。

### 2. 处理结果回调依赖

异步文档处理完成后，AI 服务会向后端回调文档分块结果或失败状态。

因此当前部署与联调时，至少需要保证：

- 后端接口可访问
- `AI_SERVICE_BACKEND_URL` 配置正确
- 如启用内部鉴权，后端与 AI 服务使用同一份 `AI_SERVICE_API_KEY`

## 关键环境变量

当前 `Settings` 使用 `AI_SERVICE_` 前缀读取环境变量。

代码中已经使用到的关键配置如下：

- `AI_SERVICE_API_KEY`
- `AI_SERVICE_ALLOW_INSECURE_DEV_AUTH_BYPASS`
- `AI_SERVICE_REDIS_URL`
- `AI_SERVICE_BACKEND_URL`
- `AI_SERVICE_APP_ENV`
- `AI_SERVICE_SERVICE_NAME`
- `AI_SERVICE_KNOWLEDGE_ROOT`
- `AI_SERVICE_MODEL_PROVIDER`

默认值可在 `app/core/settings.py` 查看。需要注意的是，`AI_SERVICE_MODEL_PROVIDER` 目前主要影响健康检查返回值，不代表实际运行时一定走 mock provider。

## 现状说明

为了避免误解，当前能力边界补充如下：

- 首题生成、追问、评分、报告生成已经接入 `OpenAICompatibleProvider`
- 文档处理接口已存在，同步与异步入口都可用
- 资源推荐、训练计划、RAG 检索路由已存在，但 `OpenAICompatibleProvider` 中仍会抛出 `NotImplementedError`
- 服务当前是后端内部服务，不是面向公网直接开放的独立产品接口
