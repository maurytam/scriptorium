import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, switchMap, take, timer } from 'rxjs';

import { DocumentDetails, UploadedDocument } from './document.models';

const DOCUMENTS_URL = '/api/documents';

@Injectable({ providedIn: 'root' })
export class DocumentApiService {
  private readonly http = inject(HttpClient);

  upload(file: File): Observable<UploadedDocument> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<UploadedDocument>(DOCUMENTS_URL, form);
  }

  getById(id: string): Observable<DocumentDetails> {
    return this.http.get<DocumentDetails>(`${DOCUMENTS_URL}/${id}`);
  }

  /** Polls the document until it reaches a terminal status (ready or failed), then emits it once. */
  waitUntilProcessed(id: string, intervalMs = 1000): Observable<DocumentDetails> {
    return timer(0, intervalMs).pipe(
      switchMap(() => this.getById(id)),
      filter((document) => document.status !== 'processing'),
      take(1),
    );
  }
}
