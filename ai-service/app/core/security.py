import logging

from fastapi import Header, HTTPException, status

from app.core.settings import get_settings

logger = logging.getLogger(__name__)


async def verify_internal_request(authorization: str | None = Header(default=None)) -> None:
    settings = get_settings()

    if not settings.api_key:
        logger.warning("AI_SERVICE_API_KEY 未配置，拒绝所有内部请求")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="未授权的内部调用",
        )

    expected_value = f"Bearer {settings.api_key}"
    if authorization == expected_value:
        return

    raise HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="未授权的内部调用",
    )
