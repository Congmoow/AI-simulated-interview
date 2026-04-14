from fastapi import Header, HTTPException, status

from app.core.settings import get_settings


async def verify_internal_request(authorization: str | None = Header(default=None)) -> None:
    settings = get_settings()
    if not settings.api_key:
        return

    expected_value = f"Bearer {settings.api_key}"
    if authorization != expected_value:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="未授权的内部调用",
        )
