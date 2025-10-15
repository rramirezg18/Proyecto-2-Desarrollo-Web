import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Observable } from 'rxjs';
import { LoginResponseDto } from '../models/login-response.dto';
import { decodeJwt } from '../utils/jwt'; // 👈 ruta correcta desde /core/services

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  private apiUrl = '/api/auth';

  private storage(): Storage | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    try { return window.localStorage; } catch { return null; }
  }

  login(username: string, password: string): Observable<LoginResponseDto> {
    return this.http.post<LoginResponseDto>(`${this.apiUrl}/login`, { username, password });
  }

  saveUser(userData: LoginResponseDto) {
    const s = this.storage();
    if (!s) return;
    s.setItem('user', JSON.stringify(userData));
    if (userData.token) s.setItem('token', userData.token);
  }

  getUser(): any | null {
    const s = this.storage();
    const raw = s?.getItem('user');
    if (!raw) return null;
    try { return JSON.parse(raw); } catch { return null; }
  }

  getToken(): string | null {
    const s = this.storage();
    return s?.getItem('token') ?? null;
  }

  /** Admin si:
   *  - el objeto user guardado tiene role.name === 'admin', o
   *  - el JWT trae el claim role/roles con 'Admin'
   */
  isAdmin(): boolean {
    // 1) user guardado
    const u = this.getUser();
    const roleName = u?.role?.name;
    if (typeof roleName === 'string' && roleName.toLowerCase() === 'admin') return true;

    // 2) token (claim)
    const token = this.getToken();
    if (!token) return false;

    const payload: any = decodeJwt(token) || {};
    // distintos backends usan 'role' o 'roles'
    const roles = Array.isArray(payload.roles) ? payload.roles : [payload.role].filter(Boolean);
    return roles.map((r: string) => String(r).toLowerCase()).includes('admin');
  }

  logout() {
    const s = this.storage();
    s?.removeItem('user');
    s?.removeItem('token');
  }
}
