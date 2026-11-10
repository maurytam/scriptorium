import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentSummary, TextPreview } from '../document.models';
import { TextPopupSelectionService } from './text-popup-selection.service';

const NOT_AVAILABLE = 'The text is not available.';

/** Read-only popup with the extracted text of the selected document, as the server sends it (already cut). */
@Component({
  selector: 'app-text-popup',
  templateUrl: './text-popup.component.html',
})
export class TextPopupComponent {
  private readonly api = inject(DocumentApiService);
  private readonly selection = inject(TextPopupSelectionService);
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private pending: Subscription | null = null;

  readonly document = this.selection.document;
  readonly loading = signal(false);
  readonly preview = signal<TextPreview | null>(null);
  readonly errorMessage = signal<string | null>(null);

  constructor() {
    effect(() => {
      const document = this.document();
      const dialog = this.dialog()?.nativeElement;
      untracked(() => this.sync(document, dialog));
    });
  }

  close(): void {
    this.selection.close();
  }

  /** The dialog closed itself (Escape) or was closed by us: clear the selection and give the focus back. */
  onClosed(): void {
    this.selection.close();
    this.selection.opener?.focus();
  }

  onBackdropClick(event: Event): void {
    if (event.target === this.dialog()?.nativeElement) {
      this.close();
    }
  }

  private sync(document: DocumentSummary | null, dialog: HTMLDialogElement | undefined): void {
    if (!dialog) {
      return;
    }

    if (document === null) {
      this.pending?.unsubscribe();
      if (dialog.open) {
        dialog.close();
      }
      return;
    }

    this.load(document);
    if (!dialog.open) {
      dialog.showModal();
    }
  }

  private load(document: DocumentSummary): void {
    this.pending?.unsubscribe(); // selecting another document cancels the request of the previous one
    this.loading.set(true);
    this.preview.set(null);
    this.errorMessage.set(null);
    this.pending = this.api.getTextPreview(document.id).subscribe({
      next: (preview) => this.finish(preview, null),
      error: (error: HttpErrorResponse) => this.finish(null, error.error?.error ?? NOT_AVAILABLE),
    });
  }

  private finish(preview: TextPreview | null, errorMessage: string | null): void {
    this.preview.set(preview);
    this.errorMessage.set(errorMessage);
    this.loading.set(false);
  }
}
