using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;

namespace Fasolt.Server.Infrastructure.Data;

/// <summary>
/// Generates bulk content + a synthetic review history for the dev admin user
/// so a freshly-seeded database has enough volume + activity to exercise the
/// Study hero, Decks grid, Progress heatmap, rating mix, Cards bulk-edit etc.
///
/// Deterministic — seeded RNG so the same DB always produces the same shape.
/// </summary>
public static class DevSeedHistory
{
    private const int RngSeed = 42;
    private const int HistoryDays = 60;

    public static (List<Deck> Decks, List<Card> Cards, List<DeckCard> DeckCards, List<ReviewLog> Logs, int BestStreak)
        Build(string adminUserId, DateTimeOffset now)
    {
        var rng = new Random(RngSeed);

        var decks = new List<Deck>();
        var cards = new List<Card>();
        var deckCards = new List<DeckCard>();

        foreach (var (deckName, deckDesc, items) in BulkContent)
        {
            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = adminUserId,
                Name = deckName,
                Description = deckDesc,
                CreatedAt = now.AddDays(-rng.Next(40, HistoryDays)),
            };
            decks.Add(deck);

            foreach (var (front, back) in items)
            {
                var ageDays = rng.Next(1, HistoryDays);
                var card = new Card
                {
                    Id = Guid.NewGuid(),
                    PublicId = NanoIdGenerator.New(),
                    UserId = adminUserId,
                    Front = front,
                    Back = back,
                    State = "new",
                    CreatedAt = now.AddDays(-ageDays).AddMinutes(rng.Next(0, 24 * 60)),
                };
                cards.Add(card);
                deckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
            }
        }

