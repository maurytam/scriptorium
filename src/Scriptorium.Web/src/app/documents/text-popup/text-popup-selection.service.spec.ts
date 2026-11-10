import { TestBed } from '@angular/core/testing';

import { DocumentSummary } from '../document.models';
import { TextPopupSelectionService } from './text-popup-selection.service';

const doc = (id: string): DocumentSummary => ({
  id,
  fileName: `${id}.pdf`,
  fileType: 'pdf',
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status: 'ready',
});

describe('TextPopupSelectionService', () => {
  let service: TextPopupSelectionService;

  beforeEach(() => {
    service = TestBed.inject(TextPopupSelectionService);
  });

  it('starts with no document open', () => {
    expect(service.document()).toBeNull();
  });

  it('opens a document', () => {
    service.open(doc('a'));

    expect(service.document()?.id).toBe('a');
  });

  it('closes the open document', () => {
    service.open(doc('a'));

    service.close();

    expect(service.document()).toBeNull();
  });

  it('replaces the open document when another one is opened', () => {
    service.open(doc('a'));

    service.open(doc('b'));

    expect(service.document()?.id).toBe('b');
  });

  it('remembers the element that opened the popup, so the focus can go back to it', () => {
    const opener = document.createElement('button');

    service.open(doc('a'), opener);

    expect(service.opener).toBe(opener);
  });

  it('has no opener when none is given', () => {
    service.open(doc('a'));

    expect(service.opener).toBeNull();
  });
});
