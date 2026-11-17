# Cleanup löscht Lieferverzeichnisse, die die Datenbank nicht kennt (#947)

Review: required. Es ändert, wann geopilot gelieferte Daten löscht, und die Reihenfolge der Deklaration. Ein Fehler darin kostet Lieferungen, das zähle ich zu sicherheitsrelevant (Integrität der Lieferungen). Keine Migration, keine neue Einstellung.

## Ziel

Gelieferte Daten verschwinden nicht, weil die Datenbank sie nicht kennt. Heute löscht das erste Cleanup nach dem Restore einer älteren Datenbank, also beim Start, die Lieferverzeichnisse (`Storage:AssetsDirectory/<jobId>`) aller Lieferungen, die nach der Sicherung deklariert wurden, mit einem Trace-Eintrag pro Verzeichnis: `ProcessingJobCleanupService` hält jedes Verzeichnis eines Jobs, den sein Speicher nicht kennt und der keine Lieferung hat, für verwaist.

Den Teil des Issues zur Single-Instance-Annahme und seine Akzeptanzkriterien liefert #954. Dessen Spec stellt den Upload-Sweep auf die Upload-Sessions in der Datenbank und das Job-Cleanup auf Lauf und Heartbeat. Der rein altersbasierte Weg aus dem Issue, gedacht als Sofortmassnahme, falls #954 sich verzögert, entfällt: Beide Tickets liegen in derselben Etappe. Diese Spec deckt, was keiner der beiden Wege löst, den Restore aus dem Architektur-Review 2026-11 (Befund 3.1b).

Akzeptanzkriterien:

- Das Cleanup löscht ein Lieferverzeichnis nur, wenn die Datenbank die Lieferung seines Jobs als gelöscht führt.
- Ein Lieferverzeichnis entsteht erst, wenn seine Lieferung gespeichert ist.
- Lieferverzeichnisse ohne Lieferung und Lieferungen ohne Lieferverzeichnis meldet jeder Cleanup-Lauf als Warnung.
- Nach dem Restore einer älteren Datenbank sind alle Lieferverzeichnisse noch da.

## Umsetzung

Grundlage ist eine Regel: Ein Lieferverzeichnis gehört zu einer Lieferung in der Datenbank. Was ihr nicht folgt, löscht geopilot nicht, es meldet es.

