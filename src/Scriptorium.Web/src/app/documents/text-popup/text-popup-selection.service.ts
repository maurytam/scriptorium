import { Injectable, signal } from '@angular/core';

import { DocumentSummary } from '../document.models';

/** Which document's extracted text the popup is open on; shared by the list and the popup. */
@Injectable({ providedIn: 'root' })
export class TextPopupSelectionService {
  private readonly current = signal<DocumentSummary | null>(null);
  private openerElement: HTMLElement | null = null;

  readonly document = this.current.asReadonly();

  /** The button that opened the popup, so the focus can go back to it (not every browser focuses a clicked button). */
  get opener(): HTMLElement | null {
    return this.openerElement;
  }

  open(document: DocumentSummary, opener: HTMLElement | null = null): void {
    this.openerElement = opener;
    this.current.set(document);
  }

  close(): void {
    this.current.set(null);
  }
}
