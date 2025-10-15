// src/app/features/scoreboard/scoreboard/scoreboard.ts
import { Component, computed, effect, inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import Swal from 'sweetalert2';

import { ApiService } from '../../../core/api';
import { RealtimeService } from '../../../core/realtime';
import { AuthenticationService } from '../../../core/services/authentication.service';

import { TeamPanelComponent } from '../../../shared/team-panel/team-panel';
import { TimerComponent } from '../../../shared/timer/timer';
import { QuarterIndicatorComponent } from '../../../shared/quarter-indicator/quarter-indicator';
import { FoulsPanelComponent } from '../../../shared/fouls-panel/fouls-panel';

@Component({
  selector: 'app-scoreboard',
  standalone: true,
  templateUrl: './scoreboard.html',
  styleUrls: ['./scoreboard.css'],
  imports: [
    CommonModule,
    RouterModule,
    TeamPanelComponent,
    TimerComponent,
    QuarterIndicatorComponent,
    FoulsPanelComponent
  ]
})
export class ScoreboardComponent {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);
  private platformId = inject(PLATFORM_ID);
  private router = inject(Router);

  auth = inject(AuthenticationService);
  realtime = inject(RealtimeService);

  matchId = computed(() => Number(this.route.snapshot.paramMap.get('id') ?? '1'));

  homeName = 'A TEAM';
  awayName = 'B TEAM';

  constructor() {
    effect(() => {
      const over = this.realtime.gameOver();
      if (!over || !isPlatformBrowser(this.platformId)) return;

      const text =
        over.winner === 'draw'
          ? `Empate ${over.home} - ${over.away}`
          : over.winner === 'home'
            ? `¡Ganó ${this.homeName}! ${over.home} - ${over.away}`
            : `¡Ganó ${this.awayName}! ${over.away} - ${over.home}`;

      Swal.fire({ title: 'Fin del partido', text, icon: 'warning', position: 'top', showConfirmButton: true });
    });
  }

  async ngOnInit() {
    this.api.getMatch(this.matchId()).subscribe({
      next: (m: any) => {
        this.realtime.score.set({ home: m.homeScore, away: m.awayScore });
        this.homeName = m.homeTeam || 'A TEAM';
        this.awayName = m.awayTeam || 'B TEAM';
        if (typeof m.quarter === 'number') this.realtime.quarter.set(m.quarter);
        this.realtime.hydrateTimerFromSnapshot(m.timer);
        if (m?.fouls) {
          const fouls = m.fouls ?? { home: m.homeFouls ?? 0, away: m.awayFouls ?? 0 };
          this.realtime.hydrateFoulsFromSnapshot(fouls);
        }
      }
    });

    if (isPlatformBrowser(this.platformId)) {
      await this.realtime.connect(this.matchId());
    }
  }

  ngOnDestroy() {
    if (isPlatformBrowser(this.platformId)) {
      this.realtime.disconnect();
    }
  }


  get isAdmin(): boolean {
    try {
      if (typeof this.auth.isAdmin === 'function') return this.auth.isAdmin();
      const saved = localStorage.getItem('user');
      const user = saved ? JSON.parse(saved) : null;
      return user?.role?.name?.toLowerCase() === 'admin';
    } catch {
      return false;
    }
  }

  logout() {
    try {
      if (typeof this.auth.logout === 'function') this.auth.logout();
      else {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      }
    } finally {
      this.router.navigate(['/login']);
    }
  }
}
