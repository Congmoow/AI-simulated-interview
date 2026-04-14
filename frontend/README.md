# 前端服务

前端使用 Next.js App Router、TypeScript 与 Tailwind CSS 实现，只负责页面展示、交互编排和状态呈现。

## 约束

- 不承载核心业务逻辑
- 通过 HTTP 调用 ASP.NET Core 后端
- 通过 SignalR 接收实时面试消息
- 所有视觉规范必须基于 `src/styles/tokens.css` 与 Tailwind Token 扩展

## 本地启动

```powershell
npm install
npm run dev
```

默认访问地址为 [http://localhost:3000](http://localhost:3000)。
