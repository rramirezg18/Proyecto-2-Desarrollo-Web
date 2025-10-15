// src/app/core/services/reports.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private base = '/api/reports';

  /** Equipos */
  downloadTeams(): Observable<Blob> {
    return this.http.get(`${this.base}/teams.pdf`, { responseType: 'blob' });
  }

  /** Jugadores por equipo */
  downloadPlayersByTeam(teamId: number): Observable<Blob> {
    return this.http.get(`${this.base}/teams/${teamId}/players.pdf`, {
      responseType: 'blob',
    });
  }

  /** Historial de partidos (opcionalmente con ?from=&to=) */
  downloadMatchesHistory(params?: { from?: string; to?: string }): Observable<Blob> {
    let hp = new HttpParams();
    if (params?.from) hp = hp.set('from', params.from);
    if (params?.to) hp = hp.set('to', params.to);

    return this.http.get(`${this.base}/matches/history.pdf`, {
      responseType: 'blob',
      params: hp,
    });
  }

  /** Roster por partido */
  downloadMatchRoster(matchId: number): Observable<Blob> {
    return this.http.get(`${this.base}/matches/${matchId}/roster.pdf`, {
      responseType: 'blob',
    });
  }

  /** NUEVO: Tabla de posiciones */
  downloadStandings(): Observable<Blob> {
    return this.http.get(`${this.base}/standings.pdf`, { responseType: 'blob' });
  }
}
