# Specs

Für jedes Ticket liegt hier vor der Umsetzung eine Spec: wie das Ticket umgesetzt wird und ob der Umsetzungs-PR ein manuelles Review braucht. Die Spec wird in einem eigenen PR reviewt, damit manuelles Review dort landet, wo es etwas bringt. PRs ohne Ticket, etwa von Dependabot, laufen wie bisher.

## Ablauf

1. Der Autor schreibt die Spec, allein oder mit KI, auf einem eigenen Branch ab `main` und eröffnet dafür einen PR.
2. Ein Teammitglied reviewt die Spec und beurteilt dabei die Review-Klasse. Mit dem Merge ist das Ticket Ready.
3. Die Umsetzung zweigt von `main` ab. Ihr PR verlinkt den Spec-PR und löscht die Spec; braucht ein Ticket mehrere PRs, löscht sie der letzte.
4. Bei `Review: required` wird der Umsetzungs-PR wie bisher reviewt. Bei `Review: waived` prüfen Autor und KI die Umsetzung gegen die Spec, etwa mit `geow:review-branch`, eine Änderung am GUI auch von Auge, und der PR sagt, was mit den Befunden geschehen ist. Ein anderes Teammitglied approved dann, ohne zu reviewen.

Weicht die Umsetzung von der Spec ab, sagt der PR das. Trifft dabei ein Kriterium für `required` zu, wird er reviewt, auch wenn die Spec `waived` sagt.

Gelöscht wird die Spec, damit `specs/` nur zeigt, was spezifiziert und noch nicht umgesetzt ist. Eine umgesetzte Spec bliebe sonst als veralteter Stand liegen, den Mensch und KI für aktuell halten. Über ihren PR und die Git-Geschichte bleibt sie auffindbar. Wird ein Ticket ohne Umsetzung geschlossen, wird seine Spec ebenfalls gelöscht.

Beiträge von ausserhalb des Teams laufen nicht über diesen Ablauf und werden immer reviewt.

## Review-Klasse

`required`, sobald eines zutrifft:

- Kunden konfigurieren es oder bauen darauf auf: Neues in der Pipeline-Definition, brechende Änderungen an API, PipelineCore oder Betreiberdoku, Migrationen, die bestehende Daten anfassen. Eine Migration, die nur Spalten oder Tabellen hinzufügt, zählt nicht dazu.
- Es bringt ein neues architektonisches Konzept.
- Es ist sicherheitsrelevant, etwa Authentifizierung, Autorisierung, anonym erreichbare Endpunkte oder Uploads.
- Es kommt eine Abhängigkeit dazu.
- Es ändert CI/CD oder Infrastruktur.

`waived` für additive, leicht umkehrbare Änderungen innerhalb bestehender Konzepte, zum Beispiel ein neues Attribut in einer API-Antwort, das das GUI anzeigt.

Nicht jeder Fall ist eindeutig. Der Autor begründet seine Wahl in der Spec, wer sie reviewt, entscheidet. War ein Fall unklar, schärfen wir diese Liste nach.

## Spec schreiben

Dateiname `specs/<Issue>-<kurzname>.md`, zum Beispiel `specs/1195-verarbeitung.md`.

Die Spec ergänzt das Issue, sie wiederholt es nicht: Problem, Lösung und Akzeptanzkriterien stehen dort. Hierher gehört, was das Issue offen lässt, und nur so viel, wie das Review braucht. Weicht die Spec vom Issue ab, sagt sie das. Abschnitte ohne Inhalt fallen weg, eine Spec mit `waived` hat oft keine zehn Zeilen. Ausführliche Umsetzungspläne einer KI gehören nicht hierher.

Das Repo ist öffentlich. Für Specs gelten dieselben Regeln wie für Issues: keine Namen von Kunden oder Personen.

```markdown
# <Titel des Issues> (#<Issue>)

Review: waived. <Welches Kriterium zutrifft, oder warum keines.>

## Umsetzung

<Entscheide, die das Issue offen lässt: Ansatz, betroffene Stellen, Reihenfolge.>

## Nicht im Umfang

<Was jemand erwarten könnte, aber nicht kommt.>

## Prüfung

<Woran sich zeigt, dass es funktioniert: Tests, Smoke-Test, Akzeptanzkriterien des Issues.>
```
