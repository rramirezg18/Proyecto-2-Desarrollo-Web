# report-service/app/clients.py
from __future__ import annotations

import os
from datetime import datetime
from typing import Any

import httpx

# ============================
# Config
# ============================
BASE_API = os.getenv("BASE_API", "http://api:8080")
UPSTREAM_TOKEN = os.getenv("UPSTREAM_TOKEN", "").strip()  # opcional/fallback


# ============================
# Helpers
# ============================
def _bearer_header(api_bearer: str | None) -> dict[str, str]:
    """
    Construye headers para llamar a la API C#. Si llega un string "Bearer ...",
    se usa tal cual. Si llega sólo el token, se le antepone "Bearer ".
    Si no llega, usa UPSTREAM_TOKEN (si está definido).
    """
    if api_bearer:
        return {"Authorization": api_bearer}
    if UPSTREAM_TOKEN:
        tok = UPSTREAM_TOKEN
        if not tok.lower().startswith("bearer "):
            tok = f"Bearer {tok}"
        return {"Authorization": tok}
    return {}


def _as_list_items(data: Any) -> list[dict[str, Any]]:
    """
    La API puede responder {items:[...], totalCount} o directamente una lista.
    Normaliza a lista de dicts.
    """
    if isinstance(data, dict):
        items = data.get("items", data.get("data"))
        if isinstance(items, list):
            return items
        return [data]
    if isinstance(data, list):
        return data
    return []


def _parse_iso(d: str | None) -> datetime | None:
    if not d:
        return None
    try:
        return datetime.fromisoformat(d.replace("Z", "+00:00"))
    except Exception:
        return None


# ============================
# TEAMS
# ============================
async def fetch_teams(api_bearer: str | None) -> list[dict[str, Any]]:
    url = f"{BASE_API}/api/teams"
    async with httpx.AsyncClient(timeout=30) as cx:
        r = await cx.get(url, headers=_bearer_header(api_bearer))
        r.raise_for_status()
        return _as_list_items(r.json())


async def fetch_teams_map(api_bearer: str | None) -> dict[str, str]:
    """
    Devuelve { "1": "Tigres", "2": "Leones", ... } a partir de /api/teams.
    """
    teams = await fetch_teams(api_bearer)
    mapping: dict[str, str] = {}
    for t in teams:
        tid = str(t.get("id") or t.get("Id") or "")
        name = t.get("name") or t.get("Name") or tid
        if tid:
            mapping[tid] = str(name)
    return mapping


# ============================
# STANDINGS
# ============================
async def fetch_standings(api_bearer: str | None) -> list[dict[str, Any]]:
    """
    GET /api/standings  ->  [{id, name, color, wins}, ...]
    """
    url = f"{BASE_API}/api/standings"
    async with httpx.AsyncClient(timeout=30) as cx:
        r = await cx.get(url, headers=_bearer_header(api_bearer))
        r.raise_for_status()
        return _as_list_items(r.json())


# ============================
# PLAYERS
# ============================
async def fetch_players_by_team(
    team_id: str, api_bearer: str | None
) -> list[dict[str, Any]]:
    url = f"{BASE_API}/api/players"
    params = {"teamId": team_id}
    async with httpx.AsyncClient(timeout=30) as cx:
        r = await cx.get(url, params=params, headers=_bearer_header(api_bearer))
        r.raise_for_status()
        return _as_list_items(r.json())


async def fetch_all_players(api_bearer: str | None) -> list[dict[str, Any]]:
    """
    Intenta GET /api/players (todos). Si la API no lo soporta, hace fallback por equipo.
    """
    url = f"{BASE_API}/api/players"
    async with httpx.AsyncClient(timeout=60) as cx:
        r = await cx.get(url, headers=_bearer_header(api_bearer))
        if r.status_code == 200:
            return _as_list_items(r.json())

    # Fallback: iterar equipos y juntar sus jugadores
    teams = await fetch_teams(api_bearer)
    all_players: list[dict[str, Any]] = []
    async with httpx.AsyncClient(timeout=30) as cx:
        for t in teams:
            tid = t.get("id") or t.get("Id")
            if tid is None:
                continue
            rr = await cx.get(
                f"{BASE_API}/api/players",
                params={"teamId": tid},
                headers=_bearer_header(api_bearer),
            )
            if rr.status_code == 200:
                all_players.extend(_as_list_items(rr.json()))
    return all_players


# ============================
# MATCHES (history / stats)
# ============================
async def fetch_matches_history(
    from_date: str | None,
    to_date: str | None,
    api_bearer: str | None,
) -> list[dict[str, Any]]:
    url = f"{BASE_API}/api/matches"

    params: dict[str, Any] = {}
    if from_date:
        params["from"] = from_date
    if to_date:
        params["to"] = to_date

    async with httpx.AsyncClient(timeout=30) as cx:
        r = await cx.get(url, params=params, headers=_bearer_header(api_bearer))
        r.raise_for_status()
        data = _as_list_items(r.json())

    # Filtrado client-side si la API no soporta ?from=&to=
    if from_date or to_date:
        f_dt = _parse_iso(from_date)
        t_dt = _parse_iso(to_date)
        filtered = []
        for m in data:
            raw = (
                m.get("dateMatchUtc")
                or m.get("dateMatch")
                or m.get("DateMatch")
                or m.get("date")
            )
            d = _parse_iso(str(raw))
            if f_dt and (not d or d < f_dt):
                continue
            if t_dt and (not d or d > t_dt):
                continue
            filtered.append(m)
        data = filtered

    return data


async def fetch_all_matches(api_bearer: str | None) -> list[dict[str, Any]]:
    return await fetch_matches_history(None, None, api_bearer)


# ============================
# MATCH ROSTER
# ============================
async def fetch_match_roster(match_id: str, api_bearer: str | None) -> dict[str, Any]:
    """
    Devuelve:
      {
        "match": {...},
        "homePlayers": [...],
        "awayPlayers": [...]
      }
    """
    headers = _bearer_header(api_bearer)
    async with httpx.AsyncClient(timeout=30) as cx:
        # Detalle del partido
        m_url = f"{BASE_API}/api/matches/{match_id}"
        mr = await cx.get(m_url, headers=headers)
        mr.raise_for_status()
        match = mr.json()

        home_id = (
            match.get("homeTeamId")
            or match.get("HomeTeamId")
            or (match.get("homeTeam") or {}).get("id")
        )
        away_id = (
            match.get("awayTeamId")
            or match.get("AwayTeamId")
            or (match.get("awayTeam") or {}).get("id")
        )

        # Planteles
        players_url = f"{BASE_API}/api/players"

        phr = await cx.get(players_url, params={"teamId": home_id}, headers=headers)
        phr.raise_for_status()
        home_players = _as_list_items(phr.json())

        par = await cx.get(players_url, params={"teamId": away_id}, headers=headers)
        par.raise_for_status()
        away_players = _as_list_items(par.json())

    return {
        "match": match,
        "homePlayers": home_players,
        "awayPlayers": away_players,
    }
