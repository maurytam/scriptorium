import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { ConversationSelectionService } from './conversation-selection.service';

interface Exchange {
  question: string;
  answer: string;
  truncated: boolean;
}

@Component({
  selector: 'app-ask-panel',
  templateUrl: './ask-panel.component.html',
})
export class AskPanelComponent {
  private readonly api = inject(DocumentApiService);
  private readonly selection = inject(ConversationSelectionService);
  private pending: Subscription | null = null;

  readonly document = this.selection.document;
  readonly transcript = signal<Exchange[]>([]);
  readonly question = signal('');
  readonly waiting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly canSend = computed(() => !this.waiting() && this.question().trim().length > 0);

  private readonly documentId = computed(() => this.document()?.id ?? null);

  constructor() {
    // Opening another document, or closing the card, starts from an empty conversation (FR-007).
    effect(() => {
      this.documentId();
      untracked(() => this.reset());
    });
  }

  onInput(event: Event): void {
    this.question.set((event.target as HTMLTextAreaElement).value);
  }

  onEnter(event: Event): void {
    const key = event as KeyboardEvent;
    if (key.shiftKey) {
      return; // Shift+Enter adds a new line
    }

    key.preventDefault();
    this.send();
  }

  send(): void {
    const document = this.document();
    if (!document || !this.canSend()) {
      return;
    }

    const text = this.question().trim();
    this.waiting.set(true);
    this.errorMessage.set(null);
    this.pending = this.api.ask(document.id, text).subscribe({
      next: (response) => {
        this.transcript.update((exchanges) => [
          ...exchanges,
          { question: text, answer: response.answer, truncated: response.truncated },
        ]);
        this.question.set('');
        this.waiting.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.error?.error ?? 'The question could not be answered. Please try again.');
        this.waiting.set(false);
      },
    });
  }

  close(): void {
    this.selection.close();
  }

  private reset(): void {
    this.pending?.unsubscribe(); // a late answer about the previous document must not appear here
    this.pending = null;
    this.transcript.set([]);
    this.question.set('');
    this.errorMessage.set(null);
    this.waiting.set(false);
  }
}
