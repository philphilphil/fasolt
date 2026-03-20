# CLAUDE.md

## Project Overview

Spaced repetition app for markdown files. Turn your notes into flashcards and retain what you learn.

### Core Concept

Users upload `.md` files and create flashcards from them — either from the entire file or from a specific heading section. Cards are reviewed using spaced repetition (SM-2 algorithm), which schedules reviews at increasing intervals based on how well you recall each card.

### Features

- **Markdown file management** — upload, view, and delete `.md` files
- **Flashcard creation** — create cards from an entire file or a specific heading/section within a file
- **Spaced repetition study** — review due cards with quality-based scheduling (SM-2)
- **Groups** — organize cards into groups for focused study sessions
- **Dashboard** — overview of stats like cards due, total cards, study streaks
- **User accounts** — registration, login, per-user data isolation

## Tech Stack

- **Backend**: .NET 10
- **Frontend**: Vue
