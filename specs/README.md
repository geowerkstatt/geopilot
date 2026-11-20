# Specs

Für jedes Ticket liegt hier vor der Umsetzung eine Spec, die sagt, wie es umgesetzt wird. Die Spec wird in einem eigenen PR reviewt, damit manuelles Review dort landet, wo es etwas bringt; dabei entscheidet sich auch, ob der Umsetzungs-PR ein manuelles Review braucht. PRs ohne Ticket, etwa von Dependabot, laufen wie bisher.

Das Issue sagt, welches Problem gelöst wird, die Spec, wie. Was im Issue steht, wiederholt die Spec nicht.

## Ablauf

1. Der Autor bringt das Issue auf den Stand, den das Review braucht (siehe *Issue*), und schreibt die Spec auf einem eigenen Branch. Ob allein mit KI oder zusammen mit einem Teammitglied, überlassen wir seiner Einschätzung; eine aufwändige Spec zu zweit zu schreiben, kann sich lohnen. Auf dem Issue setzt er die Labels, die er für die Umsetzung vorschlägt (siehe *Review der Umsetzung*). Der PR braucht keine Beschreibung: Was das Review braucht, steht im Issue und in der Spec.
2. Ein Teammitglied reviewt die Spec zusammen mit dem Issue und passt die Labels an, wo es anders entscheidet. Versteht es das Problem aus dem Issue nicht, geht zuerst das Issue an den Autor zurück. Mit dem Merge ist das Ticket Ready.
3. Die Umsetzung zweigt von dem Branch ab, in den die Spec gemergt wurde, und geht von Issue, Parent-Issue und Spec aus, auch wenn eine KI umsetzt. Ihr PR verlinkt den Spec-PR und löscht die Spec; braucht ein Ticket mehrere PRs, löscht sie der letzte.
4. Hat das Issue `code review`, wird der Umsetzungs-PR wie bisher reviewt. Sonst prüfen Autor und KI die Umsetzung gegen die Spec, etwa mit `geow:review-branch`, der PR sagt, was mit den Befunden geschehen ist, und bekommt das Label `author reviewed`. Ein anderes Teammitglied approved dann, ohne zu reviewen. Hat das Issue `UI review`, probiert, wer reviewt oder approved, die Änderung im GUI kurz aus.

Weicht die Umsetzung von der Spec ab, sagt der PR das. Trifft dabei ein Kriterium für `code review` zu, wird er reviewt, auch wenn das Issue das Label nicht hat.

Gelöscht wird die Spec, damit `specs/` nur zeigt, was spezifiziert und noch nicht umgesetzt ist. Eine umgesetzte Spec bliebe sonst als veralteter Stand liegen, den Mensch und KI für aktuell halten. Über ihren PR und die Git-Geschichte bleibt sie auffindbar. Wird ein Ticket ohne Umsetzung geschlossen, wird seine Spec ebenfalls gelöscht.

Beiträge von ausserhalb des Teams laufen nicht über diesen Ablauf und werden immer reviewt.

## Issue

Zuoberst steht das Problem in wenigen einfachen Sätzen, von Hand geschrieben und ohne Kenntnis des Codes verständlich; bei einem Feature ist das die User Story. Darunter folgen Akzeptanzkriterien und Abgrenzung. Gehört das Issue zu einem grösseren Vorhaben, hängt es als Sub-Issue unter dessen Issue, das den Kontext liefert. Was vorher erledigt sein muss, steht als Beziehung «Blocked by» am Issue, nicht im Text.

Ändert ein Kommentar den Umfang, wird der Text des Issues nachgeführt, bevor die Spec ins Review geht. Bleibt vom ursprünglichen Problem nichts übrig, ersetzt ein neues Issue das alte.

## Review der Umsetzung

Zwei Labels auf dem Issue sagen, wie der Umsetzungs-PR geprüft wird. Sie lassen sich kombinieren.

`code review`, sobald eines zutrifft:

- Kunden konfigurieren es oder bauen darauf auf: Neues in der Pipeline-Definition, brechende Änderungen an API, PipelineCore oder Betreiberdoku, Migrationen, die bestehende Daten anfassen. Eine Migration, die nur Spalten oder Tabellen hinzufügt, zählt nicht dazu.
- Es bringt ein neues architektonisches Konzept.
- Es ist sicherheitsrelevant, etwa Authentifizierung, Autorisierung, anonym erreichbare Endpunkte oder Uploads.
- Es kommt eine Abhängigkeit dazu.
- Es ändert CI/CD oder Infrastruktur.

Ohne `code review` bleiben additive, leicht umkehrbare Änderungen innerhalb bestehender Konzepte, zum Beispiel ein neues Attribut in einer API-Antwort, das das GUI anzeigt.

`UI review`, wenn die Umsetzung das GUI ändert.

Nicht jeder Fall ist eindeutig. Wer die Spec reviewt, entscheidet; war ein Fall unklar, schärfen wir diese Liste nach. Ältere Specs nennen die Entscheidung noch in der Zeile `Review:`, dort entspricht `required` dem Label `code review` und `waived` keinem Label.

## Spec schreiben

Dateiname `specs/<Issue>-<kurzname>.md`, zum Beispiel `specs/1195-protokoll.md`.

Die Spec sagt, wie das Ticket umgesetzt wird und woran sich zeigt, dass es funktioniert. Problem, Akzeptanzkriterien, Abgrenzung und Abhängigkeiten stehen im Issue. Ausser dem eigenen nennt die Spec keine Issues und PRs; was die Umsetzung von dort braucht, sagt sie selbst. Entscheidet sie etwas, das den Umfang ändert, wird das Issue nachgeführt.

Die Spec bleibt kurz und enthält nur so viel, wie das Review braucht. Mehr als etwa 1000 Wörter lassen sich schlecht in einem Zug reviewen; braucht sie mehr, wird das Ticket geteilt. Abschnitte ohne Inhalt fallen weg, eine Spec ohne `code review` hat oft keine zehn Zeilen. Ausführliche Umsetzungspläne einer KI gehören nicht hierher.

Die Spec verwendet die Begriffe aus [`GLOSSARY.md`](../GLOSSARY.md), und die KI prüft sie beim Schreiben dagegen, etwa mit dem Skill `domain-modeling` aus [mattpocock/skills](https://github.com/mattpocock/skills). Führt die Spec einen Begriff ein oder ändert sie einen, erklärt sie ihn unter *Begriffe*, und der Umsetzungs-PR übernimmt ihn ins Glossar.

Das Repo ist öffentlich. Für Specs gelten dieselben Regeln wie für Issues: keine Namen von Kunden oder Personen. Liegt ein Ticket nicht hier, etwa weil es eine Sicherheitslücke beschreibt, liegt seine Spec im selben Repo wie das Ticket.

```markdown
# <Titel des Issues> (#<Issue>)

## Begriffe

<Nur Begriffe, die neu sind oder sich ändern.>

## Umsetzung

<Entscheide: Ansatz, betroffene Stellen und Konfiguration, Reihenfolge innerhalb des Tickets.>

## Prüfung

<Woran sich zeigt, dass es funktioniert: Tests und Smoke-Test zu den Akzeptanzkriterien des Issues.>
```