        // === Visual Reference deck — showcases SVG patterns users typically
        // ask their AI to generate (diagrams, charts, annotated illustrations). ===
        var visualDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUserId,
            Name = "Visual Reference",
            Description = "Cards with SVG diagrams: sequence flows, charts, annotated anatomy, geometry, timelines",
            CreatedAt = now.AddDays(-30),
        };
        decks.Add(visualDeck);

        foreach (var spec in VisualReferenceCards)
        {
            var ageDays = rng.Next(1, HistoryDays);
            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = adminUserId,
                Front = spec.Front,
                Back = spec.Back,
                FrontSvg = spec.FrontSvg,
                BackSvg = spec.BackSvg,
                SourceFile = "visual-notes.md",
                SourceHeading = spec.Heading,
                State = "new",
                CreatedAt = now.AddDays(-ageDays).AddMinutes(rng.Next(0, 24 * 60)),
            };
            cards.Add(card);
            deckCards.Add(new DeckCard { DeckId = visualDeck.Id, CardId = card.Id });
        }

        // Rest days — a handful of full skip days so streaks have texture and
        // the heatmap isn't a uniform wall of activity.
        var restDays = new HashSet<int>();
        while (restDays.Count < 6)
            restDays.Add(rng.Next(2, HistoryDays - 2));

        // ~20% of cards stay "new" (no history) — useful for testing the new-card flow.
        var historyCards = cards.Where(_ => rng.NextDouble() > 0.20).ToList();

        var logs = new List<ReviewLog>();
        foreach (var card in historyCards)
            SimulateReviews(card, now, rng, restDays, logs);

        // Pin a handful of cards to "due today" so the Study hero has something
        // to chew on without waiting for the SRS clock.
        var reviewableToday = cards
            .Where(c => c.State is "review" or "learning" or "relearning")
            .OrderBy(_ => rng.Next())
            .Take(18)
            .ToList();
        foreach (var c in reviewableToday)
            c.DueAt = now.AddMinutes(-rng.Next(5, 240));

        // Suspend one card so the Cards screen has a suspended row to test.
        var toSuspend = cards.FirstOrDefault(c => c.State == "review");
        if (toSuspend is not null) toSuspend.IsSuspended = true;

        // Compute best streak from the generated logs so the user profile is
        // consistent with what the Progress page will display.
        var bestStreak = ComputeBestStreak(logs, now);

        return (decks, cards, deckCards, logs, bestStreak);
    }

    private static void SimulateReviews(
        Card card,
        DateTimeOffset now,
        Random rng,
        HashSet<int> restDays,
        List<ReviewLog> logs)
    {
        var cursor = card.CreatedAt.AddMinutes(rng.Next(5, 60));
        var interval = TimeSpan.FromMinutes(10);
        var state = "learning";
        double stability = 0.5;
        double difficulty = 5.0 + (rng.NextDouble() - 0.5);
        int? step = 0;

        // Hard ceiling so a single very-old card can't generate hundreds of logs.
        const int maxReviews = 40;
        var reviewCount = 0;

        while (cursor < now && reviewCount < maxReviews)
        {
            var daysAgo = (int)Math.Floor((now - cursor).TotalDays);
            if (restDays.Contains(daysAgo))
            {
                cursor = cursor.AddDays(1);
                continue;
            }

            var rating = PickRating(rng);

            if (rating == "again")
            {
                stability = Math.Max(0.3, stability * 0.4);
                difficulty = Math.Min(10, difficulty + 0.6);
                interval = TimeSpan.FromMinutes(10);
                step = 0;
                state = "relearning";
            }
            else
            {
                var factor = rating switch
                {
                    "hard" => 1.2,
                    "good" => 2.4,
                    "easy" => 3.4,
                    _ => 1.0,
                };
                difficulty = Math.Clamp(
                    difficulty + rating switch { "hard" => 0.15, "easy" => -0.2, _ => -0.05 },
                    1.0, 10.0);
                stability = Math.Min(120, Math.Max(stability * factor, stability + 0.5));
                var jitter = 0.85 + rng.NextDouble() * 0.30;
                interval = TimeSpan.FromMinutes(
                    Math.Min(interval.TotalMinutes * factor * jitter, TimeSpan.FromDays(45).TotalMinutes));
                if (interval.TotalDays >= 1)
                {
                    state = "review";
                    step = null;
                }
                else
                {
                    state = "learning";
                    step = (step ?? 0) + 1;
                }
            }

            var scheduledDue = cursor + interval;

            logs.Add(new ReviewLog
            {
                UserId = card.UserId,
                CardId = card.Id,
                Rating = rating,
                ReviewedAt = cursor,
                ScheduledDueAfter = scheduledDue,
                StateAfter = state,
            });
            reviewCount++;

            // Real users don't review at exactly the scheduled instant — drift the
            // next session within ±4h so daily buckets get filled at varying times.
            cursor = scheduledDue.AddMinutes(rng.Next(-90, 240));
        }

        if (reviewCount > 0)
        {
            card.State = state;
            card.Stability = stability;
            card.Difficulty = difficulty;
            card.Step = step;
            card.DueAt = logs.Last(l => l.CardId == card.Id).ScheduledDueAfter;
            card.LastReviewedAt = logs.Last(l => l.CardId == card.Id).ReviewedAt;
        }
    }

    private static string PickRating(Random rng)
    {
        var r = rng.NextDouble();
        if (r < 0.08) return "again";
        if (r < 0.26) return "hard";
        if (r < 0.88) return "good";
        return "easy";
    }

    private static int ComputeBestStreak(List<ReviewLog> logs, DateTimeOffset now)
    {
        // Day-of-year buckets (UTC). Good enough for the seed — the live service
        // recomputes streaks against the user's actual time zone on first stats fetch.
        var studyDays = logs
            .Select(l => l.ReviewedAt.UtcDateTime.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (studyDays.Count == 0) return 0;

        var best = 1;
        var current = 1;
        for (var i = 1; i < studyDays.Count; i++)
        {
            if ((studyDays[i] - studyDays[i - 1]).TotalDays == 1)
            {
                current++;
                if (current > best) best = current;
            }
            else
            {
                current = 1;
            }
        }
        return best;
    }

    private static readonly (string Name, string Description, (string Front, string Back)[] Cards)[] BulkContent =
    {
        ("Spanish Verbs", "Common Spanish verbs and English meanings",
        new[]
        {
            ("hablar", "to speak\n\n*yo hablo, tú hablas, él habla*"),
            ("comer", "to eat\n\n*yo como, tú comes, él come*"),
            ("vivir", "to live\n\n*yo vivo, tú vives, él vive*"),
            ("tener", "to have *(irregular)*\n\n*yo tengo, tú tienes, él tiene*"),
            ("ser", "to be (essence) *(irregular)*\n\n*yo soy, tú eres, él es*"),
            ("estar", "to be (state) *(irregular)*\n\n*yo estoy, tú estás, él está*"),
            ("ir", "to go *(irregular)*\n\n*yo voy, tú vas, él va*"),
            ("hacer", "to do / to make *(irregular)*\n\n*yo hago, tú haces, él hace*"),
            ("poder", "to be able to *(o → ue)*\n\n*yo puedo, tú puedes, él puede*"),
            ("querer", "to want / to love *(e → ie)*\n\n*yo quiero, tú quieres, él quiere*"),
            ("decir", "to say *(irregular)*\n\n*yo digo, tú dices, él dice*"),
            ("ver", "to see\n\n*yo veo, tú ves, él ve*"),
            ("saber", "to know (a fact) *(irregular)*\n\n*yo sé, tú sabes, él sabe*"),
            ("conocer", "to know (a person/place)\n\n*yo conozco, tú conoces, él conoce*"),
            ("pensar", "to think *(e → ie)*\n\n*yo pienso, tú piensas, él piensa*"),
        }),

        ("Human Anatomy", "Body parts, organs, and systems",
        new[]
        {
            ("Largest organ in the human body?", "The **skin** — roughly 2 m² in adults, around 16% of body weight."),
            ("Function of the alveoli?", "Tiny air sacs in the lungs where **gas exchange** happens — oxygen in, CO₂ out."),
            ("How many chambers does the human heart have?", "**Four**: two atria (upper) and two ventricles (lower)."),
            ("What does the pancreas produce?", "**Insulin** and **glucagon** (hormones), plus digestive enzymes."),
            ("Where are red blood cells produced?", "In the **bone marrow**, primarily of long bones in adults."),
            ("Smallest bone in the human body?", "The **stapes**, in the middle ear — about 3mm long."),
            ("What is the function of the kidneys?", "Filter blood to remove waste and excess water, producing urine. Also regulate blood pressure and pH."),
            ("Difference between arteries and veins?", "**Arteries** carry blood *away* from the heart (typically oxygenated, high pressure). **Veins** return blood *to* the heart (typically deoxygenated, low pressure)."),
            ("What does the cerebellum control?", "**Balance, coordination, and fine motor movement**. Sits at the back of the brain below the cerebrum."),
            ("How many vertebrae in the human spine?", "**33** — 7 cervical, 12 thoracic, 5 lumbar, 5 fused sacral, 4 fused coccygeal."),
            ("Function of the liver?", "Detoxification, protein synthesis, bile production, glycogen storage. Roughly 500 distinct functions."),
            ("What is the largest muscle?", "The **gluteus maximus** — primary extensor of the hip."),
            ("How many bones in the adult human body?", "**206** bones. Babies are born with ~270; many fuse during growth."),
            ("Role of white blood cells?", "**Immune defense** — neutrophils, lymphocytes, monocytes, eosinophils, and basophils each target different threats."),
            ("What is the medulla oblongata responsible for?", "Autonomic functions: **breathing, heart rate, blood pressure**, swallowing, reflexes like coughing and sneezing."),
        }),

        ("World History Dates", "Pivotal historical events and their years",
        new[]
        {
            ("When did Columbus reach the Americas?", "**1492** — landfall at an island in the Bahamas, October 12."),
            ("Year of the French Revolution's start?", "**1789** — storming of the Bastille on July 14."),
            ("When did World War I begin?", "**1914** — triggered by the assassination of Archduke Franz Ferdinand on June 28."),
            ("Year of the Treaty of Westphalia?", "**1648** — ended the Thirty Years' War, established modern state sovereignty."),
            ("When did the Berlin Wall fall?", "**1989** — November 9, opening the way to German reunification."),
            ("Year humans first landed on the moon?", "**1969** — Apollo 11, July 20. Armstrong and Aldrin."),
            ("When was the Magna Carta sealed?", "**1215** — by King John at Runnymede, June 15."),
            ("Year of the first successful powered flight?", "**1903** — Wright brothers at Kitty Hawk, December 17."),
            ("When did the Roman Empire fall (Western)?", "**476 AD** — Romulus Augustulus deposed by Odoacer."),
            ("Year of the Russian Revolution (October)?", "**1917** — Bolsheviks under Lenin seized power."),
            ("When did the Black Death peak in Europe?", "**1347–1351** — killed roughly a third of the European population."),
            ("Year of the American Declaration of Independence?", "**1776** — adopted July 4."),
            ("When did India gain independence?", "**1947** — August 15, ending British rule."),
            ("Year the printing press was invented?", "**~1440** — Gutenberg's movable-type press in Mainz."),
            ("When did World War II end?", "**1945** — VE Day May 8 (Europe), VJ Day September 2 (Pacific)."),
        }),

        ("Chemistry Basics", "Elements, formulas, and constants",
        new[]
        {
            ("Symbol for gold?", "**Au** (from Latin *aurum*). Atomic number 79."),
            ("Symbol for sodium?", "**Na** (from Latin *natrium*). Atomic number 11."),
            ("What is the formula for water?", "**H₂O** — two hydrogen atoms bonded to one oxygen."),
            ("Atomic number of carbon?", "**6** — 6 protons, basis of all organic chemistry."),
            ("What gas do plants release during photosynthesis?", "**Oxygen (O₂)** — a byproduct of splitting water molecules."),
            ("What is the pH of pure water?", "**7** — neutral. Below 7 is acidic, above 7 is basic."),
            ("Symbol for potassium?", "**K** (from Latin *kalium*). Atomic number 19."),
            ("What is Avogadro's number?", "**6.022 × 10²³** — particles per mole."),
            ("Most abundant gas in Earth's atmosphere?", "**Nitrogen (N₂)** — ~78%. Oxygen is ~21%."),
            ("What does NaCl stand for?", "**Sodium chloride** — table salt."),
            ("Atomic number of oxygen?", "**8** — 8 protons, 8 electrons in neutral form."),
            ("What is an isotope?", "Atoms of the **same element** with the **same number of protons** but **different numbers of neutrons** (e.g. ¹²C vs ¹⁴C)."),
        }),

        ("JavaScript Quirks", "Surprising behaviors every JS dev meets eventually",
        new[]
        {
            ("`typeof null` returns what?", "`'object'` — a long-standing JS bug preserved for backwards compatibility."),
            ("What is `[] + []`?", "`''` — empty string. Both arrays coerce to empty strings before concatenation."),
            ("What is `[] + {}`?", "`'[object Object]'` — empty array becomes `''`, object becomes its string tag."),
            ("What is `0.1 + 0.2`?", "`0.30000000000000004` — floating-point representation. Common to all IEEE 754 languages."),
            ("Difference between `==` and `===`?", "`==` performs **type coercion** before comparing; `===` checks **type and value** strictly. Prefer `===`."),
            ("What does `'5' - 3` return?", "`2` — `-` forces numeric coercion. `'5' + 3` would give `'53'`."),
            ("What is the value of `this` in arrow functions?", "Arrow functions **don't have their own `this`** — they inherit from the enclosing lexical scope."),
            ("What does `NaN === NaN` evaluate to?", "`false` — NaN is the only JS value not equal to itself. Use `Number.isNaN(x)` to test."),
            ("What is the difference between `null` and `undefined`?", "**`undefined`** = variable declared but no value assigned (or missing). **`null`** = explicit \"no value\" set by the developer."),
            ("What does `Array(3)` produce?", "An array of length 3 with **empty slots** (sparse), not `[undefined, undefined, undefined]`. Methods like `.map` skip empty slots."),
        }),
    };

    private record VisualCardSpec(string Heading, string Front, string Back, string? FrontSvg, string? BackSvg);

    private static readonly VisualCardSpec[] VisualReferenceCards =
    {
        new(
            Heading: "HTTP",
            Front: "Trace one HTTP request/response cycle from client to server.",
            Back: "1. Client opens a TCP connection.\n2. Sends a **request line** (`GET /api/users HTTP/1.1`) + headers + optional body.\n3. Server returns a **status line** (`200 OK`), response headers, and a body.\n4. Connection is reused (keep-alive) or closed.",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <text x="80" y="28" text-anchor="middle" font-size="14" fill="currentColor">Client</text>
                  <text x="320" y="28" text-anchor="middle" font-size="14" fill="currentColor">Server</text>
                  <line x1="80" y1="40" x2="80" y2="225" stroke="currentColor" stroke-width="1.5"/>
                  <line x1="320" y1="40" x2="320" y2="225" stroke="currentColor" stroke-width="1.5"/>
                  <line x1="82" y1="75" x2="318" y2="75" stroke="#3b82f6" stroke-width="2" marker-end="url(#a)"/>
                  <text x="200" y="70" text-anchor="middle" font-size="12" fill="#3b82f6">GET /api/users</text>
                  <line x1="318" y1="125" x2="82" y2="125" stroke="#22c55e" stroke-width="2" marker-end="url(#a)"/>
                  <text x="200" y="120" text-anchor="middle" font-size="12" fill="#22c55e">200 OK + JSON</text>
                  <line x1="82" y1="180" x2="318" y2="180" stroke="#3b82f6" stroke-width="2" marker-end="url(#a)"/>
                  <text x="200" y="175" text-anchor="middle" font-size="12" fill="#3b82f6">DELETE /api/users/42</text>
                  <line x1="318" y1="215" x2="82" y2="215" stroke="#22c55e" stroke-width="2" marker-end="url(#a)"/>
                  <text x="200" y="210" text-anchor="middle" font-size="12" fill="#22c55e">204 No Content</text>
                  <defs><marker id="a" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor"/></marker></defs>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Trigonometry",
            Front: "Identify the function shown.",
            Back: "**y = sin(x)** — sinusoidal wave.\n\n- Period: `2π`\n- Amplitude: `1`\n- Range: `[-1, 1]`\n- Roots at `nπ` for integer `n`.",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <line x1="20" y1="125" x2="380" y2="125" stroke="currentColor" stroke-width="1.5"/>
                  <line x1="200" y1="20" x2="200" y2="230" stroke="currentColor" stroke-width="1.5"/>
                  <path d="M 20 125 Q 65 35 110 125 T 200 125 T 290 125 T 380 125" fill="none" stroke="#f97316" stroke-width="2.5"/>
                  <text x="385" y="120" font-size="11" fill="currentColor">x</text>
                  <text x="210" y="28" font-size="11" fill="currentColor">y</text>
                  <text x="200" y="245" text-anchor="middle" font-size="11" fill="currentColor">one period = 2π</text>
                  <line x1="110" y1="120" x2="110" y2="130" stroke="currentColor" stroke-width="1"/>
                  <text x="110" y="145" text-anchor="middle" font-size="10" fill="currentColor">π</text>
                  <line x1="290" y1="120" x2="290" y2="130" stroke="currentColor" stroke-width="1"/>
                  <text x="290" y="145" text-anchor="middle" font-size="10" fill="currentColor">2π</text>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Nutrition",
            Front: "What's the rough macronutrient split of a balanced diet?",
            Back: "**Carbohydrates ~50%**, **Protein ~25%**, **Fat ~25%** of total daily energy intake (broad guideline — varies by goals and individual needs).",
            FrontSvg: null,
            BackSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <path d="M 180 45 A 80 80 0 0 1 180 205 L 180 125 Z" fill="#eab308"/>
                  <path d="M 180 205 A 80 80 0 0 1 100 125 L 180 125 Z" fill="#22c55e"/>
                  <path d="M 100 125 A 80 80 0 0 1 180 45 L 180 125 Z" fill="#3b82f6"/>
                  <circle cx="180" cy="125" r="80" fill="none" stroke="currentColor" stroke-width="1"/>
                  <rect x="280" y="80" width="12" height="12" fill="#eab308"/>
                  <text x="300" y="91" font-size="13" fill="currentColor">Carbs 50%</text>
                  <rect x="280" y="115" width="12" height="12" fill="#22c55e"/>
                  <text x="300" y="126" font-size="13" fill="currentColor">Protein 25%</text>
                  <rect x="280" y="150" width="12" height="12" fill="#3b82f6"/>
                  <text x="300" y="161" font-size="13" fill="currentColor">Fat 25%</text>
                </svg>
                """
        ),
        new(
            Heading: "Astronomy",
            Front: "Rank the inner planets and Jupiter by diameter.",
            Back: "From smallest to largest: **Mercury** (4,879 km) → **Mars** (6,779 km) → **Venus** (12,104 km) → **Earth** (12,742 km) → **Jupiter** (139,820 km).",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <text x="200" y="22" text-anchor="middle" font-size="12" fill="currentColor">Diameter (relative to Jupiter)</text>
                  <line x1="60" y1="210" x2="380" y2="210" stroke="currentColor" stroke-width="1"/>
                  <rect x="80" y="200" width="36" height="10" fill="#a8a29e"/>
                  <text x="98" y="228" text-anchor="middle" font-size="11" fill="currentColor">Mercury</text>
                  <rect x="135" y="195" width="36" height="15" fill="#dc2626"/>
                  <text x="153" y="228" text-anchor="middle" font-size="11" fill="currentColor">Mars</text>
                  <rect x="190" y="180" width="36" height="30" fill="#fbbf24"/>
                  <text x="208" y="228" text-anchor="middle" font-size="11" fill="currentColor">Venus</text>
                  <rect x="245" y="178" width="36" height="32" fill="#3b82f6"/>
                  <text x="263" y="228" text-anchor="middle" font-size="11" fill="currentColor">Earth</text>
                  <rect x="300" y="50" width="36" height="160" fill="#f59e0b"/>
                  <text x="318" y="228" text-anchor="middle" font-size="11" fill="currentColor">Jupiter</text>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Graph theory",
            Front: "What does this directed graph represent? Identify in-degree of D.",
            Back: "A **directed graph** with 4 vertices `{A, B, C, D}` and edges `A→B`, `A→C`, `C→D`, `B→D`.\n\nIn-degree of **D = 2** (incoming from B and C).",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <line x1="122" y1="80" x2="278" y2="80" stroke="currentColor" stroke-width="1.5" marker-end="url(#a)"/>
                  <line x1="100" y1="102" x2="100" y2="168" stroke="currentColor" stroke-width="1.5" marker-end="url(#a)"/>
                  <line x1="122" y1="190" x2="278" y2="190" stroke="currentColor" stroke-width="1.5" marker-end="url(#a)"/>
                  <line x1="282" y1="100" x2="118" y2="172" stroke="currentColor" stroke-width="1.5" marker-end="url(#a)"/>
                  <circle cx="100" cy="80" r="22" fill="var(--paper-1)" stroke="currentColor" stroke-width="2"/>
                  <text x="100" y="86" text-anchor="middle" font-size="14" fill="currentColor">A</text>
                  <circle cx="300" cy="80" r="22" fill="var(--paper-1)" stroke="currentColor" stroke-width="2"/>
                  <text x="300" y="86" text-anchor="middle" font-size="14" fill="currentColor">B</text>
                  <circle cx="100" cy="190" r="22" fill="var(--paper-1)" stroke="currentColor" stroke-width="2"/>
                  <text x="100" y="196" text-anchor="middle" font-size="14" fill="currentColor">C</text>
                  <circle cx="300" cy="190" r="22" fill="var(--paper-1)" stroke="currentColor" stroke-width="2"/>
                  <text x="300" y="196" text-anchor="middle" font-size="14" fill="currentColor">D</text>
                  <defs><marker id="a" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor"/></marker></defs>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Cell biology",
            Front: "Name the labeled organelles.",
            Back: "- **Nucleus** — houses DNA, controls gene expression.\n- **Mitochondria** — power plants; produce ATP via oxidative phosphorylation.\n- **Cell membrane** — selectively permeable bilayer of phospholipids.",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <ellipse cx="180" cy="125" rx="130" ry="80" fill="none" stroke="currentColor" stroke-width="2"/>
                  <circle cx="180" cy="125" r="30" fill="none" stroke="#dc2626" stroke-width="2"/>
                  <circle cx="180" cy="125" r="8" fill="#dc2626"/>
                  <ellipse cx="110" cy="100" rx="18" ry="10" fill="none" stroke="#22c55e" stroke-width="2"/>
                  <ellipse cx="250" cy="160" rx="18" ry="10" fill="none" stroke="#22c55e" stroke-width="2"/>
                  <ellipse cx="240" cy="90" rx="14" ry="8" fill="none" stroke="#22c55e" stroke-width="2"/>
                  <line x1="180" y1="155" x2="180" y2="220" stroke="currentColor" stroke-width="1"/>
                  <text x="180" y="235" text-anchor="middle" font-size="11" fill="#dc2626">nucleus</text>
                  <line x1="110" y1="90" x2="80" y2="35" stroke="currentColor" stroke-width="1"/>
                  <text x="80" y="25" text-anchor="middle" font-size="11" fill="#22c55e">mitochondria</text>
                  <line x1="305" y1="155" x2="345" y2="200" stroke="currentColor" stroke-width="1"/>
                  <text x="345" y="215" text-anchor="middle" font-size="11" fill="currentColor">cell membrane</text>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Fractions",
            Front: "Mark **5/8** on the number line. Is it greater or less than **1/2**?",
            Back: "5/8 is **greater than 1/2** (which equals 4/8).\n\nIt sits between 1/2 and 3/4, closer to 3/4.",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 120" width="400" height="120">
                  <line x1="40" y1="65" x2="360" y2="65" stroke="currentColor" stroke-width="2"/>
                  <line x1="40" y1="55" x2="40" y2="75" stroke="currentColor" stroke-width="2"/>
                  <line x1="120" y1="55" x2="120" y2="75" stroke="currentColor" stroke-width="1.5"/>
                  <line x1="200" y1="55" x2="200" y2="75" stroke="currentColor" stroke-width="2"/>
                  <line x1="280" y1="55" x2="280" y2="75" stroke="currentColor" stroke-width="1.5"/>
                  <line x1="360" y1="55" x2="360" y2="75" stroke="currentColor" stroke-width="2"/>
                  <text x="40" y="95" text-anchor="middle" font-size="13" fill="currentColor">0</text>
                  <text x="120" y="95" text-anchor="middle" font-size="13" fill="currentColor">1/4</text>
                  <text x="200" y="95" text-anchor="middle" font-size="13" fill="currentColor">1/2</text>
                  <text x="280" y="95" text-anchor="middle" font-size="13" fill="currentColor">3/4</text>
                  <text x="360" y="95" text-anchor="middle" font-size="13" fill="currentColor">1</text>
                </svg>
                """,
            BackSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 120" width="400" height="120">
                  <line x1="40" y1="65" x2="360" y2="65" stroke="currentColor" stroke-width="2"/>
                  <line x1="40" y1="55" x2="40" y2="75" stroke="currentColor" stroke-width="2"/>
                  <line x1="200" y1="55" x2="200" y2="75" stroke="currentColor" stroke-width="2"/>
                  <line x1="360" y1="55" x2="360" y2="75" stroke="currentColor" stroke-width="2"/>
                  <text x="40" y="95" text-anchor="middle" font-size="13" fill="currentColor">0</text>
                  <text x="200" y="95" text-anchor="middle" font-size="13" fill="currentColor">1/2</text>
                  <text x="360" y="95" text-anchor="middle" font-size="13" fill="currentColor">1</text>
                  <line x1="240" y1="30" x2="240" y2="60" stroke="#f97316" stroke-width="2.5" marker-end="url(#b)"/>
                  <text x="240" y="22" text-anchor="middle" font-size="13" fill="#f97316">5/8</text>
                  <defs><marker id="b" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="#f97316"/></marker></defs>
                </svg>
                """
        ),
        new(
            Heading: "Pythagorean theorem",
            Front: "State the relationship between the sides a, b, c of a right triangle.",
            Back: "**a² + b² = c²**\n\n- `a` and `b` are the **legs** (the two sides forming the right angle)\n- `c` is the **hypotenuse** (opposite the right angle, always the longest side)",
            FrontSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <polygon points="100,200 300,200 300,60" fill="none" stroke="currentColor" stroke-width="2"/>
                  <polyline points="285,200 285,185 300,185" fill="none" stroke="currentColor" stroke-width="1.5"/>
                  <text x="200" y="225" text-anchor="middle" font-size="18" fill="#3b82f6">b</text>
                  <text x="320" y="135" font-size="18" fill="#22c55e">a</text>
                  <text x="180" y="125" text-anchor="middle" font-size="18" fill="#f97316">c</text>
                </svg>
                """,
            BackSvg: null
        ),
        new(
            Heading: "Computing history",
            Front: "Place ENIAC, ARPANET, WWW, and iPhone on a timeline.",
            Back: "- **1946** — ENIAC, first general-purpose electronic computer (UPenn).\n- **1969** — ARPANET, the network that became the Internet.\n- **1991** — World Wide Web released publicly by Tim Berners-Lee.\n- **2007** — iPhone, mainstream pocket computing.",
            FrontSvg: null,
            BackSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 200" width="400" height="200">
                  <line x1="30" y1="100" x2="370" y2="100" stroke="currentColor" stroke-width="2"/>
                  <circle cx="60" cy="100" r="5" fill="#3b82f6"/>
                  <line x1="60" y1="95" x2="60" y2="60" stroke="currentColor" stroke-width="1"/>
                  <text x="60" y="50" text-anchor="middle" font-size="11" fill="currentColor">1946</text>
                  <text x="60" y="128" text-anchor="middle" font-size="11" fill="#3b82f6">ENIAC</text>
                  <circle cx="155" cy="100" r="5" fill="#22c55e"/>
                  <line x1="155" y1="95" x2="155" y2="60" stroke="currentColor" stroke-width="1"/>
                  <text x="155" y="50" text-anchor="middle" font-size="11" fill="currentColor">1969</text>
                  <text x="155" y="128" text-anchor="middle" font-size="11" fill="#22c55e">ARPANET</text>
                  <circle cx="255" cy="100" r="5" fill="#f97316"/>
                  <line x1="255" y1="95" x2="255" y2="60" stroke="currentColor" stroke-width="1"/>
                  <text x="255" y="50" text-anchor="middle" font-size="11" fill="currentColor">1991</text>
                  <text x="255" y="128" text-anchor="middle" font-size="11" fill="#f97316">WWW</text>
                  <circle cx="340" cy="100" r="5" fill="#dc2626"/>
                  <line x1="340" y1="95" x2="340" y2="60" stroke="currentColor" stroke-width="1"/>
                  <text x="340" y="50" text-anchor="middle" font-size="11" fill="currentColor">2007</text>
                  <text x="340" y="128" text-anchor="middle" font-size="11" fill="#dc2626">iPhone</text>
                </svg>
                """
        ),
        new(
            Heading: "State machines",
            Front: "Draw the state diagram of a 3-color traffic light.",
            Back: "Three states cycle: **Red → Green → Yellow → Red**.\n\nThe transitions are time-based (each state has a fixed duration). A pedestrian button can shorten the time to the next Red.",
            FrontSvg: null,
            BackSvg: """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 250" width="400" height="250">
                  <circle cx="100" cy="125" r="32" fill="#dc2626" stroke="currentColor" stroke-width="2"/>
                  <text x="100" y="131" text-anchor="middle" font-size="13" fill="white">RED</text>
                  <circle cx="300" cy="60" r="32" fill="#22c55e" stroke="currentColor" stroke-width="2"/>
                  <text x="300" y="66" text-anchor="middle" font-size="13" fill="white">GREEN</text>
                  <circle cx="300" cy="190" r="32" fill="#eab308" stroke="currentColor" stroke-width="2"/>
                  <text x="300" y="196" text-anchor="middle" font-size="13" fill="white">YELLOW</text>
                  <path d="M 128 110 Q 200 50 268 60" fill="none" stroke="currentColor" stroke-width="1.5" marker-end="url(#c)"/>
                  <path d="M 300 92 L 300 158" fill="none" stroke="currentColor" stroke-width="1.5" marker-end="url(#c)"/>
                  <path d="M 268 200 Q 200 220 128 145" fill="none" stroke="currentColor" stroke-width="1.5" marker-end="url(#c)"/>
                  <defs><marker id="c" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor"/></marker></defs>
                </svg>
                """
        ),
    };
}
