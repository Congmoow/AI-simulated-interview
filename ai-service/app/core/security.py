from fastapi import Header, HTTPException, status

from app.core.settings import get_settings


async def verify_internal_request(authorization: str | None = Header(default=None)) -> None:
    settings = get_settings()
    if settings.api_key:
        expected_value = f"Bearer {settings.api_key}"
        if authorization == expected_value:
            return
    elif settings.app_env.lower() == "development" and settings.allow_insecure_dev_auth_bypass:
        return

    raise HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="未授权的内部调用",
    )
