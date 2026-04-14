# AI 服务

这是 AI 模拟面试系统的内部 FastAPI 服务。

- 提供首题生成、追问、评分、报告、推荐和 RAG 检索占位能力
- 当前采用 mock provider，方便先打通主链路
- 不直接连接数据库，所有结构化结果由 ASP.NET Core 后端统一落库
