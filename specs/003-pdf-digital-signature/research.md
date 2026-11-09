# Research: Digital Signature Note for Signed PDFs

All questions below are closed; none is left as NEEDS CLARIFICATION. R1 and R2 were checked with a throwaway
program outside the repository (PdfPig 0.1.16 from the local NuGet cache, fake PDFs written as raw text, nothing
committed and no real document used).

## R1. Can PdfPig 0.1.16 find digital signatures without a new package?

**Decision**: Yes. Open the document as today, ask for its form (`PdfDocument.TryGetForm`), and take the fields of
type `AcroSignatureField`. Each signed field points through its `/V` entry to the signature dictionary.

**Rationale**: The spike returned, for a hand-written PDF with a signature field, the field as an
`AcroSignatureField`, its `/V` as an indirect reference, and the signature dictionary once resolved with
`PdfDocument.Structure.GetObject`. All of these are public PdfPig APIs. Finding the signature and reading its date
involves no cryptography, so nothing beyond PdfPig is needed (FR-009). Reading the signer's name (R8-R11) needs more
than PdfPig, but only parts of the .NET runtime itself.

**Alternatives considered**:
- *Searching the raw bytes for `/ByteRange` or `/Type /Sig`*: no form parsing needed, but it also matches text inside
  streams and cannot tell an empty field from a signed one. Rejected.
- *A PKCS#7 / CMS library (`System.Security.Cryptography.Pkcs`) to read the signing time and signer*: would give a
  certified time and the signer, but it is a separate package and is not part of the runtime. Rejected. The signer's
  name is read another way, with no package: see R10.

## R2. Where does the signing date come from, and how is it read?

**Decision**: Read the `/M` entry of the signature dictionary (a PDF date string such as `D:20260314103200+01'00'`)
and parse it with `UglyToad.PdfPig.Util.DateFormatHelper.TryParseDateTimeOffset`. When `/M` is missing or cannot be
parsed, the note is written without a date (FR-004).

**Rationale**: Observed in the spike:

| Raw `/M` | Result |
|---|---|
| `D:20260314103200+01'00'` | parsed, `+01:00` |
| `D:20260314103200Z` | parsed, `+00:00` |
| `D:20260314103200` (no zone) | parsed as `+00:00`, indistinguishable from `Z` |
| `D:2026`, `D:20260314` | parsed, with missing parts filled by defaults |
| `D:20260314103200+0100` | **not parsed** |
| `not a date`, missing | not parsed / absent |

This is the date the signer's software declares about itself; it is not certified (spec Assumptions).

**Alternatives considered**:
- *Reading the signing time inside the PKCS#7 blob*: certified, but needs a CMS library. Rejected (R1).
- *Writing my own PDF-date parser*: more code for no gain over the PdfPig helper. Rejected.

## R3. How is the date shown?

**Decision**: `yyyy-MM-dd HH:mm:ss` followed by the offset (`+01:00`, `+00:00`) **only when the raw string carries a zone
designator** (`Z`, `+hh'mm'` or `-hh'mm'`). A string with no zone designator is shown without offset. A string that does
not contain at least date, hour and minute (12 digits after `D:`) is treated as unreadable: note without date. The date is
the one in the file; it is **not** converted to the machine's time zone.

Examples of a complete line: `file firmato con firma digitale — 2026-03-14 10:32:00 +01:00`; without a readable date:
`file firmato con firma digitale`.

**Rationale**: FR-002 asks for an unambiguous format that shows the zone "when the signature provides it". Because PdfPig
turns a missing zone into `+00:00` (R2), printing the parsed offset blindly would invent a zone that the file never
declared, and a bare year such as `D:2026` would print as 1 January 00:00, a false precision. ISO ordering
(year-month-day) cannot be misread across locales, and not converting keeps the value identical to what the signing
software wrote.

**Alternatives considered**:
- *Always print the offset PdfPig returns*: simple, but wrong for zone-less dates. Rejected.
- *Convert to the local time zone*: friendlier to read, but shows a time the file never contained and depends on where the
  server runs. Rejected.
- *Italian format (`14/03/2026`)*: the note wording is Italian, but day/month order is the classic source of confusion and
  the model reading the text handles ISO dates reliably. Rejected.

## R4. Where does the note go, and how does it interact with the "no text" rule?

