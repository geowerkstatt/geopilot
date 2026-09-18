# Maschinelle Anlieferung

Datenlieferantinnen und -lieferanten, die ihre Daten aus einem automatisierten Prozess abgeben, brauchen keine Weboberfläche. geopilot bietet ihnen dafür eine eigene Ressource: ein Aufruf übergibt Mandat, Lieferangaben und Daten, danach wird der Versuch abgefragt, bis er geliefert oder abgelehnt ist. Die Lieferung deklariert geopilot selbst, sobald der Lauf sie zulässt.

Diese Schnittstelle ist bewusst von der Weboberfläche getrennt. Die Endpunkte, die das Frontend bedient, bewegen sich mit ihm; die hier beschriebenen sollen über die Zeit stabil bleiben.

Maschinell und über die Weboberfläche angelieferte Daten landen in derselben Ablage: dieselbe Pipeline, dieselbe `Delivery` mit ihren `Assets`, dasselbe Ausführungsprotokoll (siehe [Ausführungsprotokoll](Ausfuehrungsprotokoll.md)).

## Voraussetzungen

- **Die Installation bietet die Fähigkeit an.** Ohne `MachineDelivery:Enabled` existieren die Endpunkte nicht: sie sind weder erreichbar noch in der OpenAPI-Beschreibung sichtbar.
- **Das Mandat trägt einen Schlüssel.** Der Client spricht ein Mandat über diesen Schlüssel an, nicht über seine Datenbank-Id. Administratorinnen und Administratoren vergeben ihn in der Mandatsverwaltung; er ist installationsweit eindeutig.
- **Der Aufrufer ist authentifiziert und darf auf das Mandat.** Ein Maschinen-Client ist in geopilot registriert und einer oder mehreren Organisationen zugeteilt (siehe [Authentifizierung](#authentifizierung)). Sichtbar ist ein Mandat, wenn es öffentlich ist oder der Aufrufer einer Organisation angehört, der es zugeteilt ist. Anonyme maschinelle Lieferungen gibt es nicht: eine Lieferung hat immer einen Urheber.

## Authentifizierung

Ein Maschinen-Client holt sich sein Token mit **Client Credentials** (Client ID und Secret) beim Identity Provider der Installation, demselben, an dem sich auch die Benutzerinnen und Benutzer anmelden: dieselbe Authority, dieselbe Audience. Das Token schickt er unverändert als `Authorization: Bearer <token>` mit.

Die Zugangsdaten verwaltet der Identity Provider. geopilot kennt einen Client über seine **Kennung**, das `sub` seines Tokens. Bei opaken Tokens ist es das `sub` der Introspection-Antwort, ersatzweise deren `client_id`. Eine Administratorin oder ein Administrator registriert den Client in der Verwaltung unter *Maschinen-Clients* mit dieser Kennung und einem Anzeigenamen und teilt ihn Organisationen zu, so wie eine Person; über die Organisationen erreicht der Client seine Mandate. Nichts registriert einen Client von sich aus: ein gültiges Token mit unbekannter Kennung wird mit `403` abgewiesen, und das Log der Installation nennt die Kennung, damit sie beim Registrieren nicht am Identity Provider zusammengesucht werden muss. Ein deaktivierter Client wird ebenso abgewiesen. Das ist der Weg, einen Client zu sperren, ohne am Identity Provider etwas zu ändern.

Die Zugangsdaten eines Clients öffnen die Weboberfläche nicht. Ein Client ist kein Benutzer: er erreicht ausschliesslich `api/v1/submission*`, weder eine Lieferungsliste noch die Verwaltung, und ein Token, dessen Kennung als Client registriert ist, wird nirgends als Person behandelt, auch wenn der Identity Provider dazu Benutzerinformationen liefern würde; geopilot fragt sie für einen registrierten Client gar nicht ab. Umgekehrt lässt sich die Kennung einer bestehenden Person nicht als Client registrieren.

Der Anzeigename des Clients erscheint als Urheber der Lieferung, im Portal wie im STAC-Katalog, und das [Ausführungsprotokoll](Ausfuehrungsprotokoll.md) hält den Client fest.

Ein Benutzer-Token wird auf diesen Endpunkten derzeit ebenfalls angenommen. Das dient Probe und Support und wird in einem späteren Schritt auf Clients eingeschränkt.

## Der Ablauf

Wie die Dateien hereinkommen, hängt davon ab, wo die Installation ihre Uploads ablegt. Das ist eine Betriebsentscheidung, keine des Clients, und der Server sagt in der Antwort, welche Form er erwartet.

| Ablage der Uploads | Route | Form |
| --- | --- | --- |
| ausserhalb der API (Objektspeicher) | `POST api/v1/submission` | JSON, verweist auf einen vorher erstellten Upload |
| durch die API (lokales Verzeichnis) | `POST api/v1/submission/files` | `multipart/form-data`, die Dateien liegen im Request |

Wer die falsche Route wählt, erhält `400` mit dem Hinweis auf die richtige. Ein Client wird einmal pro Installation eingerichtet und kennt danach seine Form, so wie er auch seinen Mandatsschlüssel kennt.

### Variante mit vorherigem Upload

1. `POST api/v2/upload` meldet die Dateien mit Name und Grösse an und liefert je Datei eine Upload-URL.
2. Der Client lädt jede Datei an die zurückgegebene URL hoch.
3. `POST api/v1/submission` startet den Versuch:

```json
{
  "mandateKey": "av-2026",
  "uploadId": "0f9c1f2e-4d3b-4a1e-9f2c-7b8e5d4a3c21",
  "comment": "Nachlieferung Perimeter Nord",
  "partialDelivery": false,
  "precursorDeliveryId": 42
}
```

`mandateKey` und `uploadId` sind Pflicht. Ob `comment`, `partialDelivery` und `precursorDeliveryId` verboten, erlaubt oder Pflicht sind, legt das Mandat fest.

### Variante mit Dateien im Request

Ein Aufruf genügt. Die Formularfelder heissen wie die JSON-Felder oben, ohne `uploadId`:

```bash
curl -X POST https://geopilot.example.ch/api/v1/submission/files \
  -H "Authorization: Bearer $TOKEN" \
  -F "mandateKey=av-2026" \
  -F "comment=Nachlieferung Perimeter Nord" \
  -F "file=@lieferung.xtf"
```

**Die Felder müssen vor den Dateien gesendet werden.** Daran hängt eine Zusage: geopilot prüft Mandat und Lieferangaben, bevor es den ersten Byte Dateiinhalt liest. Bei einem Tippfehler im Schlüssel bricht der Server ab, statt erst eine mehrere Gigabyte grosse Datei entgegenzunehmen. Übliche HTTP-Clients senden die Teile in der Reihenfolge, in der sie angegeben werden. Der Feldname der Dateien spielt keine Rolle, entscheidend ist, dass der Teil einen Dateinamen trägt.

Ein einzelnes Formularfeld darf 8 KB gross sein. Das betrifft in der Praxis nur `comment`; ein längeres Feld wird mit `400` abgewiesen.

### Antwort

Beide Routen antworten mit `202 Accepted`, einem `Location` auf den Statusendpunkt und dem Statusdokument. Die Id des Versuchs ist zugleich die Id seines Verarbeitungsjobs.

## Status abfragen

`GET api/v1/submission/{id}`

```json
{
  "id": "3f7a2b91-8c4d-4e5f-a1b2-c3d4e5f60718",
  "state": "rejected",
  "mandateKey": "av-2026",
  "deliveryId": null,
  "messages": [
    {
      "step": "validation",
      "severity": "warning",
      "text": { "de": "Die Validierung war nicht erfolgreich.", "en": "The validation was not successful." }
    }
  ],
  "downloads": [
    {
      "step": "validation",
      "name": "validation.log",
      "url": "https://geopilot.example.ch/api/v1/submission/3f7a2b91-.../downloads/validation_validation.log"
    }
  ]
}
```

`state` trägt das Ergebnis, und zwar als einziges Feld:

| `state` | Bedeutung |
| --- | --- |
| `processing` | Der Lauf ist unterwegs, oder er ist fertig und die Lieferung noch nicht geschrieben. |
| `delivered` | Die Pipeline hat die Daten angenommen, die Lieferung existiert. `deliveryId` ist gesetzt. |
| `rejected` | Die Pipeline hat die Daten geprüft und abgelehnt. Es entsteht keine Lieferung, und die Daten müssen korrigiert werden. |
| `failed` | So weit kam es nicht, oder die Lieferung liess sich nicht anlegen. Nicht die Daten sind das Problem, der Versuch kann wiederholt werden. |

Damit ist auch die Frage beantwortet, ob die Prüfungen bestanden wurden: `delivered` heisst ja, `rejected` heisst nein. Ein eigenes Feld dafür gibt es bewusst nicht. geopilot kennt keinen ausgezeichneten Validierungsschritt; was eine Pipeline prüft und welcher Prozessor ablehnt, steht in ihrer Definition, und eine Pipeline muss überhaupt nichts prüfen. `rejected` sagt deshalb genau so viel, wie sich ehrlich sagen lässt: diese Pipeline hat diese Daten nicht angenommen.

Der Unterschied zwischen `rejected` und `failed` ist der, auf den ein Client reagiert. `rejected` heisst korrigieren, `failed` heisst wiederholen. Auch der Fall, dass die Daten in Ordnung waren und nur das Anlegen der Lieferung scheiterte, ist deshalb `failed`: zu korrigieren gibt es nichts, und `messages` nennt den Grund.

Ein fertiger Lauf bleibt kurz auf `processing`, bis die Lieferung geschrieben ist. `delivered` ohne `deliveryId` gibt es nicht.

`messages` sammelt, was die Schritte gemeldet haben, je mit Schritt und Gewicht (`info`, `warning`, `error`). Die Texte sind mehrsprachig, wie überall in geopilot.

## Was der Lauf zum Herunterladen anbietet

Alles, was ein Schritt über die Ausgabeaktion `Download` bereitstellt, steht in `downloads`, je mit dem Schritt, der es erzeugt hat. Was das ist, entscheidet die Pipeline-Definition: bei der mitgelieferten XTF-Validierung ist es das Validierungsprotokoll, eine andere Pipeline kann dort einen Bericht oder umgewandelte Daten anbieten. geopilot weiss nur, dass die Datei zum Herunterladen markiert wurde.

**Der Client wählt über `step` und `name` aus und holt die Datei über `url`.** Die URL ist der einzige zugesicherte Weg. Sie aus `name` zusammenzusetzen geht schief: `name` ist der Name, den der Schritt der Datei gegeben hat, und weil zwei Schritte denselben Namen vergeben dürfen, steht in der URL ein davon abgeleiteter, eindeutiger Ablagename. Im Beispiel oben heisst die Datei `validation.log` und liegt unter `validation_validation.log`. Heruntergeladen wird sie wieder als `validation.log`.

## Lebensdauer

Ein Versuch lebt im Prozess, nicht in der Datenbank. Er ist höchstens so lange abfragbar wie sein Verarbeitungsjob (`Processing:JobRetention`, standardmässig ein Tag), und ein Neustart der Instanz beendet ihn. Danach antwortet der Statusendpunkt mit `404`.

**Der dauerhafte Griff ist die `deliveryId`.** Die Lieferung ist eine echte Entität, sie überlebt Neustarts und Aufräumläufe. Ein Client, der die Antwort verloren hat, klärt den Ausgang nicht selbst: eine Lieferungsliste gibt es für Clients nicht, aber eine Person seiner Organisation sieht die Lieferung in der Weboberfläche.

Die Links in `downloads` haben ein **kürzeres** Fenster als der Versuch selbst: Downloads werden nach `Processing:DownloadRetention` aufgeräumt, standardmässig nach einer Stunde, während der Job einen Tag lebt. Ein Client, der langsam abfragt, kann also einen fertigen Lauf vorfinden, dessen Dateien schon weg sind. Wer maschinelle Lieferungen anbietet, sollte `DownloadRetention` an die erwartete Abfragefrequenz anpassen.

## Antworten

| Code | Wann |
| --- | --- |
| `202` | Der Versuch ist angenommen und läuft. |
| `400` | Die Anfrage hat die falsche Form für diese Installation, das Mandat nimmt die Dateitypen nicht (oder der Upload enthält keine Datei mit Dateiendung, an der sie sich prüfen liessen), oder die Lieferangaben verletzen die Regeln des Mandats (dann als `ValidationProblemDetails` je Feld). |
| `401` | Kein oder kein gültiges Token. |
| `403` | Das Token gehört weder einem registrierten, aktiven Maschinen-Client noch einem aktiven Benutzer. Das Log der Installation nennt die Kennung, die zu registrieren wäre. |
| `404` | Kein Mandat mit diesem Schlüssel ist für den Aufrufer erreichbar, der Upload ist unbekannt, oder der Versuch existiert nicht mehr. |
| `409` | Das Mandat nimmt keine Lieferungen an, oder es nennt keine Pipeline, die diese Installation anbietet. |
| `413` | Eine Datei ist grösser, als die Installation annimmt. |
| `503` | Die Installation hält bereits so viele oder so grosse Uploads, wie sie gleichzeitig annimmt. `Retry-After` nennt die Wartezeit in Sekunden. |

`400`, `409` und `413` heissen "korrigiere die Anfrage", `503` heisst "schicke dieselbe Anfrage später nochmals". Ein Client, der beides gleich behandelt, wiederholt entweder vergeblich oder gibt zu früh auf.

Ein unbekannter Schlüssel und ein nicht erreichbares Mandat ergeben **dieselbe** Antwort. Sonst wäre der Endpunkt ein Verzeichnis fremder Mandatsschlüssel.

Der Schlüsselvergleich ist **exakt, auch in der Gross- und Kleinschreibung**. `AV-2026` und `av-2026` sind verschiedene Schlüssel, und nur einer davon existiert. Leerzeichen und Zeilenumbrüche am Anfang und Ende werden entfernt, wie beim Speichern des Schlüssels: ein Wert aus einer Konfigurationsdatei verfehlt sein Mandat nicht wegen eines Zeilenumbruchs.

## Hinweis für Pipeline-Definitionen

Ob ungültige Daten beim Client als `rejected` oder als `failed` ankommen, entscheidet die Pipeline-Definition, nicht geopilot.

- Eine Definition, die die Lieferung über `restrict_delivery_conditions` sperrt, führt zu `rejected`. Das ist die richtige Aussage: geprüft und nicht angenommen, der Client korrigiert seine Daten.
- Eine Definition, die denselben Sachverhalt über `fail_conditions` abbildet, führt zu `failed`. Der Client liest daraus "wiederhole es", obwohl seine Daten das Problem sind, und ein Serverproblem ist davon nicht mehr zu unterscheiden.

Für Mandate, die maschinell bedient werden, gehören datenbedingte Ablehnungen deshalb in `restrict_delivery_conditions` (siehe [Pipelines](pipeline/Pipelines.md)).

## Wenn die Fähigkeit nicht eingeschaltet ist

Ohne `MachineDelivery:Enabled` werden die Endpunkte gar nicht erst eingerichtet. Sie antworten nicht, und sie stehen nicht in der OpenAPI-Beschreibung der Installation. Ein Aufruf bekommt dann, was diese Installation auf einen unbekannten Pfad antwortet, und das ist kein verlässliches `404`: mit gültigem Token ist es `403`. Ob eine Installation maschinelle Lieferungen anbietet, ist eine Betriebsentscheidung und nichts, was ein Client zur Laufzeit aushandelt. Die Antwort auf die Frage steht in der OpenAPI-Beschreibung. Auch das Schlüsselfeld am Mandat wird in der Verwaltung dann nicht angeboten, und ein mitgeschickter Schlüssel wird ignoriert, ohne einen bereits gespeicherten zu überschreiben. Dasselbe gilt für die Maschinen-Clients: ihre Verwaltung (`api/v1/machineclient` und die Seite *Maschinen-Clients*) existiert nicht, und eine Organisation behält ihre zugeteilten Clients, auch wenn sie ohne die Fähigkeit gespeichert wird. So pflegt niemand einen Wert, der für seine Installation keine Bedeutung hat.
