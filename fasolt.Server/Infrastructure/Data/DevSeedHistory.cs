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
}