**Decision**: Notes first (one per line), then a blank line, then the page text. The "No extractable text" check is made on
the **page text alone**, before the notes are added.

**Rationale**: Notes at the start survive the 8,000-character cut that feature 002 applies to long documents (FR-005). Checking
emptiness on the page text keeps a signed scan rejected as today (spec Edge Cases): otherwise a note would make an image-only
PDF look readable.

**Alternatives considered**:
- *Notes at the end*: cut away first on long documents. Rejected.
- *Store the signature as separate metadata on `Document`*: needs a migration and a UI/API change, far beyond the request,
  and the model would not see it. Rejected.

## R5. Order, nesting and placeholders

**Decision**: Use the form's own field order and flatten nested fields (`AcroFormExtensions.GetFields`), keep only
`AcroSignatureField`s whose `/V` resolves to a dictionary, and skip fields with no `/V`.

**Rationale**: The spike showed a placeholder field (no `/V`) is still reported as a signature field, so the `/V` test is what
separates it from a real signature (FR-007). The two-signature case listed the fields in file order. The test with two
signatures will assert the order so a change in PdfPig would be caught (FR-003).

**Alternatives considered**: *Sorting by the signing date*: would reorder signatures that were applied in file order and breaks
down when dates are missing. Rejected.

## R6. Failure handling

**Decision**: The whole lookup runs inside the reader and catches any exception other than cancellation, returning "no
signatures" in that case. The parser's existing `catch` blocks (encrypted, corrupted) are untouched.

**Rationale**: FR-010 and SC-004: a PDF that uploaded before must still upload. A malformed form must degrade to "no note", not
to "Corrupted or unreadable PDF". Cancellation is not swallowed, as in the parser today.

## R7. How the parser receives the reader

**Decision**: Constructor injection of an interface: `PdfDocumentParser(IPdfSignatureNoteReader reader)`, with
`IPdfSignatureNoteReader` implemented by `PdfSignatureNoteReader` and registered as a singleton in `Program.cs`. The
interface lives in `Scriptorium.Infrastructure/Parsing`, not in Core. The existing test `new PdfDocumentParser()` becomes
`new PdfDocumentParser(new PdfSignatureNoteReader())`, and the parser also gets Moq tests against the interface.

**Rationale**: Principle V, and the constitution's workflow rule "write the interface before the implementation" (decided
by the user after the consistency analysis, 2026-11-09). The interface is not in Core because it exposes the PdfPig
`PdfDocument` type and nothing outside Infrastructure uses it. It also lets the parser be tested without building signed
PDFs. The reader is stateless, so a singleton is safe.

**Alternatives considered**: *A parameterless constructor that creates the reader itself*: hides a dependency and makes the
parser harder to test in isolation. Rejected. *No interface, injecting the concrete class*: was the first proposal (one
implementation, no consumer outside Infrastructure); dropped because it departs from the interface-first workflow.
*The interface in Core*: would force Core to reference PdfPig or to wrap `PdfDocument`. Rejected.

---

# Addition of 2026-11-09: who signed (User Story 3)

The user asked for the signer's name in the note: "use the signature's own name field if it has the signer, otherwise
read the certificate". R8-R12 were checked with a second throwaway program outside the repository (fake certificates made
with the runtime's `CertificateRequest`, a hand-built signed-data structure, nothing committed, no real document).

## R8. Where does the signer's name come from, and in which order?

**Decision**: First the `/Name` entry of the signature dictionary. If it is missing or blank, the name of the signer's
certificate found in `/Contents`. If neither gives a name, the note says "firmatario sconosciuto" (FR-011, FR-012, FR-013, FR-016).

**Rationale**: It is the order the user asked for. `/Name` is the cheap path (a text entry); the certificate is the path that
works for most real signatures, which usually do not fill `/Name`. The certificate is parsed only when `/Name` gives nothing.

**Alternatives considered**: *Certificate first*: more work on every signature and contradicts the user's order. Rejected.
*Showing both names when they differ*: more text and a choice the spec does not ask for. Rejected.

## R9. How is `/Name` read?

**Decision**: Accept the two token types PdfPig produces. A literal string arrives as a `StringToken` already decoded
(`Data`, `PdfDocEncoding`). A hexadecimal string arrives as a `HexToken` with raw bytes: if they start with `FE FF` they are
UTF-16 big-endian after the BOM, otherwise they are read as Latin-1 (the PDF default text encoding is close to it). A blank
result counts as "no name".

