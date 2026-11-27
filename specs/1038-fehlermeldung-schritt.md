# Fehlermeldung bei unerwarteten Fehlern eines Pipeline-Steps (#1038)

## Begriffe

- **Fehlerhinweis** (neu): Ein fester Text für den Lieferanten bei einem Schritt, der scheitert, ohne zu sagen, warum. Er sagt, dass der Schritt gescheitert ist, und nennt die Id der Verarbeitung als Referenz für den Support. Der Lieferant sieht die Ursache nicht, sie steht nur im Protokoll. Das Protokoll liest der Support, also wer die Installation betreibt: Im Admin-Portal öffnet die Id das Protokoll direkt. Im Code: `FailureMessage`. Vermeiden: Fehlermeldung, so heisst im Code der Text der Exception (`ErrorMessage`), und der steht nur im Protokoll.

## Umsetzung

**Wann.** Ein Schritt bekommt einen Fehlerhinweis, wenn er mit `Error` endet und keine Bedingung ihm eine Meldung gegeben hat (`ConditionMessage`). Das sind zwei Fälle:

- Der Schritt hat geworfen.
- Eine Bedingung aus `fail_conditions` hat gegriffen, hat aber keine `message`.

Im zweiten Fall erfährt der Lieferant sonst ebenso wenig, warum der Schritt gescheitert ist. Im Protokoll steht dann, welche Bedingung gegriffen hat. Den Text der Exception braucht die Entscheidung nicht.

**Feld.** `StepResultResponse` bekommt das Feld `FailureMessage` (`LocalizedText?`), in JSON `failureMessage`. Es trägt den Fehlerhinweis und ist sonst `null`. Die Beschreibung in Swagger sagt, dass das Feld nur diesen festen Text trägt und nie den Text einer Exception.

Der Text des Fehlerhinweises ist ein fester `LocalizedText` in `DtoMapperExtensions` neben `PreflightFailedMessageTemplate`, auf Deutsch, Englisch, Französisch und Italienisch:

> Bei diesem Schritt ist ein Fehler aufgetreten. Bitte wenden Sie sich an den Support. Referenz für den Support: {jobId}.

Er spricht weder von einem unerwarteten Fehler noch von einem weiteren Versuch. Bei einer Bedingung ohne `message` scheitert auch der nächste Versuch gleich.

Die Abbildung eines Schritts auf `StepResultResponse` setzt das Feld. Wenn die Statusabfrage bei der Umsetzung schon aus dem Protokoll liest, gilt dasselbe für die Abbildung von `RecordingStep`, denn dort stehen `State` und `ConditionMessage` ebenso.

**Preflight.** Ein gescheiterter Preflight behält seinen Text in `StatusMessage` und bekommt keinen Fehlerhinweis, denn sein Text nennt die Id schon. Ihn nach `FailureMessage` zu verschieben, wäre eine brechende Änderung der API: `statusMessage` des Preflights wäre in der anonymen Statusabfrage plötzlich leer. Der Wizard zeigt ihn deshalb weiterhin als Text und nicht als rote `Alert`.

**Wizard.** In `deliveryProcessing.tsx` zählt `failureMessage` für `stepHasContent`. Damit lässt sich der Schritt aufklappen, und er klappt beim Ende von selbst auf wie die anderen. Der Fehlerhinweis steht als rote `Alert` zuoberst im Schritt, vor einer allfälligen Statusmeldung. Der Knoten der Verarbeitung im Stepper zeigt weiterhin «Fehlgeschlagen».

**Statusdokument.** `SubmissionController.BuildResponse` nimmt `FailureMessage` wie `StatusMessage` und `ConditionMessage` in `messages` auf. Das Gewicht kommt wie dort aus dem Zustand des Schritts, also `error`.

**Doku.**

- `docs/MaschinelleLieferung.md` unter «Status abfragen», beim Absatz zu `messages`: Wenn ein Schritt ohne Meldung scheitert, steht dort sein Fehlerhinweis mit `error`. Endet auch der nächste Versuch mit `failed`, wendet sich der Client mit der Id an den Support.
- `docs/MaschinelleLieferung.md` unter «Hinweis für Pipeline-Definitionen» und `docs/pipeline/Pipelines.md` bei `message`: Ohne `message` sieht der Lieferant bei einer Bedingung aus `fail_conditions` nur einen festen Hinweis mit der Referenz für den Support, nicht den Grund.
- CHANGELOG unter Fixed: Wenn ein Schritt ohne Meldung der Pipeline scheitert, zeigt der Wizard dort einen Hinweis mit der Referenz für den Support, und der Schritt lässt sich aufklappen. Die Statusabfrage liefert den Hinweis im neuen Feld `failureMessage` eines Schritts, das Statusdokument einer maschinellen Lieferung in `messages`.

## Prüfung

- `DtoMapperExtensionsTest` als Tabellentest:
  - Ein Schritt auf `Error` ohne Meldung einer Bedingung bekommt den Fehlerhinweis in vier Sprachen mit der Id, nach einem Wurf wie nach einer Bedingung aus `fail_conditions` ohne `message`.
  - Mit Meldung einer Bedingung und in allen anderen Zuständen bekommt er keinen.
  - Ein Schritt mit Statusmeldung behält sie neben dem Fehlerhinweis.
  - Ein gescheiterter Preflight trägt seinen Text weiter in `StatusMessage` und hat keinen Fehlerhinweis.
  - Wenn die Statusabfrage aus dem Protokoll liest, gilt dasselbe für `RecordingStep`.
- `ProcessingControllerTest` und `SubmissionControllerTest`: Ein Schritt steht auf `Error`, und seine `ErrorMessage` enthält einen Serverpfad. Die serialisierte Antwort enthält die Id, aber den Pfad nicht. Im Statusdokument steht der Fehlerhinweis mit `error` beim Schritt.
- Cypress mit Intercept wie in `delivery.cy.ts`: Ein Schritt auf `Error` mit `failureMessage` klappt von selbst auf und zeigt den Text.
- Smoke-Test: Den Container `ilitools-wrapper` stoppen und im Wizard eine XTF-Datei mit der Pipeline aus `basicPipeline_01.yaml` prüfen. Der Validierungsschritt klappt auf und zeigt den Fehlerhinweis mit der Id. Unter dieser Id zeigt das Protokoll im Admin-Portal die Exception. Als maschinelle Lieferung enthält `messages` denselben Text mit `error`.
