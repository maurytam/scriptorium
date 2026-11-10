import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';

import { ConversationSelectionService } from '../ask/conversation-selection.service';
import { DocumentApiService } from '../document-api.service';
import { DocumentSummary, TextPreview } from '../document.models';
import { TextPopupComponent } from './text-popup.component';
import { TextPopupSelectionService } from './text-popup-selection.service';

const doc = (id: string): DocumentSummary => ({
  id,
  fileName: `${id}.pdf`,
  fileType: 'pdf',
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status: 'ready',
});

const preview = (documentId: string, content: string, overrides: Partial<TextPreview> = {}): TextPreview => ({
  documentId,
  content,
  truncated: false,
  totalCharacters: content.length,
  ...overrides,
});

describe('TextPopupComponent', () => {
  let api: jasmine.SpyObj<DocumentApiService>;
  let selection: TextPopupSelectionService;
  let fixture: ComponentFixture<TextPopupComponent>;

  beforeEach(() => {
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['getTextPreview']);
    TestBed.configureTestingModule({
      imports: [TextPopupComponent],
      providers: [{ provide: DocumentApiService, useValue: api }],
    });
    selection = TestBed.inject(TextPopupSelectionService);
    fixture = TestBed.createComponent(TextPopupComponent);
    fixture.detectChanges();
  });

  // The selection is an effect: one pass runs it, the next one renders what it produced.
  const settle = () => {
    fixture.detectChanges();
    fixture.detectChanges();
  };
  const root = () => fixture.nativeElement as HTMLElement;
  const dialog = () => root().querySelector('dialog') as HTMLDialogElement;
  const textArea = () => root().querySelector('.text-popup-text') as HTMLElement | null;
  const show = (document: DocumentSummary, result: Observable<TextPreview>, opener: HTMLElement | null = null) => {
    api.getTextPreview.and.returnValue(result);
    selection.open(document, opener);
    settle();
  };
  // The dialog reports its closing with an event queued by the browser; wait for it and for our handler to run.
  const closed = () =>
    new Promise<void>((resolve) => dialog().addEventListener('close', () => setTimeout(resolve), { once: true }));

  it('is closed while no document is selected', () => {
    expect(dialog().open).toBeFalse();
    expect(api.getTextPreview).not.toHaveBeenCalled();
  });

  it('opens a modal dialog titled with the file name and says it is loading', () => {
    show(doc('report'), new Subject<TextPreview>());

    expect(dialog().open).toBeTrue();
    expect(root().querySelector('h2')?.textContent).toBe('Extracted text of report.pdf');
    expect(root().textContent).toContain('Loading…');
    expect(api.getTextPreview).toHaveBeenCalledOnceWith('report');
  });

  it('shows the text with its line breaks and no cutting notice when it was not cut', () => {
    show(doc('report'), of(preview('report', 'first line\n\nsecond line')));

    expect(textArea()?.textContent).toBe('first line\n\nsecond line');
    expect(getComputedStyle(textArea() as HTMLElement).whiteSpace).toBe('pre-wrap');
    expect(root().textContent).not.toContain('Loading…');
    expect(root().querySelector('[role="note"]')).toBeNull();
  });

  it('shows text that looks like markup as plain characters', () => {
    const content = '<b>bold</b> <script>alert(1)</script>';

    show(doc('report'), of(preview('report', content)));

    expect(textArea()?.textContent).toBe(content);
    expect(textArea()?.querySelector('b, script')).toBeNull();
  });

  it('has no editable control, so the text is read-only', () => {
    show(doc('report'), of(preview('report', 'some text')));

    expect(root().querySelector('textarea, input, [contenteditable]')).toBeNull();
  });

  describe('when the text cannot be loaded', () => {
    it('shows the server message of a conflict', () => {
      const error = new HttpErrorResponse({
        status: 409,
        error: { error: 'Document is not ready. Current status: failed.' },
      });

      show(doc('report'), throwError(() => error));

      expect(root().textContent).toContain('Document is not ready. Current status: failed.');
      expect(textArea()).toBeNull();
    });

    it('shows a generic message for a missing document or a network error', () => {
      show(doc('gone'), throwError(() => new HttpErrorResponse({ status: 404 })));
      expect(root().textContent).toContain('The text is not available.');

      selection.close();
      settle();
      show(doc('offline'), throwError(() => new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') })));
      expect(root().textContent).toContain('The text is not available.');
    });

    it('can still be closed', () => {
      show(doc('report'), throwError(() => new HttpErrorResponse({ status: 404 })));

      (root().querySelector('button') as HTMLButtonElement).click();
      settle();

      expect(selection.document()).toBeNull();
      expect(dialog().open).toBeFalse();
    });
  });

  describe('closing', () => {
    beforeEach(() => show(doc('report'), of(preview('report', 'some text'))));

    it('closes with the Close button', () => {
      (root().querySelector('button') as HTMLButtonElement).click();
      settle();

      expect(selection.document()).toBeNull();
      expect(dialog().open).toBeFalse();
    });

    it('closes when the dialog is cancelled, as the Escape key does', () => {
      dialog().dispatchEvent(new Event('cancel'));
      settle();

      expect(selection.document()).toBeNull();
      expect(dialog().open).toBeFalse();
    });

    it('closes on a click outside the content, on the backdrop', () => {
      dialog().click();
      settle();

      expect(selection.document()).toBeNull();
      expect(dialog().open).toBeFalse();
    });

    it('stays open on a click inside the content', () => {
      (textArea() as HTMLElement).click();
      settle();

      expect(selection.document()?.id).toBe('report');
      expect(dialog().open).toBeTrue();
    });

    it('gives the focus back to the button that opened it', async () => {
      selection.close();
      settle();
      const opener = document.createElement('button');
      document.body.appendChild(opener);
      try {
        show(doc('report'), of(preview('report', 'some text')), opener);

        const done = closed();
        (root().querySelector('button') as HTMLButtonElement).click();
        settle();
        await done;

        expect(document.activeElement).toBe(opener);
      } finally {
        opener.remove();
      }
    });
  });

  it('shows only the text of the document selected last when the selection changes while loading', () => {
    const first = new Subject<TextPreview>();
    api.getTextPreview.and.callFake((id: string) => (id === 'a' ? first : of(preview('b', 'text of b'))));

    selection.open(doc('a'));
    settle();
    selection.open(doc('b'));
    settle();
    first.next(preview('a', 'text of a'));
    settle();

    expect(textArea()?.textContent).toBe('text of b');
    expect(root().querySelector('h2')?.textContent).toBe('Extracted text of b.pdf');
    expect(first.observed).toBeFalse();
  });

  it('does not touch the question conversation', () => {
    const conversation = TestBed.inject(ConversationSelectionService);
    spyOn(conversation, 'open');
    spyOn(conversation, 'close');
    spyOn(conversation, 'clearIf');

    show(doc('report'), of(preview('report', 'some text')));
    selection.close();
    settle();

    expect(conversation.open).not.toHaveBeenCalled();
    expect(conversation.close).not.toHaveBeenCalled();
    expect(conversation.clearIf).not.toHaveBeenCalled();
    expect(conversation.document()).toBeNull();
  });
});
