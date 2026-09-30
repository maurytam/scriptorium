import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, Subject, filter, map, switchMap, take, timer } from 'rxjs';

import {
  DocumentDetails,
  DocumentSummary,
  UploadEvent,
  UploadLimits,
  UploadedDocument,
} from './document.models';

const DOCUMENTS_URL = '/api/documents';

@Injectable({ providedIn: 'root' })
export class DocumentApiService {
  private readonly http = inject(HttpClient);
  private readonly changed = new Subject<void>();

  /** Emits whenever a document was added, so the list can refresh. */
  readonly documentsChanged: Observable<void> = this.changed.asObservable();

  notifyDocumentsChanged(): void {
    this.changed.next();
  }

  list(): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(DOCUMENTS_URL);
  }

  /** Emits progress events while the file is being sent, then a single completed event. */
  upload(file: File, isPrivate = false): Observable<UploadEvent> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('isPrivate', String(isPrivate));
    return this.http
      .post<UploadedDocument>(DOCUMENTS_URL, form, { observe: 'events', reportProgress: true })
      .pipe(
        map((event) => this.toUploadEvent(event)),
        filter((event): event is UploadEvent => event !== null),
      );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${DOCUMENTS_URL}/${id}`);
  }

  getLimits(): Observable<UploadLimits> {
    return this.http.get<UploadLimits>(`${DOCUMENTS_URL}/limits`);
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

  private toUploadEvent(event: HttpEvent<UploadedDocument>): UploadEvent | null {
    if (event.type === HttpEventType.UploadProgress) {
      const percent = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
      return { kind: 'progress', percent };
    }

    if (event.type === HttpEventType.Response && event.body) {
      return { kind: 'completed', document: event.body };
    }

    return null;
  }
}
