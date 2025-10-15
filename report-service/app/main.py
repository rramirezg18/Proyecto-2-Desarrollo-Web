# app/main.py
from __future__ import annotations

import io
import os

import httpx
from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.responses import JSONResponse, StreamingResponse
from jose import JWTError, jwt

from . import clients, pdf_utils

app = FastAPI()

AUTH_SECRET = os.getenv("AUTH_SECRET", "change_me")
ALGO = "HS256"


# ---------- Seguridad: requiere RS_TOKEN con role=admin ----------
async def require_admin(authorization: str | None = Header(default=None)) -> None:
    """
    Valida el bearer enviado por Nginx al report-service.
    Debe ser un JWT HS256 con claim role/admin.
    """
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing RS bearer")
    token = authorization.split(" ", 1)[1]
    try:
        payload = jwt.decode(token, AUTH_SECRET, algorithms=[ALGO])
    except JWTError as e:
        raise HTTPException(status_code=401, detail=f"Invalid RS bearer: {e}")
    role = (payload.get("role") or payload.get("roles") or "").lower()
    if "admin" not in str(role):
        raise HTTPException(status_code=403, detail="RS bearer not admin")


# ---------- Health ----------
@app.get("/health")
async def health():
    return {
        "status": "ok",
        "service": "report-service",
        "port": os.getenv("SERVICE_PORT", "8080"),
    }


# ---------- Helpers ----------
def _upstream_502(e: httpx.HTTPStatusError) -> HTTPException:
    req = e.request
    resp = e.response
    detail = {
        "upstream_url": str(req.url),
        "status_code": resp.status_code,
        "body": (resp.text or "")[:200],
    }
    return HTTPException(status_code=502, detail=detail)


# ============================
# Reports
# ============================


@app.get("/reports/teams.pdf", dependencies=[Depends(require_admin)])
async def report_teams(
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization")
):
    try:
        teams = await clients.fetch_teams(x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)
    pdf = pdf_utils.build_pdf_teams(teams)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={"Content-Disposition": 'attachment; filename="equipos.pdf"'},
    )


@app.get("/reports/teams/{team_id}/players.pdf", dependencies=[Depends(require_admin)])
async def report_players(
    team_id: str,
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization"),
):
    try:
        players = await clients.fetch_players_by_team(team_id, x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)
    pdf = pdf_utils.build_pdf_players_by_team(team_id, players)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={
            "Content-Disposition": f'attachment; filename="players_{team_id}.pdf"'
        },
    )


@app.get("/reports/matches/history.pdf", dependencies=[Depends(require_admin)])
async def report_history(
    from_: str | None = None,
    to: str | None = None,
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization"),
):
    try:
        items = await clients.fetch_matches_history(from_, to, x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)
    pdf = pdf_utils.build_pdf_matches_history(items, from_, to)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={"Content-Disposition": 'attachment; filename="history.pdf"'},
    )


@app.get(
    "/reports/matches/{match_id}/roster.pdf", dependencies=[Depends(require_admin)]
)
async def report_match_roster(
    match_id: str,
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization"),
):
    try:
        roster = await clients.fetch_match_roster(match_id, x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)
    pdf = pdf_utils.build_pdf_match_roster(match_id, roster)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={
            "Content-Disposition": f'attachment; filename="roster_{match_id}.pdf"'
        },
    )


# ---------- NUEVO: standings ----------
@app.get("/reports/standings.pdf", dependencies=[Depends(require_admin)])
async def report_standings(
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization")
):
    try:
        rows = await clients.fetch_standings(x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)
    pdf = pdf_utils.build_pdf_standings(rows)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={"Content-Disposition": 'attachment; filename="standings.pdf"'},
    )


# Manejo genérico de excepciones de httpx (opcional)
@app.exception_handler(httpx.RequestError)
async def httpx_request_error_handler(_req: Request, exc: httpx.RequestError):
    return JSONResponse(status_code=502, content={"detail": {"message": str(exc)}})
