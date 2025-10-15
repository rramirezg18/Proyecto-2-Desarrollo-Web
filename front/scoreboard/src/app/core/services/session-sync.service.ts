import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class SessionSyncService {
  constructor(private router: Router) {}

  init() {
    // Si se borra el token en otra pestaña, navegamos a /login
    window.addEventListener('storage', (e) => {
      if (e.key === 'token' && e.newValue === null) {
        this.router.navigate(['/login']);
      }
    });
  }
}
