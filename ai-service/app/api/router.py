from fastapi import APIRouter

from app.api.routes import document, evaluation, interview, rag, recommend, report

api_router = APIRouter()
api_router.include_router(interview.router, prefix="/interview", tags=["interview"])
api_router.include_router(evaluation.router, prefix="/evaluation", tags=["evaluation"])
api_router.include_router(report.router, prefix="/report", tags=["report"])
api_router.include_router(recommend.router, prefix="/recommend", tags=["recommend"])
api_router.include_router(rag.router, prefix="/rag", tags=["rag"])
api_router.include_router(document.router, prefix="/document", tags=["document"])
