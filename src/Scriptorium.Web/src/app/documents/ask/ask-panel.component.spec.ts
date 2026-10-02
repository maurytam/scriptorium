import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { AskResponse, DocumentSummary } from '../document.models';
import { AskPanelComponent } from './ask-panel.component';
import { ConversationSelectionService } from './conversation-selection.service';

const doc = (id: string): DocumentSummary => ({
  id,
  fileName: `${id}.pdf`,
  fileType: 'pdf',
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status: 'ready',
});

describe('AskPanelComponent', () => {
  let fixture: ComponentFixture<AskPanelComponent>;
  let component: AskPanelComponent;
  let selection: ConversationSelectionService;
  let api: jasmine.SpyObj<DocumentApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['ask']);
    await TestBed.configureTestingModule({
      imports: [AskPanelComponent],
      providers: [{ provide: DocumentApiService, useValue: api }],
    }).compileComponents();

    selection = TestBed.inject(ConversationSelectionService);
    fixture = TestBed.createComponent(AskPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent ?? '';
  const textarea = () => root().querySelector('textarea') as HTMLTextAreaElement;
  const sendButton = () => root().querySelector('button[type=submit]') as HTMLButtonElement;
  const settle = () => {
    fixture.detectChanges();
    TestBed.flushEffects();
    fixture.detectChanges();
  };
  const open = (id = 'a') => {
    selection.open(doc(id));
    settle();
  };
  const type = (value: string) => {
    textarea().value = value;
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const answer = (value = 'Maria Rossi.', truncated = false): AskResponse => ({ answer: value, truncated });

  it('shows nothing until a document is opened', () => {
    expect(root().querySelector('section')).toBeNull();
  });

  it('shows the card titled with the document name', () => {
    open('report');

    expect(text()).toContain('Ask about report.pdf');
    expect(text()).toContain('Answers come only from its text');
  });

  it('sends the trimmed question for the open document and shows the answer', () => {
    api.ask.and.returnValue(of(answer()));
    open('report');

    type('  Who signed?  ');
    sendButton().click();
    fixture.detectChanges();

    expect(api.ask).toHaveBeenCalledOnceWith('report', 'Who signed?');
    expect(text()).toContain('Who signed?');
    expect(text()).toContain('Maria Rossi.');
    expect(textarea().value).toBe('');
  });

  it('keeps the questions and answers of the conversation in order', () => {
    api.ask.and.returnValues(of(answer('First answer.')), of(answer('Second answer.')));
    open();

    type('First?');
    sendButton().click();
    type('Second?');
    sendButton().click();
    fixture.detectChanges();

    const exchanges = root().querySelectorAll('.exchange');
    expect(exchanges.length).toBe(2);
    expect(exchanges[0].textContent).toContain('First answer.');
    expect(exchanges[1].textContent).toContain('Second answer.');
  });

  it('shows a progress indicator and disables sending while waiting for the answer', () => {
    const reply = new Subject<AskResponse>();
    api.ask.and.returnValue(reply);
    open();

    type('Who signed?');
    sendButton().click();
    fixture.detectChanges();

    expect(root().querySelector('progress')).toBeTruthy();
    expect(textarea().disabled).toBeTrue();
    expect(sendButton().disabled).toBeTrue();

    reply.next(answer());
    fixture.detectChanges();

    expect(root().querySelector('progress')).toBeNull();
    expect(textarea().disabled).toBeFalse();
  });

  it('does not send an empty question', () => {
    open();

    type('   ');

    expect(sendButton().disabled).toBeTrue();
    component.send();
    expect(api.ask).not.toHaveBeenCalled();
  });

  it('shows the server message and keeps the question when the request fails', () => {
    api.ask.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'The document is not ready. Current status: failed.' } })),
    );
    open();

    type('Who signed?');
    sendButton().click();
    fixture.detectChanges();

    expect(text()).toContain('The document is not ready. Current status: failed.');
    expect(textarea().value).toBe('Who signed?');
    expect(sendButton().disabled).toBeFalse();
  });

  it('shows a generic message when the failure has no details', () => {
    api.ask.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    open();

    type('Who signed?');
    sendButton().click();
    fixture.detectChanges();

    expect(text()).toContain('The question could not be answered');
  });

  it('sends on Enter and adds a new line on Shift+Enter', () => {
    api.ask.and.returnValue(of(answer()));
    open();
    type('Who signed?');

    textarea().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true, cancelable: true }));
    expect(api.ask).not.toHaveBeenCalled();

    const enter = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true });
    textarea().dispatchEvent(enter);
    expect(api.ask).toHaveBeenCalledTimes(1);
    expect(enter.defaultPrevented).toBeTrue();
  });

  it('closes the card from the Close button', () => {
    open();

    (Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Close') as HTMLButtonElement).click();
    settle();

    expect(selection.document()).toBeNull();
    expect(root().querySelector('section')).toBeNull();
  });

  it('starts an empty conversation when another document is opened', () => {
    api.ask.and.returnValue(of(answer('Answer about A.')));
    open('a');
    type('Question about A?');
    sendButton().click();
    fixture.detectChanges();
    expect(text()).toContain('Answer about A.');

    open('b');

    expect(text()).toContain('Ask about b.pdf');
    expect(text()).not.toContain('Answer about A.');
    expect(textarea().value).toBe('');
  });

  it('ignores a late answer about the document that was open before', () => {
    const late = new Subject<AskResponse>();
    api.ask.and.returnValue(late);
    open('a');
    type('Question about A?');
    sendButton().click();
    fixture.detectChanges();

    open('b');
    late.next(answer('Late answer about A.'));
    fixture.detectChanges();

    expect(text()).not.toContain('Late answer about A.');
    expect(component.waiting()).toBeFalse();
  });
});