**Rationale**: Observed in the spike: `/Name (Mario Rossi)` gave `StringToken`, `Data='Mario Rossi'`; `/Name <FEFF004D00E000720069006F>` gave a
`HexToken` with the raw bytes, so decoding is ours to do.

**Alternatives considered**: *Reading only literal strings*: would miss names written by tools that use hexadecimal strings
with accents. Rejected.

## R10. How is the certificate read, with no new package?

**Decision**: The signature's `/Contents` (a `HexToken`) holds a CMS "signed data" structure, trailing zero bytes included.
Read it with `System.Formats.Asn1` (BER rules, so indefinite lengths used by some producers are accepted): take the
`certificates` set, read every certificate with `X509CertificateLoader.LoadCertificate` (each one in its own try/catch), then
read the first signer-info and pick the certificate it points to, either by issuer and serial number or by subject key
identifier. The name is `GetNameInfo(X509NameType.SimpleName)` (the common name, with the framework's own fallbacks).

**Rationale**: Both libraries ship with the .NET 10 runtime (`System.Formats.Asn1.dll`,
`System.Security.Cryptography.X509Certificates.dll` are in `Microsoft.NETCore.App`; `System.Security.Cryptography.Pkcs` is
not). The spike built a structure with two certificates, the authority first and the signer second, and returned
`ROSSI MARIO`, the signer, not the authority. Matching by the signer-info is what keeps the authority's name out (FR-013).

**Alternatives considered**:
- *Taking the first certificate*: wrong whenever the authority comes first. Rejected.
- *Taking the first certificate that is not a CA*: a guess that fails for unusual chains. Rejected: if the signer-info cannot be
  matched, the signer is "firmatario sconosciuto" (FR-013).
- *Verifying the chain and the signature*: out of scope (FR-008) and not needed to read a name.

## R11. Sanitizing and wording the name

**Decision**: Replace line breaks and control characters with spaces, collapse repeated spaces, trim, cut at 100 characters; a
result that is empty counts as no name (FR-014). The note reads `file firmato con firma digitale da <name> — <date>`, or
`... da <name>` when there is no date. When there is no name the literal `firmatario sconosciuto` takes its place (FR-016, user's request of
2026-11-09): `... (firmatario sconosciuto) — <date>`. The note reader writes this literal; `ISignerNameReader` keeps returning null.

**Rationale**: The name comes from the file and ends up in the text the model reads: a newline followed by instructions would
add a line of its own to the note. Sanitizing keeps it one short line of data. "da" follows the user's request to write *who*
signed. The name is shown as the file declares it and is not verified (FR-008).

**Alternatives considered**: *"certificato intestato a"*: more accurate about what is verified, but it is not how the user phrased
the need ("write who signed"). Kept the plain "da"; the spec's Assumptions state that the name is not verified. *Leaving the name
unsanitized*: lets a crafted file shape the text. Rejected.

## R12. Classes and interfaces

**Decision**: Two small units, each with an interface in Infrastructure written before the class (constitution workflow):
`ISignerNameReader` / `SignerNameReader` (R8, R9, R11: `/Name`, then the certificate, then cleaning) and
`ICmsSignerCertificateReader` / `CmsSignerCertificateReader` (R10: from the `/Contents` bytes to the signer certificate's name).
`PdfSignatureNoteReader` receives `ISignerNameReader` by constructor and only builds the line.

**Rationale**: Three responsibilities (assembling the note, choosing and cleaning the name, parsing CMS) each in a class of its
own, each method under 30 lines (Principle VI), each with its own test class (Principle VIII). The split also lets the name logic
be tested with a mocked certificate reader, without building certificates.

**Alternatives considered**: *All inside `PdfSignatureNoteReader`*: about 150 lines and three reasons to change. Rejected.
*One class for name and certificate*: the CMS parsing would hide the simple "which source wins" rule. Rejected.

## R13. What changes for existing decisions

- R1 and the plan's performance note said the signature bytes are never read: now `/Contents` is read, but only when `/Name`
  gives no name.
- R7 (constructor injection): `PdfSignatureNoteReader` gets a constructor parameter, so every `new PdfSignatureNoteReader()` in
  the tests changes to take a `SignerNameReader` (or a mock).
- FR-008 (no signer) of the first version is replaced by "no verification of the signer".
