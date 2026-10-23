import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { describeAskFailure } from './ask-failure-message';
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
  private limitsRequest: Subscription | null = null;

  readonly document = this.selection.document;
  readonly transcript = signal<Exchange[]>([]);
  readonly question = signal('');
  readonly waiting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly maxQuestionLength = signal<number | null>(null);
  // The server trims the question before measuring it, so the counter does the same.
  readonly questionLength = computed(() => this.question().trim().length);
  readonly tooLong = computed(() => {
    const max = this.maxQuestionLength();
    return max !== null && this.questionLength() > max;
  });
  readonly canSend = computed(() => !this.waiting() && this.questionLength() > 0 && !this.tooLong());

  private readonly documentId = computed(() => this.document()?.id ?? null);

  constructor() {
    // Opening another document, or closing the card, starts from an empty conversation (FR-007).
    effect(() => {
      const id = this.documentId();
      untracked(() => {
        this.reset();
        if (id !== null) {
          this.loadLimits();
        }
      });
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
    const history = this.transcript().map(({ question, answer }) => ({ question, answer }));
    this.pending = this.api.ask(document.id, text, history).subscribe({
      next: (response) => {
        this.transcript.update((exchanges) => [
          ...exchanges,
          { question: text, answer: response.answer, truncated: response.truncated },
        ]);
        this.question.set('');
        this.waiting.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(describeAskFailure(error));
        this.waiting.set(false);
      },
    });
  }

  close(): void {
    this.selection.close();
  }

  newConversation(): void {
    this.reset();
  }

  // Read each time a conversation opens, so a limit changed in the server settings shows up without a reload.
  private loadLimits(): void {
    this.limitsRequest = this.api.getAskLimits().subscribe({
      next: (limits) => this.maxQuestionLength.set(limits.maxQuestionLength),
      // Without the limit there is no pre-check; the server still validates every question.
      error: () => undefined,
    });
  }

  private reset(): void {
    this.limitsRequest?.unsubscribe();
    this.limitsRequest = null;
    this.pending?.unsubscribe(); // a late answer about the previous document must not appear here
    this.pending = null;
    this.transcript.set([]);
    this.question.set('');
    this.errorMessage.set(null);
    this.waiting.set(false);
  }
}
