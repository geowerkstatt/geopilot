# Filter in der Lieferungsübersicht

Zu diesem Ticket gibt es kein Issue, deshalb nennt die Spec Problem, Akzeptanzkriterien und Abgrenzung selbst.

## Problem

Die Lieferungsübersicht der Administration lädt alle Lieferungen auf einmal. Filtern lässt sie sich nur über das Spaltenmenü des Grids, eine Spalte aufs Mal und ohne dass die Adresse den Filter behält. Bei bis zu 300 Lieferungen pro Woche wird die Liste schnell lang und langsam, und Lieferungen, deren Weitergabe oder Benachrichtigungen gescheitert sind, lassen sich nicht gezielt finden.

**User Story.** Als Admin finde ich die Lieferungen, um die ich mich kümmern muss, etwa die gescheiterten Weitergaben eines Mandats im letzten Monat, indem ich Filter kombiniere wie in der Liste der Protokolle.

**Akzeptanzkriterien.**

- Die Lieferungsübersicht filtert nach Zeitraum, Lieferant, Mandat und danach, ob die Weitergabe oder die Benachrichtigungen gescheitert sind. Alle Filter ausser dem Zeitraum nehmen mehrere Werte.
- Das Suchfeld grenzt die Liste auf die Lieferungen ein, deren ID mit den eingegebenen Ziffern beginnt. Es öffnet keine Lieferung.
- Filter, Suche und Seite stehen in der Adresse. Wurde eine Lieferung aus der gefilterten Liste geöffnet, führen «Zurück zu den Lieferungen» und das Löschen der Lieferung in dieselbe gefilterte Liste zurück. Auf jedem anderen Weg erscheint die Liste ungefiltert. Die Liste der Protokolle verhält sich weiterhin genauso.
- Die Liste lädt seitenweise vom Server, die neuesten Lieferungen zuerst.

**Nicht im Umfang.** *Meine Lieferungen* bleibt, wie sie ist. Kein Sortieren nach Spalten. Kein Filter nach Kommentar oder Lieferangaben. Kein Filter nach `Pending`, `Running` oder `Done`: Diese Zustände ändern sich laufend, und eine danach gefilterte Liste wäre schon veraltet, wenn man eine Lieferung daraus öffnet. Ebenso kein Filter nach Reaktionen, die hängen oder noch einmal versuchen, und keiner nach Reaktionen, die einmal gescheitert sind und inzwischen zurückgesetzt wurden. Die Liste der Protokolle bekommt keine neuen Filter.

## Umsetzung

Ein PR, mit `code review` (neues npm-Paket, brechende API-Änderung) und `UI review`.

**API.** `GET api/v1/delivery` nimmt wie `GET api/v1/recording` eine Abfrage und liefert eine Seite: `DeliveryQuery` mit `From`, `To`, `MandateIds`, `UserIds`, `MachineClientIds`, `FailedReactionKinds`, `IdPrefix`, `Page` und `PageSize`, als Antwort `DeliveryPageResponse(TotalCount, Items)` mit den bisherigen `DeliveryResponse`. Jeder gesetzte Filter grenzt ein, ein Filter mit mehreren Werten trifft jeden davon. `UserIds` und `MachineClientIds` bilden zusammen den Filter nach Lieferant. `From` gilt inklusive, `To` exklusiv, beide auf `Delivery.Date`. `IdPrefix` vergleicht die ID als Text: `12` trifft 12, 120 und 1234, nicht 312. Sortiert wird fest nach `Date`, dann `Id`, beide absteigend. Grenzen wie bei `RecordingQuery`: höchstens 100 Werte pro Filter, `Page` 0 bis 1'000'000, `PageSize` 1 bis 100, und ein Zeitraum, der vor seinem Beginn endet, gibt `400`. `IdPrefix` besteht aus 1 bis 9 Ziffern, sonst ebenfalls `400`.

**Gescheitert.** `FailedReactionKinds` nimmt `Transfer` und `Notification` (`ReactionKind`) und lässt nur Lieferungen durch, bei denen mindestens eine Reaktion einer gewählten Art auf `Error` steht; mit beiden Werten genügt eine gescheiterte Weitergabe oder eine gescheiterte Benachrichtigung. Nur nach `Error` wird gefiltert, weil dieser Zustand bleibt, bis ein Admin die Reaktion zurücksetzt: Eine gefilterte Liste veraltet also nicht von selbst. Weil `Error` in `Precedence` vorne steht, trifft der Filter genau die Lieferungen, deren Spalte der gewählten Art `Error` zeigt; die Regel muss dafür nicht ins Query. Die Spalten fasst wie bisher `SummedUp` im Speicher zusammen, nur für die Lieferungen der Seite. `ReactionExtensions` bleibt, wie es ist.

**Adresse mit nuqs.** Das Paket `nuqs` kommt dazu, mindestens 2.7 wegen `parseAsNativeArrayOf`. `NuqsAdapter` aus `nuqs/adapters/react-router/v7` umschliesst in `app.tsx` die Routen innerhalb von `BrowserRouter`; `react-router` 7.18.2 ist über `react-router-dom` schon installiert. Beide Listen lesen und schreiben Filter, Suche und Seite mit `useQueryStates`. Filter mit mehreren Werten stehen als wiederholte Schlüssel in der Adresse (`parseAsNativeArrayOf`), wie die Protokolle sie heute schreiben. Änderungen ersetzen den Eintrag im Verlauf, statt einen neuen anzulegen. Werte, die die API nicht nimmt, setzen wie heute keinen Filter; wo ein eingebauter Parser das nicht sicherstellt, prüft ein eigener (`createParser`). Ändert sich ein Filter oder die Suche, springt die Liste auf die erste Seite.

