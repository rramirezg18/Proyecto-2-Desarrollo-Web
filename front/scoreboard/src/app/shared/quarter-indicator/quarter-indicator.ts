import { Component, computed, inject } from '@angular/core';
import { RealtimeService } from '../../core/realtime';

@Component({
  selector: 'app-quarter-indicator',
  standalone: true,
  templateUrl: './quarter-indicator.html',
  styleUrls: ['./quarter-indicator.css']
})
export class QuarterIndicatorComponent {
  private rt = inject(RealtimeService);
  quarter = computed(() => this.rt.quarter());
}
