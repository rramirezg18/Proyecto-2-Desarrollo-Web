import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TeamService } from '../../../services/team.service';
import { Team } from '../../../models/team';
import { AuthenticationService } from '../../../core/services/authentication.service';

@Component({
  selector: 'app-teams-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './teams-list.html',
  styleUrls: ['./teams-list.scss']
})
export class TeamsListComponent implements OnInit {
  displayedColumns: string[] = ['id', 'name', 'color', 'playersCount', 'actions'];
  dataSource: Team[] = [];

  search = '';

  // Paginación
  page = 1;
  pageSize = 10;
  totalItems = 0;

  constructor(
    private teamService: TeamService,
    private auth: AuthenticationService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadTeams();
  }

  loadTeams() {
    this.teamService.getTeams(this.page, this.pageSize, this.search)
      .subscribe(res => {
        this.dataSource = res.items;
        this.totalItems = res.totalCount;
      });
  }

  applyFilter() {
    this.page = 1;
    this.loadTeams();
  }

  onPageChange(event: PageEvent) {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadTeams();
  }

  deleteTeam(id: number) {
    if (!confirm('¿Eliminar equipo?')) return;
    this.teamService.delete(id).subscribe(() => this.loadTeams());
  }

  // Header actions

  logout() {
  // Ajusta si tu servicio devuelve un Observable/Promise
  (this as any).auth?.logout?.();
  (this as any).router?.navigate?.(['/login']);
}
}
