using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;

namespace Fasolt.Server.Infrastructure.Data;

public static class DevSeedData
{
    public const string DevEmail = "dev@fasolt.local";
    public const string DevPassword = "Dev1234!";
    public const string RegularEmail = "user@fasolt.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await userManager.FindByEmailAsync(DevEmail);
        if (existing is not null)
        {
            // Ensure dev user has Admin role even if already created
            if (!await userManager.IsInRoleAsync(existing, "Admin"))
                await userManager.AddToRoleAsync(existing, "Admin");
            return;
        }

        // === Admin User ===
        var adminUser = new AppUser
        {
            UserName = DevEmail,
            Email = DevEmail,
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(adminUser, DevPassword);
        await userManager.AddToRoleAsync(adminUser, "Admin");

        // === Regular User ===
        var regularUser = new AppUser
        {
            UserName = RegularEmail,
            Email = RegularEmail,
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(regularUser, DevPassword);

        // === Admin Decks ===
        var now = DateTimeOffset.UtcNow;

        var capitalsDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "European Capitals",
            Description = "Capitals of European countries",
            CreatedAt = now,
            IsActive = true,
        };

        var programmingDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "Programming Concepts",
            Description = "Core computer science concepts",
            CreatedAt = now,
            IsActive = true,
        };

        var archivedDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "Archived Deck",
            Description = "An inactive deck for testing",
            CreatedAt = now,
            IsActive = false,
        };

        db.Decks.AddRange(capitalsDeck, programmingDeck, archivedDeck);

        // === Admin Cards — European Capitals ===
        var france = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of France?",
            Back = "Paris",
            State = "review",
            Stability = 15.5,
            Difficulty = 4.2,
            Step = null,
            DueAt = now.AddDays(5),
            LastReviewedAt = now.AddDays(-2),
            CreatedAt = now.AddDays(-10),
        };

        var germany = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Germany?",
            Back = "Berlin",
            State = "learning",
            Stability = 2.1,
            Difficulty = 5.0,
            Step = 1,
            DueAt = now.AddMinutes(10),
            LastReviewedAt = now,
            CreatedAt = now.AddDays(-3),
        };

        var spain = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Spain?",
            Back = "Madrid",
            State = "new",
            CreatedAt = now.AddDays(-1),
        };

        var italy = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Italy?",
            Back = "Rome",
            State = "new",
            CreatedAt = now,
        };

        db.Cards.AddRange(france, germany, spain, italy);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = capitalsDeck.Id, CardId = france.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = germany.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = spain.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = italy.Id }
        );

        // === Admin Cards — Programming Concepts ===
        const string linkedListSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 100" width="400" height="100">
              <rect x="10" y="30" width="60" height="40" rx="4" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="40" y="55" text-anchor="middle" font-size="14" fill="currentColor">A</text>
              <line x1="70" y1="50" x2="110" y2="50" stroke="currentColor" stroke-width="2" marker-end="url(#arrow)"/>
              <rect x="110" y="30" width="60" height="40" rx="4" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="140" y="55" text-anchor="middle" font-size="14" fill="currentColor">B</text>
              <line x1="170" y1="50" x2="210" y2="50" stroke="currentColor" stroke-width="2" marker-end="url(#arrow)"/>
              <rect x="210" y="30" width="60" height="40" rx="4" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="240" y="55" text-anchor="middle" font-size="14" fill="currentColor">C</text>
              <line x1="270" y1="50" x2="310" y2="50" stroke="currentColor" stroke-width="2" marker-end="url(#arrow)"/>
              <text x="330" y="55" text-anchor="middle" font-size="14" fill="currentColor">null</text>
              <defs><marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor"/></marker></defs>
            </svg>
            """;

        var linkedList = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is a linked list?",
            Back = "A linear data structure where each element (node) contains a value and a pointer to the next node. Unlike arrays, elements are not stored contiguously in memory.",
            FrontSvg = linkedListSvg,
            State = "new",
            CreatedAt = now.AddDays(-2),
        };

        const string bigOSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
              <line x1="50" y1="220" x2="380" y2="220" stroke="currentColor" stroke-width="1.5"/>
              <line x1="50" y1="220" x2="50" y2="20" stroke="currentColor" stroke-width="1.5"/>
              <text x="390" y="225" font-size="11" fill="currentColor">n</text>
              <text x="30" y="15" font-size="11" fill="currentColor">time</text>
              <line x1="50" y1="200" x2="370" y2="200" stroke="#22c55e" stroke-width="2"/>
              <text x="372" y="204" font-size="10" fill="#22c55e">O(1)</text>
              <path d="M 50 200 Q 200 180 370 140" fill="none" stroke="#3b82f6" stroke-width="2"/>
              <text x="372" y="144" font-size="10" fill="#3b82f6">O(log n)</text>
              <line x1="50" y1="200" x2="370" y2="60" stroke="#eab308" stroke-width="2"/>
              <text x="372" y="64" font-size="10" fill="#eab308">O(n)</text>
              <path d="M 50 200 Q 180 120 300 40" fill="none" stroke="#f97316" stroke-width="2"/>
              <text x="302" y="36" font-size="10" fill="#f97316">O(n²)</text>
            </svg>
            """;

        var bigO = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is Big O notation?",
            Back = "A mathematical notation that describes the upper bound of an algorithm's time or space complexity as the input size grows. Common complexities: O(1), O(log n), O(n), O(n log n), O(n²).",
            BackSvg = bigOSvg,
            State = "review",
            Stability = 22.0,
            Difficulty = 5.5,
            Step = null,
            DueAt = now.AddDays(7),
            LastReviewedAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-14),
        };

        const string treeFrontSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 150" width="200" height="150">
              <line x1="100" y1="30" x2="50" y2="90" stroke="currentColor" stroke-width="2"/>
              <line x1="100" y1="30" x2="150" y2="90" stroke="currentColor" stroke-width="2"/>
              <circle cx="100" cy="30" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="100" y="35" text-anchor="middle" font-size="14" fill="currentColor">8</text>
              <circle cx="50" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="50" y="95" text-anchor="middle" font-size="14" fill="currentColor">3</text>
              <circle cx="150" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="150" y="95" text-anchor="middle" font-size="14" fill="currentColor">10</text>
            </svg>
            """;

        const string treeBackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 150" width="200" height="150">
              <line x1="100" y1="30" x2="50" y2="90" stroke="currentColor" stroke-width="2"/>
              <line x1="100" y1="30" x2="150" y2="90" stroke="currentColor" stroke-width="2"/>
              <circle cx="100" cy="30" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="100" y="35" text-anchor="middle" font-size="14" fill="currentColor">root</text>
              <circle cx="50" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="50" y="95" text-anchor="middle" font-size="14" fill="currentColor">L</text>
              <circle cx="150" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="150" y="95" text-anchor="middle" font-size="14" fill="currentColor">R</text>
              <text x="100" y="140" text-anchor="middle" font-size="11" fill="currentColor">Left &lt; Root &lt; Right</text>
            </svg>
            """;

        var binaryTree = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "Explain a binary tree",
            Back = "A tree data structure where each node has at most two children, referred to as left and right. Binary search trees maintain the property: left < parent < right.",
            FrontSvg = treeFrontSvg,
            BackSvg = treeBackSvg,
            State = "new",
            CreatedAt = now.AddDays(-2),
        };

        db.Cards.AddRange(linkedList, bigO, binaryTree);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = programmingDeck.Id, CardId = linkedList.Id },
            new DeckCard { DeckId = programmingDeck.Id, CardId = bigO.Id },
            new DeckCard { DeckId = programmingDeck.Id, CardId = binaryTree.Id }
        );

        // === Admin Cards — Archived Deck ===
        var recursion = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is recursion?",
            Back = "A technique where a function calls itself to solve a problem by breaking it into smaller subproblems. Requires a base case to terminate.",
            State = "new",
            CreatedAt = now.AddDays(-5),
        };

        db.Cards.Add(recursion);
        db.DeckCards.Add(new DeckCard { DeckId = archivedDeck.Id, CardId = recursion.Id });

        // === Admin Cards — Orphaned (no deck) ===
        var moonLanding = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What year was the moon landing?",
            Back = "1969 — Apollo 11, with astronauts Neil Armstrong and Buzz Aldrin.",
            State = "new",
            CreatedAt = now.AddDays(-7),
        };

        var speedOfLight = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the speed of light?",
            Back = "Approximately 300,000 km/s (299,792,458 m/s) in a vacuum.",
            SourceFile = "physics-notes.md",
            SourceHeading = "Constants",
            State = "new",
            CreatedAt = now.AddDays(-4),
        };

        // === Admin Cards — Source metadata (no deck) ===
        var photosynthesis = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is photosynthesis?",
            Back = "The process by which plants convert sunlight, water, and carbon dioxide into glucose and oxygen. Occurs primarily in chloroplasts.",
            SourceFile = "biology-notes.md",
            SourceHeading = "Plant Processes",
            State = "new",
            CreatedAt = now.AddDays(-6),
        };

        db.Cards.AddRange(moonLanding, speedOfLight, photosynthesis);

        // === Admin Deck — Markdown Tests ===
        var markdownDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "Markdown Tests",
            Description = "Cards with various markdown elements for testing rendering and stripping",
            CreatedAt = now,
            IsActive = true,
        };

        db.Decks.Add(markdownDeck);

        var mdBold = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What does **bold** text look like?",
            Back = "**Bold text** is rendered with a heavier font weight.\n\nYou can also use __double underscores__ for bold.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Bold",
            State = "new",
            CreatedAt = now,
        };

        var mdItalic = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "How do you write *italic* text?",
            Back = "Use *single asterisks* or _single underscores_ for _italic_ text.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Italic",
            State = "new",
            CreatedAt = now,
        };

        var mdInlineCode = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What does `inline code` look like in markdown?",
            Back = "Wrap text in `backticks` to render it as inline code, e.g. `console.log()` or `git status`.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Inline Code",
            State = "new",
            CreatedAt = now,
        };

        var mdCodeBlock = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "## Code Blocks\n\nHow do you write a fenced code block?",
            Back = "Use triple backticks with an optional language:\n\n```python\ndef hello():\n    print(\"Hello, world!\")\n```\n\nThis enables syntax highlighting.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Code Blocks",
            State = "new",
            CreatedAt = now,
        };

        var mdHeadings = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "# Heading Levels\n\nHow many heading levels does markdown support?",
            Back = "Six levels:\n\n# Heading 1\n## Heading 2\n### Heading 3\n#### Heading 4\n##### Heading 5\n###### Heading 6",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Headings",
            State = "new",
            CreatedAt = now,
        };

        var mdUnorderedList = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "### Unordered Lists\n\nName three **benefits** of spaced repetition",
            Back = "- Improved long-term retention\n- Efficient use of study time\n- Reduced cramming before exams\n- Builds on the spacing effect from cognitive psychology",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Unordered Lists",
            State = "new",
            CreatedAt = now,
        };

        var mdOrderedList = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What are the steps to create a **pull request**?",
            Back = "1. Create a feature branch\n2. Make your changes and commit\n3. Push the branch to the remote\n4. Open a PR on GitHub\n5. Request reviews and address feedback\n6. Merge when approved",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Ordered Lists",
            State = "new",
            CreatedAt = now,
        };

        var mdLink = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "How do you create a [link](https://example.com) in markdown?",
            Back = "Use `[text](url)` syntax:\n\n[Fasolt on GitHub](https://github.com/fasolt)\n\nThe text in brackets is displayed, the URL in parentheses is the target.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Links",
            State = "new",
            CreatedAt = now,
        };

        var mdBlockquote = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "How do you write a blockquote?",
            Back = "> The only way to do great work is to love what you do.\n>\n> — Steve Jobs\n\nPrefix lines with `>` to create a blockquote.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Blockquotes",
            State = "new",
            CreatedAt = now,
        };

        var mdStrikethrough = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "How do you ~~cross out~~ text?",
            Back = "Wrap text in ~~double tildes~~ to render ~~strikethrough~~ text.",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Strikethrough",
            State = "new",
            CreatedAt = now,
        };

        var mdMixed = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "## Mixed **Markdown** with `code`\n\nWhat happens when you _combine_ formats?",
            Back = "You can **freely** combine:\n\n- **Bold** and *italic* and ~~strikethrough~~\n- `Inline code` within **bold text**\n- Links like [this one](https://example.com) in lists\n\n> Even **blockquotes** can contain *formatted* text and `code`.\n\n```\nAnd code blocks stand alone\n```",
            SourceFile = "markdown-guide.md",
            SourceHeading = "Mixed Formatting",
            State = "new",
            CreatedAt = now,
        };

        db.Cards.AddRange(mdBold, mdItalic, mdInlineCode, mdCodeBlock, mdHeadings,
            mdUnorderedList, mdOrderedList, mdLink, mdBlockquote, mdStrikethrough, mdMixed);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdBold.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdItalic.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdInlineCode.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdCodeBlock.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdHeadings.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdUnorderedList.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdOrderedList.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdLink.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdBlockquote.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdStrikethrough.Id },
            new DeckCard { DeckId = markdownDeck.Id, CardId = mdMixed.Id }
        );

        // === Regular User Data ===
        var mathDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Name = "Math Basics",
            Description = "Fundamental math concepts",
            CreatedAt = now,
            IsActive = true,
        };

        db.Decks.Add(mathDeck);

        var addition = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is 2+2?",
            Back = "4",
            State = "new",
            CreatedAt = now,
        };

        var squareRoot = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is the square root of 144?",
            Back = "12",
            State = "new",
            CreatedAt = now,
        };

        var pi = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is pi to 2 decimal places?",
            Back = "3.14",
            State = "review",
            Stability = 18.0,
            Difficulty = 4.5,
            Step = null,
            DueAt = now.AddDays(4),
            LastReviewedAt = now.AddDays(-3),
            CreatedAt = now.AddDays(-8),
        };

        db.Cards.AddRange(addition, squareRoot, pi);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = mathDeck.Id, CardId = addition.Id },
            new DeckCard { DeckId = mathDeck.Id, CardId = squareRoot.Id },
            new DeckCard { DeckId = mathDeck.Id, CardId = pi.Id }
        );

        await db.SaveChangesAsync();
    }
}
