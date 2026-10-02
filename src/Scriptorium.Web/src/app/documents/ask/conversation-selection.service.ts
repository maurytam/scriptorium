import { Injectable, signal } from '@angular/core';

import { DocumentSummary } from '../document.models';

/** Which document the conversation card is open on; shared by the list and the card. */
@Injectable({ providedIn: 'root' })
export class ConversationSelectionService {
  private readonly current = signal<DocumentSummary | null>(null);

  readonly document = this.current.asReadonly();

  open(document: DocumentSummary): void {
    this.current.set(document);
  }

  close(): void {
    this.current.set(null);
  }

  /** Closes the conversation only if it is about the given document (for instance one that was just deleted). */
  clearIf(documentId: string): void {
    if (this.current()?.id === documentId) {
      this.current.set(null);
    }
  }
}
