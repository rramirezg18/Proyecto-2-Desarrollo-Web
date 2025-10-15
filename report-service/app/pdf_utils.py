# app/pdf_utils.py
from collections.abc import Iterable
from io import BytesIO
from typing import Any

from reportlab.lib import colors, pagesizes
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


def _doc(buf: BytesIO) -> SimpleDocTemplate:
    return SimpleDocTemplate(
        buf,
        pagesize=pagesizes.A4,
        leftMargin=18 * mm,
        rightMargin=18 * mm,
        topMargin=18 * mm,
        bottomMargin=18 * mm,
        title="Reportes",
        author="report-service",
    )


def _styles():
    s = getSampleStyleSheet()
    title = ParagraphStyle(
        "TitleBig",
        parent=s["Title"],
        fontSize=18,
        leading=22,
        alignment=TA_CENTER,
        spaceAfter=10,
    )
    h2 = ParagraphStyle(
        "H2",
        parent=s["Heading2"],
        spaceBefore=6,
        spaceAfter=6,
    )
    normal = ParagraphStyle(
        "Normal",
        parent=s["Normal"],
        alignment=TA_LEFT,
        fontSize=10,
        leading=13,
    )
    return title, h2, normal


def _table(data: list[list[Any]], col_widths: list[float] | None = None) -> Table:
    t = Table(data, colWidths=col_widths or "100%")
    t.setStyle(
        TableStyle(
            [
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, 0), 10),
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#F2F2F2")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#333333")),
                ("ALIGN", (0, 0), (-1, 0), "CENTER"),
                ("GRID", (0, 0), (-1, -1), 0.25, colors.HexColor("#CCCCCC")),
                ("FONTSIZE", (0, 1), (-1, -1), 9),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                (
                    "ROWBACKGROUNDS",
                    (0, 1),
                    (-1, -1),
                    [colors.white, colors.HexColor("#FAFAFA")],
                ),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]
        )
    )
    return t


def _safe(d: dict[str, Any], *keys: str, default: str = "-") -> Any:
    for k in keys:
        if k in d and d[k] not in (None, ""):
            return d[k]
    return default


# ------------------- BUILDERS -------------------


def build_pdf_teams(teams: Iterable[dict[str, Any]]) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, _, normal = _styles()

    rows: list[list[Any]] = [["ID", "Name", "Color", "Created"]]
    for t in teams:
        if not isinstance(t, dict):
            continue
        rows.append(
            [
                _safe(t, "id", "Id", default=""),
                _safe(t, "name", "Name", default=""),
                _safe(t, "color", "Color", default=""),
                _safe(t, "created", "Created", "createdAt", "CreatedAt", default=""),
            ]
        )

    story: list[Any] = [
        Paragraph("Listado de Equipos", title),
        Spacer(1, 6),
        Paragraph(f"Total: {len(rows)-1}", normal),
        Spacer(1, 8),
        _table(rows),
    ]
    doc.build(story)
    return buf.getvalue()


def build_pdf_players_by_team(team_id: str, players: Iterable[dict[str, Any]]) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, _, normal = _styles()

    rows: list[list[Any]] = [["#", "Player", "Position", "Number"]]
    for i, p in enumerate(players, 1):
        if not isinstance(p, dict):
            continue
        rows.append(
            [
                i,
                _safe(p, "name", "Name", default=""),
                _safe(p, "position", "Position", default=""),
                _safe(p, "number", "Number", "jersey", "Jersey", default=""),
            ]
        )

    story: list[Any] = [
        Paragraph(f"Jugadores del Equipo #{team_id}", title),
        Spacer(1, 6),
        Paragraph(f"Total: {len(rows)-1}", normal),
        Spacer(1, 8),
        _table(rows),
    ]
    doc.build(story)
    return buf.getvalue()