**Liste der Protokolle.** `recordingQuery.ts` gibt das Lesen und Schreiben der Adresse an die Parser von nuqs ab. Schlüssel (`from`, `to`, `startedBy`, `mandate`, `state`, `page`) und Verhalten bleiben, bestehende Links funktionieren weiter. `toRecordingQuery` bleibt. `listQueryState` und `listQueryOf` ziehen an eine gemeinsame Stelle, die beide Detailseiten nutzen.

**Lieferungsübersicht.** `deliveries.tsx` wird eine eigene Liste nach dem Muster von `recordings.tsx`: Seiten vom Server, deren Grösse die Höhe des Grids bestimmt (höchstens 100), kein Sortieren und kein Spaltenfilter im Grid, Spalten wie heute. Darüber steht `DeliveryFilters` auf `FilterBar`, mit fünf Feldern in einer Reihe (`columns={5}`), auf schmalen Bildschirmen eingeklappt:

- Zeitraum von und bis, mit der letzten Minute inklusive wie bei den Protokollen
- Geliefert von: Personen, Maschinen-Clients nur mit maschineller Lieferung
- Mandat
- Fehlgeschlagene Reaktionen (`failedReactions`), mit Weitergabe (`forwarding`) und Benachrichtigungen (`notifications`), den Namen der Spalten

Personen, Maschinen-Clients und Mandate lädt ein Hook, den beide Filterleisten nutzen. Die Schlüssel in der Adresse sind `from`, `to`, `deliveredBy` (`user:<id>` oder `machineClient:<id>`), `mandate`, `failed` (`transfer` oder `notification`), `id` und `page`. Die Anordnung der Felder darf sich in der Umsetzung noch ändern.

**Suche.** Das Suchfeld zeigt den Text, wie er getippt wurde. In die Adresse (`id`) kommt er nur, wenn er aus 1 bis 9 Ziffern besteht; ein leeres Feld entfernt `id`, anderer Text lässt die Liste, wie sie ist. Jede Änderung lädt neu, ohne Debounce; eine Antwort auf eine ältere Abfrage wird wie bei den Protokollen verworfen. «Filter zurücksetzen» leert auch die Suche. Neu sind nur der Platzhalter des Suchfelds (`deliveryIdSearch`) und die Beschriftung des Filters Fehlgeschlagene Reaktionen (`failedReactions`), in allen vier Sprachen.

**Detailseite.** Die Liste gibt beim Öffnen einer Lieferung ihre Adresse mit (`listQueryState`). «Zurück zu den Lieferungen» und das Löschen führen in diese Liste zurück, ohne mitgegebene Adresse in die ungefilterte.

**Meine Lieferungen.** `DeliveryGrid` dient nur noch dieser Liste und geht deshalb in `userDeliveries.tsx` auf, ohne `rowAction`, die Spalten der Administration und den Zweig für `DeliveryResponse`.

**Doku.** `docs/Reaktionen.md`: Die Lieferungsübersicht findet die Lieferungen mit gescheiterten Weitergaben oder Benachrichtigungen, und `GET api/v1/delivery` nimmt die Filter und liefert Seiten. CHANGELOG: die Filter unter Added, die geänderte Antwort von `GET api/v1/delivery` unter Changed.

## Prüfung

- `Geopilot.Api.Test`: jeder Filter allein und kombiniert; Lieferant als Person und als Maschinen-Client; Zeitraum mit Beginn inklusive und Ende exklusiv; `IdPrefix` `12` trifft 12, 120 und 1234, nicht 312; Seiten mit `TotalCount`, die neuesten zuerst; `400` für einen Zeitraum, der vor seinem Beginn endet, für `PageSize` 0 und 101, für 101 Werte und für `IdPrefix` `1a`; gelöschte Lieferungen erscheinen nie. Gescheitert: Weitergabe allein, Benachrichtigung allein und beide; eine gescheiterte Benachrichtigung trifft den Filter Weitergabe nicht und umgekehrt; eine Lieferung, deren Reaktionen nur `Pending`, `Running` oder `Done` sind oder die keine hat, trifft nie. Für jede Kombination von `Pending`, `Running`, `Error` und `Done`, auch ganz ohne Reaktion, legt ein Test eine Lieferung an und prüft, dass sie der Filter genau dann trifft, wenn ihre Spalte `Error` zeigt.
- Cypress: `adminDeliveries.cy.ts` und `adminDeliveryDetail.cy.ts` mit der Seite als Antwort. Filter und Suche landen in Adresse und Abfrage; eine Adresse mit unlesbaren Werten setzt für diese keinen Filter; zurück von der Detailseite und nach dem Löschen stehen die Filter wieder; über das Menü erscheint die Liste ungefiltert; Text, der nicht nur aus Ziffern besteht, lässt die Liste stehen. `recordings.cy.ts` bleibt grün, die Tests aus `recordingQuery.cy.ts` prüfen nun die Parser, und eine bestehende Adresse mit wiederholten Schlüsseln liest sich wie bisher.
- Von Auge im Dev-Stack: nach gescheiterter Weitergabe und einem Mandat filtern, eine Lieferung öffnen, zurück: dieselbe Liste. Im schmalen Fenster sind die Felder eingeklappt.
- CI ist grün, auch der Abgleich der generierten Typen (`npm run openapi`).
