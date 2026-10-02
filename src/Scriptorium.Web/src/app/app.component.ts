import { Component } from '@angular/core';

import { AskPanelComponent } from './documents/ask/ask-panel.component';
import { DocumentListComponent } from './documents/document-list/document-list.component';
import { UploadComponent } from './documents/upload/upload.component';

@Component({
  selector: 'app-root',
  imports: [UploadComponent, AskPanelComponent, DocumentListComponent],
  templateUrl: './app.component.html'
})
export class AppComponent {
  title = 'Scriptorium';
}
