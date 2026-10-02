import { TestBed } from '@angular/core/testing';

import { DocumentSummary } from '../document.models';
import { ConversationSelectionService } from './conversation-selection.service';

const doc = (id: string): DocumentSummary => ({
  id,
  fileName: `${id}.pdf`,
  fileType: 'pdf',
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status: 'ready',
});

describe('ConversationSelectionService', () => {
  let service: ConversationSelectionService;

  beforeEach(() => {
    service = TestBed.inject(ConversationSelectionService);
  });

  it('starts with no document open', () => {
    expect(service.document()).toBeNull();
  });

  it('opens a document', () => {
    service.open(doc('a'));

    expect(service.document()?.id).toBe('a');
  });

  it('switches to another document', () => {
    service.open(doc('a'));
    service.open(doc('b'));

    expect(service.document()?.id).toBe('b');
  });

  it('closes the conversation', () => {
    service.open(doc('a'));

    service.close();

    expect(service.document()).toBeNull();
  });

  it('clearIf closes only the matching document', () => {
    service.open(doc('a'));

    service.clearIf('b');
    expect(service.document()?.id).toBe('a');

    service.clearIf('a');
    expect(service.document()).toBeNull();
  });
});
