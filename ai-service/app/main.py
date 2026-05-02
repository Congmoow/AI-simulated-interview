import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.api.router import api_router
from app.core.settings import get_settings
from app.providers.openai_compatible_provider import ProviderCallError

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(_: FastAPI):
    yield


settings = get_settings()
app = FastAPI(
    title="AI 模拟面试 AI 服务",
    version="0.1.0",
    lifespan=lifespan,
)


@app.exception_handler(ProviderCallError)
async def provider_call_error_handler(_request: Request, exc: ProviderCallError) -> JSONResponse:
    logger.error("provider_call_error: %s", exc)
    return JSONResponse(
        status_code=502,
        content={"detail": "上游 AI 服务调用失败，请稍后重试。"},
    )


@app.exception_handler(ValueError)
async def value_error_handler(_request: Request, exc: ValueError) -> JSONResponse:
    logger.warning("value_error: %s", exc)
    return JSONResponse(
        status_code=422,
        content={"detail": str(exc) or "请求参数或响应格式异常。"},
    )


@app.exception_handler(Exception)
async def generic_exception_handler(_request: Request, exc: Exception) -> JSONResponse:
    logger.exception("unhandled_exception: %s", exc)
    return JSONResponse(
        status_code=500,
        content={"detail": "服务内部错误，请稍后重试。"},
    )


app.include_router(api_router)


@app.get("/health")
async def health() -> dict[str, str]:
    return {
        "status": "healthy",
        "service": settings.service_name,
        "provider": settings.model_provider,
    }
