import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Team } from '../models/team';

@Injectable({
  providedIn: 'root'
})
export class TeamService {
  //private apiUrl = 'http://localhost:5003/api/teams';
  private apiUrl = '/api/teams';

  constructor(private http: HttpClient) {}

  getTeams(
    page: number = 1,
    pageSize: number = 10,
    search: string = ''
  ): Observable<{ items: Team[]; totalCount: number }> {
    let params = `?page=${page}&pageSize=${pageSize}`;
    if (search) {
      params += `&q=${encodeURIComponent(search)}`;
    }
    return this.http.get<{ items: Team[]; totalCount: number }>(
      `${this.apiUrl}${params}`
    );
  }


  getAll(): Observable<Team[]> {

    return this.getTeams(1, 1000, '').pipe(map(r => r.items));
  }

  getById(id: number): Observable<Team> {
    return this.http.get<Team>(`${this.apiUrl}/${id}`);
  }


  create(team: Team): Observable<Team> {
    return this.http.post<Team>(this.apiUrl, team);
  }

  update(id: number, team: Team): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, team);
  }


  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
