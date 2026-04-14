from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.router import api_router
from app.core.settings import get_settings


@asynccontextmanager
async def lifespan(_: FastAPI):
    yield


settings = get_settings()
app = FastAPI(
    title="AI 模拟面试 AI 服务",
    version="0.1.0",
    lifespan=lifespan,
)
app.include_router(api_router)


@app.get("/health")
async def health() -> dict[str, str]:
    return {
        "status": "healthy",
        "service": settings.service_name,
        "provider": settings.model_provider,
    }