def build_pdf_matches_history(
    matches: Iterable[dict[str, Any]], from_date: str | None, to_date: str | None
) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, _, normal = _styles()

    hdr = "Historial de Partidos"
    if from_date or to_date:
        hdr += f" (rango: {from_date or '...'} → {to_date or '...'})"

    rows: list[list[Any]] = [["Fecha", "Local", "Visitante", "Marcador", "Estado"]]
    for m in matches:
        if not isinstance(m, dict):
            continue
        home = _safe(m, "homeTeamName", "homeTeam", "HomeTeamName", default="")
        away = _safe(m, "awayTeamName", "awayTeam", "AwayTeamName", default="")
        hs = _safe(m, "homeScore", "HomeScore", default="")
        as_ = _safe(m, "awayScore", "AwayScore", default="")
        date = _safe(m, "dateMatchUtc", "date", "DateMatch", "DateMatchUtc", default="")
        status = _safe(m, "status", "Status", default="")
        rows.append([str(date), str(home), str(away), f"{hs} - {as_}", str(status)])

    story: list[Any] = [
        Paragraph(hdr, title),
        Spacer(1, 6),
        Paragraph(f"Total partidos: {len(rows)-1}", normal),
        Spacer(1, 8),
        _table(rows),
    ]
    doc.build(story)
    return buf.getvalue()


def build_pdf_match_roster(match_id: str, roster: dict[str, Any]) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, h2, normal = _styles()

    story: list[Any] = [
        Paragraph(f"Roster del Partido #{match_id}", title),
        Spacer(1, 8),
    ]

    home = roster.get("homeTeam") or roster.get("HomeTeam") or {}
    away = roster.get("awayTeam") or roster.get("AwayTeam") or {}
    home_name = _safe(home, "name", "Name", default="Local")
    away_name = _safe(away, "name", "Name", default="Visitante")

    # Home
    story += [Paragraph(f"{home_name}", h2)]
    home_players = home.get("players") or home.get("Players") or []
    if home_players:
        rows = [["#", "Player", "Position", "Number"]]
        for i, p in enumerate(home_players, 1):
            if not isinstance(p, dict):
                continue
            rows.append(
                [
                    i,
                    _safe(p, "name", "Name", default=""),
                    _safe(p, "position", "Position", default=""),
                    _safe(p, "number", "Number", "jersey", "Jersey", default=""),
                ]
            )
        story += [_table(rows), Spacer(1, 6)]
    else:
        story += [Paragraph("Sin detalles de jugadores.", normal), Spacer(1, 6)]

    # Away
    story += [Paragraph(f"{away_name}", h2)]
    away_players = away.get("players") or away.get("Players") or []
    if away_players:
        rows = [["#", "Player", "Position", "Number"]]
        for i, p in enumerate(away_players, 1):
            if not isinstance(p, dict):
                continue
            rows.append(
                [
                    i,
                    _safe(p, "name", "Name", default=""),
                    _safe(p, "position", "Position", default=""),
                    _safe(p, "number", "Number", "jersey", "Jersey", default=""),
                ]
            )
        story += [_table(rows), Spacer(1, 6)]
    else:
        story += [Paragraph("Sin detalles de jugadores.", normal), Spacer(1, 6)]

    doc.build(story)
    return buf.getvalue()


def build_pdf_player_stats(player_id: str, stats: dict[str, Any]) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, _, _ = _styles()

    rows = [["Campo", "Valor"]]
    if isinstance(stats, dict):
        for k, v in stats.items():
            rows.append([str(k), str(v)])
    else:
        rows.append(["-", "-"])

    story: list[Any] = [
        Paragraph(f"Estadísticas del Jugador #{player_id}", title),
        Spacer(1, 8),
        _table(rows),
    ]
    doc.build(story)
    return buf.getvalue()


def build_pdf_standings(rows: list[dict[str, Any]]) -> bytes:
    buf = BytesIO()
    doc = _doc(buf)
    title, _, normal = _styles()

    data: list[list[Any]] = [["#", "Equipo", "Victorias"]]
    for i, r in enumerate(rows, 1):
        data.append([i, str(r.get("name", "")), int(r.get("wins", 0) or 0)])

    story: list[Any] = [
        Paragraph("Tabla de Posiciones", title),
        Spacer(1, 6),
        Paragraph(f"Total: {len(rows)}", normal),
        Spacer(1, 8),
        _table(data, col_widths=[28, 340, 90]),
    ]
    doc.build(story)
    return buf.getvalue()
