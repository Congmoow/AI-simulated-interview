from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    service_name: str = "ai-interview-ai-service"
    app_env: str = "development"
    api_key: str = ""
    allow_insecure_dev_auth_bypass: bool = False
    redis_url: str = "redis://localhost:6379/0"
    model_provider: str = "mock"
    knowledge_root: str = "/app/storage/uploads/knowledge"
    backend_url: str = "http://localhost:8080"

    model_config = SettingsConfigDict(
        env_file=".env",
        env_prefix="AI_SERVICE_",
        extra="ignore",
    )


@lru_cache
def get_settings() -> Settings:
    return Settings()