**Deklaration: erst speichern, dann umbenennen.** Heute benennt die Deklaration das Staging-Verzeichnis zuerst in das Lieferverzeichnis um, hasht die Dateien dort und speichert danach die Lieferung (#997). Neu hasht sie im Staging, speichert die Lieferung und benennt erst dann um. So entsteht ein Lieferverzeichnis nur mit seiner Lieferung:

- Scheitert die Deklaration vor dem Speichern, bleibt alles im Staging. Es verfällt mit dem Job, und ein zweiter Versuch findet es dort.
- Deklarieren zwei Aufrufer denselben Job gleichzeitig, benennt nur der um, dessen Lieferung gespeichert wurde. Der andere bekommt `AlreadyDeclared` und rührt keine Datei an. Liest er noch, während der erste umbenennt, scheitert entweder sein Lesen oder das Umbenennen; das Umbenennen holt dann das Cleanup nach.
- Kann die Deklaration nach dem Speichern nicht umbenennen (Absturz, Dateifehler, ein Commit, der trotz Ausnahme durchging), liegt die Lieferung im Staging, bis das Cleanup sie an ihren Platz schiebt.
- Hinterlässt das Löschen einer Lieferung Dateien, lässt sich ihr Job daraus nicht neu deklarieren, weil nichts mehr im Staging bereitsteht. Heute hasht ein solcher Versuch die Reste am Platz.

Ein Zurückbenennen braucht es damit nicht. Es wurde verworfen, weil ein gescheiterter Aufrufer die Dateien einer Lieferung, die ein anderer gerade gespeichert hatte, ins Staging zurückschieben und so dem Cleanup ausliefern könnte.

**Cleanup.** Über das Liefer- und das Staging-Verzeichnis eines Jobs entscheiden in `ProcessingJobCleanupService` nur die Lieferungen dieses Jobs, gelöschte eingeschlossen (`IgnoreQueryFilters`):

| Lieferungen des Jobs | Lieferverzeichnis | Staging-Verzeichnis |
| --- | --- | --- |
| eine aktive | bleibt | kommt an seinen Platz; liegt dort schon ein Verzeichnis, bleibt es und wird gemeldet |
| nur gelöschte | geht: Rückfallebene, falls `DeliveryController` die Dateien beim Löschen nicht wegräumen konnte | geht mit dem Job wie heute |
| keine | bleibt und wird gemeldet | geht mit dem Job wie heute |

Ein Staging-Verzeichnis mit aktiver Lieferung schiebt jeder Lauf an seinen Platz, auch für einen Job, den der Speicher noch kennt; treffen Cleanup und Deklaration dabei aufeinander, scheitert eines der beiden Umbenennen ohne Folgen. Alles andere entscheidet das Cleanup wie heute erst, wenn sein Speicher den Job nicht kennt oder `Processing:JobRetention` abgelaufen ist. Die übrigen Verzeichnisse eines Jobs (Downloads, Visualisierungen, Arbeitsverzeichnis) räumt es weiter ab wie heute.

**Meldung.** Jeder Lauf, der erste beim Start, schreibt nach dem Verschieben eine Warnung je Richtung, wenn es Abweichungen gibt: Lieferverzeichnisse ohne Lieferung, und aktive Lieferungen, deren Verzeichnis fehlt (die Platte ist älter als die Datenbank). Die Warnung nennt die Zahl und die ersten Job-Ids, gekürzt, damit ein Restore mit tausenden Abweichungen das Log nicht flutet. Eine Abweichung wird gemeldet, bis ein Betreiber sie behebt; was mit den Dateien geschieht, entscheidet er. Weil ein Lieferverzeichnis erst nach dem Speichern entsteht, meldet eine laufende Deklaration nichts.

**#954.** Dessen Spec ersetzt im Cleanup den Speicher durch die Datenbank. Für Liefer- und Staging-Verzeichnisse gilt dort die Tabelle oben, nicht die Regel, dass ein Verzeichnis ohne Lauf verwaist ist; seine Spec sagt das.

**Doku.**

- README, Betrieb: Lieferverzeichnisse löscht geopilot nur für gelöschte Lieferungen. Was die beiden Warnungen bedeuten und dass nach einem Restore der Abgleich von Hand geschieht.
- CHANGELOG unter Fixed: Nach dem Restore einer älteren Datenbank löscht geopilot die Dateien später deklarierter Lieferungen nicht mehr, sondern meldet sie, ebenso Lieferungen, deren Dateien fehlen.

## Nicht im Umfang

- Die Cleanup-Regeln für mehrere Instanzen und nach einem Neustart: #954.
- Uploads, Downloads, Visualisierungen und Arbeitsverzeichnisse nach einem Restore, ebenso das Staging eines Jobs ohne Lieferung. Sie sind flüchtig, und ihren Job kennt nach dem Restore niemand mehr.
- Ein Werkzeug, das Verzeichnisse und Lieferungen nach einem Restore wieder zusammenführt.
- Eine Frist, nach der gemeldete Verzeichnisse doch verschwinden: Ein Restore, den niemand rechtzeitig bemerkt, kostete sonst die Lieferungen trotzdem.

## Prüfung

- Cleanup als Tabellentest: Liefer- und Staging-Verzeichnis mit aktiver, nur gelöschter und ohne Lieferung, je für einen Job, den der Speicher kennt, und einen, den er nicht kennt.
- Ein Verzeichnis ohne Lieferung erzeugt eine Warnung mit seiner Job-Id, eine aktive Lieferung ohne Verzeichnis ebenso, eine Lieferung im Staging nicht.
- Deklaration: Wirft das Hashen oder das Speichern, liegt alles weiter im Staging, kein Lieferverzeichnis entsteht, und ein zweiter Versuch liefert. Bei zwei gleichzeitigen Deklarationen benennt nur die gespeicherte um. Scheitert das Umbenennen nach dem Speichern, schiebt der nächste Cleanup-Lauf das Staging an seinen Platz.
- Smoke-Test: zwei Lieferungen deklarieren, die Datenbank auf einen Stand mit nur der ersten zurücksetzen, die Applikation starten. Beide Lieferverzeichnisse sind noch da, und die Warnung nennt den Job der zweiten.
