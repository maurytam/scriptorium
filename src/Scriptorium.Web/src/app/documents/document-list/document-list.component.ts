import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, expand, merge, of, switchMap, timer } from 'rxjs';

import { ConversationSelectionService } from '../ask/conversation-selection.service';
import { DocumentApiService } from '../document-api.service';
import { DocumentSummary } from '../document.models';

const POLL_INTERVAL_MS = 2000;

@Component({
  selector: 'app-document-list',
  imports: [DatePipe],
  templateUrl: './document-list.component.html',
})
export class DocumentListComponent {
  private readonly api = inject(DocumentApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly selection = inject(ConversationSelectionService);

  readonly documents = signal<DocumentSummary[]>([]);
  readonly loaded = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly confirmingId = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);

  constructor() {
    merge(of(undefined), this.api.documentsChanged)
      .pipe(
        switchMap(() => this.loadUntilSettled()),
        takeUntilDestroyed(),
      )
      .subscribe((documents) => {
        this.documents.set(documents);
        this.loaded.set(true);
        this.errorMessage.set(null);
      });
  }

  ask(document: DocumentSummary): void {
    this.selection.open(document);
  }

  askDelete(id: string): void {
    this.confirmingId.set(id);
  }

  cancelDelete(): void {
    this.confirmingId.set(null);
  }

  confirmDelete(document: DocumentSummary): void {
    this.deletingId.set(document.id);
    this.api
      .delete(document.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.finishDelete();
          this.api.notifyDocumentsChanged();
        },
        error: (error: HttpErrorResponse) => {
          this.finishDelete();
          this.errorMessage.set(error.error?.error ?? 'The document could not be deleted.');
        },
      });
  }

  private finishDelete(): void {
    this.deletingId.set(null);
    this.confirmingId.set(null);
  }

  /** Loads the list now and keeps reloading, one request at a time, while any document is processing. */
  private loadUntilSettled(): Observable<DocumentSummary[]> {
    return this.api.list().pipe(
      expand((documents) =>
        documents.some((d) => d.status === 'processing')
          ? timer(POLL_INTERVAL_MS).pipe(switchMap(() => this.api.list()))
          : EMPTY,
      ),
      catchError(() => {
        this.errorMessage.set('The document list could not be loaded.');
        return EMPTY;
      }),
    );
  }
}
