import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { PlayerService } from './player.service';  // 👈 CORREGIDO
import { Player } from '../models/player';

describe('PlayerService', () => {
  let service: PlayerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [PlayerService]
    });

    service = TestBed.inject(PlayerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify(); // ✅ asegura que no queden requests pendientes
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch players from API', () => {
    const mockPlayers: Player[] = [
      { id: 1, number: 10, name: 'Jordan', teamId: 1, teamName: 'Bulls' },
      { id: 2, number: 23, name: 'LeBron', teamId: 2, teamName: 'Lakers' }
    ];


    service.getPlayers(1, 5).subscribe((res: { items: Player[], totalCount: number }) => {
      expect(res.items.length).toBe(2);
      expect(res.items[0].name).toBe('Jordan');
    });

    const req = httpMock.expectOne('http://localhost:5003/api/players?page=1&pageSize=5');
    expect(req.request.method).toBe('GET');

    req.flush({ items: mockPlayers, totalCount: 2 });
  });
});
