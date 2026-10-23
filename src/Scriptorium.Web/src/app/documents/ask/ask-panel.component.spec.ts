import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { AskLimits, AskResponse, DocumentSummary } from '../document.models';
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
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['ask', 'getAskLimits']);
    api.getAskLimits.and.returnValue(of({ maxQuestionLength: 2000 }));
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

    expect(api.ask).toHaveBeenCalledOnceWith('report', 'Who signed?', []);
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

  it('shows the notice that only the first part was read when the answer is truncated', () => {
    api.ask.and.returnValues(of(answer('Cut answer.', true)), of(answer('Full answer.', false)));
    open();

    type('First?');
    sendButton().click();
    type('Second?');
    sendButton().click();
    fixture.detectChanges();

    const exchanges = root().querySelectorAll('.exchange');
    expect(exchanges[0].querySelector('.notice')?.textContent).toContain('based only on the first part of the document');
    expect(exchanges[1].querySelector('.notice')).toBeNull();
  });

  describe('when a question fails', () => {
    const cases: [string, number, unknown, string][] = [
      ['an invalid question (400)', 400, { error: 'The question is empty.' }, 'The question is empty.'],
      ['a document that is gone (404)', 404, { error: 'Document not found.' }, 'This document no longer exists'],
      ['a document that is not ready (409)', 409, { error: 'The document is not ready. Current status: processing.' }, 'Current status: processing.'],
      ['a model that is not available (503)', 503, { error: 'The answering model is not available. Check that Ollama is running and the model is installed.' }, 'Check that Ollama is running'],
      ['a model that is too slow (504)', 504, { error: 'The model did not answer in time. Please try again.' }, 'did not answer in time'],
      ['the server being unreachable (network error)', 0, null, 'Cannot reach the Scriptorium server'],
      ['an unexpected error (500)', 500, { error: 'An unexpected error occurred.' }, 'The question could not be answered'],
    ];

    cases.forEach(([name, status, body, expected]) => {
      it(`shows a specific message for ${name} and keeps the question for a retry`, () => {
        api.ask.and.returnValue(throwError(() => new HttpErrorResponse({ status, error: body })));
        open();

        type('Who signed?');
        sendButton().click();
        fixture.detectChanges();

        expect(root().querySelector('[role=alert]')?.textContent).toContain(expected);
        expect(textarea().value).toBe('Who signed?');
        expect(sendButton().disabled).toBeFalse();
      });
    });

    it('clears the message when the question is sent again', () => {
      api.ask.and.returnValues(throwError(() => new HttpErrorResponse({ status: 503 })), of(answer()));
      open();
      type('Who signed?');
      sendButton().click();
      fixture.detectChanges();
      expect(root().querySelector('[role=alert]')).toBeTruthy();

      sendButton().click();
      fixture.detectChanges();

      expect(root().querySelector('[role=alert]')).toBeNull();
    });
  });

  describe('character counter', () => {
    const counter = () => root().querySelector('.char-counter');
    const limits = (maxQuestionLength: number): AskLimits => ({ maxQuestionLength });

    it('asks the server for the limit each time a conversation is opened', () => {
      open('a');
      open('b');

      expect(api.getAskLimits).toHaveBeenCalledTimes(2);
    });

    it('does not ask for the limit while no document is open', () => {
      settle();

      expect(api.getAskLimits).not.toHaveBeenCalled();
      expect(counter()).toBeNull();
    });

    it('shows how much of the limit given by the server is used', () => {
      api.getAskLimits.and.returnValue(of(limits(500)));
      open();

      type('Who signed?');

      expect(counter()?.textContent).toContain('11 / 500');
    });

    it('follows a limit that changed on the server since the last conversation', () => {
      api.getAskLimits.and.returnValues(of(limits(2000)), of(limits(300)));
      open('a');
      expect(counter()?.textContent).toContain('0 / 2000');

      open('b');

      expect(counter()?.textContent).toContain('0 / 300');
    });

    it('counts the question as the server does, without the spaces around it', () => {
      api.getAskLimits.and.returnValue(of(limits(500)));
      open();

      type('   Who signed?   ');

      expect(counter()?.textContent).toContain('11 / 500');
    });

    it('accepts a question exactly at the limit', () => {
      api.getAskLimits.and.returnValue(of(limits(10)));
      open();

      type('x'.repeat(10));

      expect(counter()?.classList).not.toContain('over');
      expect(sendButton().disabled).toBeFalse();
    });

    it('blocks the send and says why when the question is over the limit', () => {
      api.ask.and.returnValue(of(answer()));
      api.getAskLimits.and.returnValue(of(limits(10)));
      open();

      type('x'.repeat(11));

      expect(counter()?.classList).toContain('over');
      expect(counter()?.textContent).toContain('too long');
      expect(sendButton().disabled).toBeTrue();
      component.send();
      expect(api.ask).not.toHaveBeenCalled();
    });

    it('lets the user send again once the question is shortened', () => {
      api.getAskLimits.and.returnValue(of(limits(10)));
      open();
      type('x'.repeat(11));

      type('x'.repeat(10));

      expect(sendButton().disabled).toBeFalse();
      expect(counter()?.classList).not.toContain('over');
    });

    it('shows no counter and does not block anything when the limit cannot be read', () => {
      api.getAskLimits.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      open();

      type('x'.repeat(5000));

      expect(counter()).toBeNull();
      expect(sendButton().disabled).toBeFalse();
    });

    it('ignores a limit that arrives after another document was opened', () => {
      const late = new Subject<AskLimits>();
      api.getAskLimits.and.returnValues(late, of(limits(300)));
      open('a');

      open('b');
      late.next(limits(10));
      fixture.detectChanges();

      expect(counter()?.textContent).toContain('0 / 300');
    });
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

  it('sends the earlier exchanges of the conversation with every new question', () => {
    api.ask.and.returnValues(of(answer('First answer.')), of(answer('Second answer.')), of(answer('Third answer.')));
    open('report');

    type('First?');
    sendButton().click();
    type('Second?');
    sendButton().click();
    type('Third?');
    sendButton().click();

    expect(api.ask.calls.argsFor(1)).toEqual([
      'report',
      'Second?',
      [{ question: 'First?', answer: 'First answer.' }],
    ]);
    expect(api.ask.calls.argsFor(2)[2]).toEqual([
      { question: 'First?', answer: 'First answer.' },
      { question: 'Second?', answer: 'Second answer.' },
    ]);
  });

  it('does not add a failed question to the history sent next', () => {
    api.ask.and.returnValues(throwError(() => new HttpErrorResponse({ status: 503 })), of(answer()));
    open('report');

    type('Lost?');
    sendButton().click();
    sendButton().click(); // retry with the question still in the input

    expect(api.ask.calls.argsFor(1)).toEqual(['report', 'Lost?', []]);
  });

  describe('New conversation', () => {
    const newConversationButton = () =>
      Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === 'New conversation') as
        | HTMLButtonElement
        | undefined;

    it('is offered only once there is something to discard', () => {
      api.ask.and.returnValue(of(answer()));
      open();
      expect(newConversationButton()).toBeUndefined();

      type('Who signed?');
      sendButton().click();
      fixture.detectChanges();

      expect(newConversationButton()).toBeDefined();
    });

    it('empties the transcript, keeps the document open and sends no history afterwards', () => {
      api.ask.and.returnValue(of(answer('Old answer.')));
      open('report');
      type('Old question?');
      sendButton().click();
      fixture.detectChanges();

      newConversationButton()!.click();
      fixture.detectChanges();

      expect(text()).not.toContain('Old answer.');
      expect(text()).toContain('Ask about report.pdf');
      expect(newConversationButton()).toBeUndefined();

      type('Fresh?');
      sendButton().click();
      expect(api.ask.calls.mostRecent().args).toEqual(['report', 'Fresh?', []]);
    });

    it('drops an answer that was still on its way', () => {
      const late = new Subject<AskResponse>();
      api.ask.and.returnValues(of(answer('First answer.')), late);
      open();
      type('First?');
      sendButton().click();
      type('Second?');
      sendButton().click();
      fixture.detectChanges();

      newConversationButton()!.click();
      late.next(answer('Late answer.'));
      fixture.detectChanges();

      expect(text()).not.toContain('Late answer.');
      expect(component.waiting()).toBeFalse();
    });
  });

  it('discards the transcript when the card is closed and opened again', () => {
    api.ask.and.returnValue(of(answer('Answer about A.')));
    open('a');
    type('Question?');
    sendButton().click();
    fixture.detectChanges();

    selection.close();
    settle();
    open('a');

    expect(text()).not.toContain('Answer about A.');
  });
});
