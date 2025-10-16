from __future__ import annotations

import io
import os
from typing import Any

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
# Reports existentes
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

        # nombre del equipo (opcional, no falla si no se puede)
        team_name = None
        try:
            team_map = await clients.fetch_teams_map(x_api_authorization)
            team_name = team_map.get(str(team_id))
        except httpx.HTTPStatusError:
            team_name = None

    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)

    pdf = pdf_utils.build_pdf_players_by_team(team_id, players, team_name)
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


# ============================
# NUEVOS REPORTES
# ============================
@app.get("/reports/players/all.pdf", dependencies=[Depends(require_admin)])
async def report_all_players(
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization")
):
    """
    Lista todos los jugadores registrados con el nombre del equipo.
    """
    try:
        players = await clients.fetch_all_players(x_api_authorization)
        team_map = await clients.fetch_teams_map(x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)

    pdf = pdf_utils.build_pdf_all_players(players, team_map)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={"Content-Disposition": 'attachment; filename="players_all.pdf"'},
    )


def _agg_stats_from_matches(
    matches: list[dict[str, Any]], team_map: dict[str, str]
) -> dict[str, dict[str, Any]]:
    """
    Calcula PJ/PG/PP/PF/PC por equipo a partir de la lista de partidos.
    """
    stats: dict[str, dict[str, Any]] = {}

    def _ensure(team_id: str) -> dict[str, Any]:
        if team_id not in stats:
            stats[team_id] = {
                "teamId": team_id,
                "team": team_map.get(team_id, team_id),
                "played": 0,
                "wins": 0,
                "losses": 0,
                "pf": 0,   # puntos a favor
                "pa": 0,   # puntos en contra
            }
        return stats[team_id]

    for m in matches:
        home_id = str(
            m.get("homeTeamId")
            or m.get("HomeTeamId")
            or (m.get("homeTeam") or {}).get("id")
            or ""
        )
        away_id = str(
            m.get("awayTeamId")
            or m.get("AwayTeamId")
            or (m.get("awayTeam") or {}).get("id")
            or ""
        )

        try:
            hs = int(m.get("homeScore") or m.get("HomeScore") or 0)
            as_ = int(m.get("awayScore") or m.get("AwayScore") or 0)
        except Exception:
            # si no hay marcador, lo saltamos
            continue

        if not home_id or not away_id:
            continue

        h = _ensure(home_id)
        a = _ensure(away_id)

        h["played"] += 1
        a["played"] += 1
        h["pf"] += hs
        h["pa"] += as_
        a["pf"] += as_
        a["pa"] += hs

        if hs > as_:
            h["wins"] += 1
            a["losses"] += 1
        elif as_ > hs:
            a["wins"] += 1
            h["losses"] += 1
        # empates no cambian wins/losses

    return stats


@app.get("/reports/stats/summary.pdf", dependencies=[Depends(require_admin)])
async def report_stats_summary(
    x_api_authorization: str | None = Header(default=None, alias="X-Api-Authorization")
):
    """
    Secciones:
      - Top por victorias
      - Top por puntos a favor
      - Menos puntos a favor
      - Menos derrotas
    """
    try:
        standings = await clients.fetch_standings(x_api_authorization)  # wins por equipo
        team_map = await clients.fetch_teams_map(x_api_authorization)
        matches = await clients.fetch_all_matches(x_api_authorization)
    except httpx.HTTPStatusError as e:
        raise _upstream_502(e)

    agg = _agg_stats_from_matches(matches, team_map)

    # ------- Top por victorias (usar standings si viene, si no, usar agg) -------
    wins_list: list[tuple[str, int]] = []
    if isinstance(standings, list) and standings:
        for r in standings:
            tid = str(r.get("id") or r.get("Id"))
            wins = int(r.get("wins") or 0)
            name = team_map.get(tid, r.get("name") or r.get("Name") or tid)
            wins_list.append((tid, wins, name))  # (id, wins, nombre)
    else:
        for tid, s in agg.items():
            wins_list.append((tid, int(s.get("wins") or 0), s.get("team") or tid))

    wins_list.sort(key=lambda x: (-x[1], x[2]))
    top_wins = [["#", "Equipo", "ID", "PJ", "PG", "PP", "PF", "PC"]]
    for idx, (tid, _w, name) in enumerate(wins_list[:10], 1):
        s = agg.get(tid, {"played": 0, "wins": _w, "losses": 0, "pf": 0, "pa": 0})
        top_wins.append(
            [idx, name, tid, s["played"], s["wins"], s["losses"], s["pf"], s["pa"]]
        )

    # ------- Top por puntos a favor -------
    pf_sorted = sorted(
        agg.values(), key=lambda s: (-int(s["pf"]), s["team"])
    )
    top_pf = [["#", "Equipo", "ID", "PJ", "PG", "PP", "PF", "PC"]]
    for idx, s in enumerate(pf_sorted[:10], 1):
        top_pf.append(
            [
                idx,
                s["team"],
                s["teamId"],
                s["played"],
                s["wins"],
                s["losses"],
                s["pf"],
                s["pa"],
            ]
        )

    # ------- Menos puntos a favor -------
    min_pf_sorted = sorted(
        agg.values(), key=lambda s: (int(s["pf"]), s["team"])
    )
    min_pf = [["#", "Equipo", "ID", "PJ", "PG", "PP", "PF", "PC"]]
    for idx, s in enumerate(min_pf_sorted[:10], 1):
        min_pf.append(
            [
                idx,
                s["team"],
                s["teamId"],
                s["played"],
                s["wins"],
                s["losses"],
                s["pf"],
                s["pa"],
            ]
        )

    # ------- Menos derrotas -------
    min_losses_sorted = sorted(
        agg.values(), key=lambda s: (int(s["losses"]), -int(s["wins"]), s["team"])
    )
    min_losses = [["#", "Equipo", "ID", "PJ", "PG", "PP", "PF", "PC"]]
    for idx, s in enumerate(min_losses_sorted[:10], 1):
        min_losses.append(
            [
                idx,
                s["team"],
                s["teamId"],
                s["played"],
                s["wins"],
                s["losses"],
                s["pf"],
                s["pa"],
            ]
        )

    sections = [
        ("Top por Victorias", top_wins),
        ("Top por Puntos a Favor", top_pf),
        ("Menos Puntos a Favor", min_pf),
        ("Menos Derrotas", min_losses),
    ]

    pdf = pdf_utils.build_pdf_stats_report(sections)
    return StreamingResponse(
        io.BytesIO(pdf),
        media_type="application/pdf",
        headers={"Content-Disposition": 'attachment; filename="stats_summary.pdf"'},
    )


# Manejo genérico de excepciones de httpx
@app.exception_handler(httpx.RequestError)
async def httpx_request_error_handler(_req: Request, exc: httpx.RequestError):
    return JSONResponse(status_code=502, content={"detail": {"message": str(exc)}})
