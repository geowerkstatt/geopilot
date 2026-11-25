# Person oder Maschinen-Client auch in «Meine Lieferungen» und nach dem Ausschalten erkennen (#1260)

## Umsetzung

Ob die Oberfläche eine Person von einem Maschinen-Client unterscheidet, hängt nicht mehr an `MachineDelivery:Enabled`, sondern daran, ob ein Maschinen-Client registriert ist. Maschinen-Clients lassen sich nicht löschen, also bleibt das wahr, wenn die maschinelle Lieferung später ausgeschaltet wird.

API, beide Änderungen additiv:

- `CapabilitiesResponse` bekommt `HasMachineClients`: ob mindestens ein Maschinen-Client registriert ist, aktiv oder inaktiv, unabhängig von `MachineDelivery:Enabled`. `CapabilitiesController` liest es mit `MachineClients.AnyAsync()`. Die Doc-Kommentare von Controller und Antwort sagen heute, Capabilities seien konfiguriert; sie nennen neu diesen einen Wert, der aus den Daten kommt.
- `UploadedDelivery` bekommt `DeliveredByClientId` wie `DeliveryResponse`, `null` bei einer Person.

Oberfläche:

- `UserOrClientLabel` zeigt das Icon, wenn `hasMachineClients` gilt, statt bei `machineDeliveryEnabled`. Das betrifft Listen und Detailseiten von Lieferungen und Protokollen in der Verwaltung. Ein Aufrufer kann das Icon auch verlangen; das braucht «Meine Lieferungen», weil Personen ohne Adminrechte die Capabilities nicht lesen dürfen.
- «Meine Lieferungen» zeigt die Spalte «Geliefert von» nur, wenn mindestens eine Zeile ein `deliveredByClientId` hat, und dann in jeder Zeile mit Icon. `DeliveryGrid` bekommt dafür eine Option, die nur `UserDeliveries` setzt. Die Verwaltung zeigt die Spalte weiter immer.
- Die Spalte «Versuche» in der Liste der Protokolle hängt an `hasMachineClients` statt an `machineDeliveryEnabled`.
- Auf den Detailseiten von Lieferung und Protokoll steht der Name eines Maschinen-Clients bei ausgeschalteter maschineller Lieferung als Text mit einem Hinweis, analog zu `recordingDeliveryDeleted`. Neuer Schlüssel `machineClientWithoutMachineDelivery`:
  - de: `{{name}} (maschinelle Lieferung ausgeschaltet)`
  - en: `{{name}} (machine delivery turned off)`
  - fr: `{{name}} (livraison machine désactivée)`
  - it: `{{name}} (consegna automatica disattivata)`

Die Oberfläche lädt die Capabilities einmal pro Seitenaufruf. Nach dem Registrieren des ersten Maschinen-Clients erscheinen die Icons darum erst nach dem nächsten Laden der Seite.

CHANGELOG unter Changed: Die Verwaltung kennzeichnet Personen und Maschinen-Clients mit einem Icon, sobald ein Maschinen-Client registriert ist, auch nach dem Ausschalten der maschinellen Lieferung; die Detailseiten sagen dann, warum der Name kein Link ist. «Meine Lieferungen» zeigt «Geliefert von» mit Icons, sobald eine Lieferung eines Maschinen-Clients dabei ist. `GET api/v1/capabilities` liefert `hasMachineClients`, `GET api/v1/delivery/uploads` liefert `deliveredByClientId`.

## Prüfung

- `CapabilitiesControllerTest`: `HasMachineClients` ist `false` ohne Maschinen-Client und `true` mit einem, auch mit einem inaktiven und bei ausgeschalteter maschineller Lieferung.
- `DeliveryControllerTest`: `GetUploads` liefert `DeliveredByClientId` für die Lieferung eines Maschinen-Clients und `null` für die eigene.
- `userDeliveries.cy.ts`: Nur eigene Lieferungen zeigen keine Spalte «Geliefert von». Mit einer Lieferung eines Maschinen-Clients zeigt die Spalte die Icons für Person und Maschinen-Client.
- `adminDeliveryDetail.cy.ts` und `recordingDetail.cy.ts`, mit Capabilities `machineDeliveryEnabled: false` und `hasMachineClients: true`: Icon des Maschinen-Clients, Name ohne Link, Hinweis dahinter.
- `adminDeliveries.cy.ts` und `recordings.cy.ts`: Mit `hasMachineClients: false` keine Icons und keine Spalte «Versuche», mit `true` beides, unabhängig von `machineDeliveryEnabled`.
- Smoke-Test: Einen Maschinen-Client registrieren, einer Person zuordnen und liefern lassen, dann `MachineDelivery:Enabled` auf `false` setzen. Die Verwaltung zeigt bei Lieferungen und Protokollen die Icons und die Spalte «Versuche», die Detailseiten den Hinweis. «Meine Lieferungen» zeigt der verantwortlichen Person die Spalte mit Icons, einer Person ohne Maschinen-Client keine Spalte.
