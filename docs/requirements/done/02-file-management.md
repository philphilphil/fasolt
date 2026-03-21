# Epic 2: Markdown File Management

## US-2.1 — Upload Markdown Files (P0)

As a user, I want to upload `.md` files so I can create flashcards from my existing notes.

**Acceptance criteria:**

- Drag-and-drop zone and file picker button
- Accept `.md` files only, reject others with message
- Store file content in database (not filesystem)
- Strip YAML frontmatter from display and card content (do not render as visible text)
- Show upload confirmation with filename
- Max file size: 1MB per file

## US-2.2 — View File List (P0)

As a user, I want to see all my uploaded files so I can find the notes I want to study.

**Acceptance criteria:**

- List view showing filename, upload date, number of cards created
- Sort by name, date, or card count
- Empty state with prompt to upload first file

## US-2.3 — View File Content with Markdown Preview (P0)

As a user, I want to view a rendered markdown preview of my uploaded file so I can see what's in it before creating cards.

**Acceptance criteria:**

- Markdown rendering: headings, bold/italic, lists, code blocks, links, blockquotes
- Show source markdown toggle
- Images referenced in markdown shown as alt text placeholder (image upload not supported in MVP)

_Note: Heading-level navigation is covered in US-2.5. Syntax highlighting and table rendering are P1 polish._

## US-2.4 — Delete Files (P0)

As a user, I want to delete files I no longer need.

**Acceptance criteria:**

- Confirmation dialog before deletion
- Option to keep or delete associated flashcards
- File removed from list immediately

## US-2.5 — Browse File by Headings (P1)

As a user, I want to browse my file's structure by headings so I can create cards from a specific section rather than the whole file.

**Acceptance criteria:**

- Auto-generated heading tree from markdown structure
- Click heading to scroll to section
- "Create card from this section" action on each heading
- Nested headings shown with indentation

_Depends on: US-2.3_

## US-2.6 — Bulk Upload (P2)

As a user, I want to upload multiple `.md` files at once so I can quickly import a batch of notes.

**Acceptance criteria:**

- Multi-file selection in file picker
- Drag-and-drop multiple files
- Progress indicator for batch
- Summary of successful/failed uploads

## US-2.7 — Obsidian Vault Import (P3)

As a user, I want to import a folder from my Obsidian vault so I can bring in my whole knowledge base.

**Acceptance criteria:**

- Folder/zip upload
- Preserve folder structure as groups or tags
- Convert `[[wikilinks]]` to plain text (strip brackets); do not attempt to resolve cross-file links
- Strip YAML frontmatter; optionally parse frontmatter `tags` field into card tags
- Skip non-`.md` files
- Summary showing imported file count and any skipped files
