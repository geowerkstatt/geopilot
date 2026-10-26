# Weitergabe betreiben

Ist eine Lieferung abgeschlossen, gibt geopilot sie mit den Reaktionen weiter, die der Satz ihres Mandats vorsieht, etwa in eine [Ablage](pipeline/Kanaele/ablage.md). Wie ein Satz definiert wird, steht unter [Weitergabe nach dem Abschluss](pipeline/Pipelines.md#weitergabe-nach-dem-abschluss). Diese Seite beschreibt den Betrieb: wann die Reaktionen laufen, wie sich Wiederholungen und Zeitlimit einstellen lassen, woran sich der Stand ablesen lässt und was eine Installation unter IIS braucht.

## Ablauf

- Bei der Deklaration entsteht pro Lieferungs-Schritt des Satzes eine Reaktion. Sie wartet, bis das Löschfenster der Lieferanten schliesst (siehe [Löschen von Lieferungen](../README.md#löschen-von-lieferungen)); dürfen Lieferanten nicht löschen, ist sie sofort fällig. Ein Mandat mit einem Satz verlangt deshalb ein Löschfenster, das schliesst, sonst startet geopilot nicht.
- Ein Hintergrunddienst sucht beim Start und danach alle `PollInterval` nach fälligen Reaktionen und führt sie aus, pro Instanz einen Zustellversuch nach dem anderen. Dauert ein Versuch lange, warten die übrigen so lange.
- Gelingt ein Versuch, ist die Reaktion `Done`. Sind alle Lieferungs-Schritte einer Lieferung `Done`, ist sie weitergegeben, und erst dann erscheint sie im STAC-Katalog.
- Misslingt ein Versuch, beginnt der nächste den Abstand aus `RetryDelays` nach dem Start des misslungenen, ist dieser Zeitpunkt schon vorbei, sofort. Ein Versuch, der länger als `TimeLimit` dauert, misslingt.
- Nach dem letzten misslungenen Versuch steht die Reaktion auf `Error` und läuft erst wieder, wenn ein Admin sie [zurücksetzt](#zurücksetzen). Eine Lieferung mit einem Lieferungs-Schritt in `Error` gilt nicht als weitergegeben.

## Konfiguration

Wiederholungen und Zeitlimit stehen im Abschnitt `Delivery:Reactions`. Ohne Eintrag gelten die Standardwerte.

| Schlüssel | Standard | Bedeutung |
| --- | --- | --- |
| `RetryDelays` | `00:01:00`, `00:05:00`, `00:30:00`, `02:00:00`, `06:00:00`, `12:00:00` | Die Abstände von Start zu Start zwischen den Versuchen einer Reaktion. Eine Reaktion bekommt einen Versuch mehr, als die Liste Einträge hat, mit dem Standard also sieben; der letzte beginnt knapp 21 Stunden nach dem ersten. |
| `TimeLimit` | `00:30:00` | Wie lange ein einzelner Versuch dauern darf, gemessen ab dem Holen der Reaktion. |
| `PollInterval` | `00:00:15` | Wie oft eine Instanz nach fälligen Reaktionen sucht. |

In den Appsettings:

```json
"Delivery": {
  "Reactions": {
    "RetryDelays": [ "00:01:00", "00:10:00", "01:00:00" ],
    "TimeLimit": "00:10:00"
  }
}
```

oder als Umgebungsvariablen:

```
Delivery__Reactions__RetryDelays__0=00:01:00
Delivery__Reactions__RetryDelays__1=00:10:00
Delivery__Reactions__RetryDelays__2=01:00:00
Delivery__Reactions__TimeLimit=00:10:00
```

- Eine gesetzte Liste ersetzt den Standard ganz. Das Beispiel gibt jeder Reaktion also vier Versuche.
- Alle Werte müssen grösser als null sein, sonst startet geopilot nicht.
- Läuft die Applikation durch, ist eine Reaktion spätestens erledigt oder aufgegeben, wenn nach dem Ende des Löschfensters die Summe aller Abstände verstrichen und ihr letzter Versuch beendet ist.
- `TimeLimit` sollte grosszügig über der Dauer des längsten normalen Versuchs liegen, etwa einer grossen Lieferung auf ein langsames Netzlaufwerk.

Was ein Kanal sonst braucht, etwa das Basisverzeichnis einer Ablage oder Zugangsdaten, steht in seiner Basis-Konfiguration unter `Pipeline:ChannelConfigs` (siehe [Konfiguration eines Kanals](pipeline/Pipelines.md#konfiguration-eines-kanals)). Zugangsdaten gehören nur dorthin, nie in die Pipeline-Definition oder an ein Mandat.

## Neustart und Ausfall

- Stoppt geopilot, etwa für ein Update, bricht ein laufender Versuch ab. Er zählt als misslungen, mit der Meldung `The attempt was interrupted because the application stopped.`, und es geht weiter wie nach jedem misslungenen Versuch.
- Stirbt die Applikation mitten in einem Versuch, hält dieser seine Reaktion noch, bis seit seinem Start das Zeitlimit plus eine Minute vergangen ist. Danach übernimmt eine Instanz die Reaktion mit dem nächsten Versuch, nach einem Neustart also die neu gestartete. War es der letzte Versuch, steht die Reaktion auf `Error`, mit der Meldung `The last attempt did not report back before its lock ran out.`
- Was während eines Ausfalls fällig wurde, holt geopilot beim Start nach. Dabei geht kein Versuch verloren, das Ende einer Reaktion verschiebt sich um die Dauer des Ausfalls.

## Stand ablesen

In der Administration zeigt die Lieferungsübersicht in der Spalte Weitergabe den zusammengefassten Zustand jeder Lieferung: `Error` vor `Running` vor `Pending` vor `Done`. Ein Klick darauf öffnet die Reaktionen der Lieferung mit Zustand, Versuchen, Zeitpunkt und Meldung. Eine Reaktion, die auf `Pending` steht und noch keinen Versuch hatte, wartet auf das Ende des Löschfensters ("Wartet bis"). Dieselben Angaben liefert `GET api/v1/delivery` mit `forwardingState` und `reactions`.

Jeder Zustellversuch steht im [Ausführungsprotokoll](Ausfuehrungsprotokoll.md) des Laufs, aus dem die Lieferung stammt: in der Tabelle `PipelineRunReactionEvents` und über die API unter `GET api/v1/pipelinerun/{jobId}` als `reactionEvents`.

Im Log schreibt geopilot unter den Kategorien `Geopilot.Api.Reactions.ReactionWorker` und `Geopilot.Api.Reactions.ReactionStore`:

| Stufe | Meldung | Bedeutung |
| --- | --- | --- |
| Information | `The delivery reaction {ReactionId} of delivery {DeliveryId} is done: {Message}` | Der Versuch ist gelungen. |
| Warning | `Attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} failed, the next one starts {Wait} after it started: {Message}` | Der Versuch ist misslungen, ein weiterer folgt. |
| Warning | `The delivery reaction {ReactionId} of delivery {DeliveryId} no longer belonged to its attempt when the attempt reported: {Message}` | Der Versuch hielt seine Reaktion bei der Rückmeldung nicht mehr, etwa weil die Lieferung inzwischen gelöscht wurde. |
| Error | `Attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} failed unexpectedly.` | Der Kanal ist mit einer Ausnahme gescheitert. Der Versuch zählt als misslungen. Die Meldung der Reaktion nennt nur den Typ der Ausnahme, die Einzelheiten stehen in diesem Logeintrag. |
| Error | `The transfer {ReactionId} of delivery {DeliveryId} gave up after {Attempts} attempts: {Message}` | Ein Lieferungs-Schritt ist endgültig gescheitert, die Lieferung gilt nicht als weitergegeben. |
| Error | `The outcome of attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} could not be saved. Delivered: {Delivered}. {Message}` | Der Versuch ist gelaufen, sein Ergebnis liess sich aber nicht speichern, etwa weil die Datenbank nicht erreichbar war. Nach Ablauf der Sperre läuft die Reaktion noch einmal oder gibt auf, und im Ausführungsprotokoll bleibt der Ausgang unbekannt. Ob der Versuch geliefert hat, steht nur hier. |
| Error | `{Count} delivery reactions gave up because their last attempt did not report back before its lock ran out.` | Der letzte Versuch dieser Reaktionen hat sich nie zurückgemeldet, etwa nach einem Absturz. |
| Error | `Running the due delivery reactions failed.` | Der Dienst konnte die fälligen Reaktionen nicht holen, etwa weil die Datenbank nicht erreichbar war. Er versucht es beim nächsten `PollInterval` wieder. |

Ein Alarm auf die Meldungen der Stufe Error dieser beiden Kategorien meldet jede Lieferung, deren Weitergabe endgültig scheitert, und jedes Mal, wenn der Dienst die fälligen Reaktionen nicht holen oder ein Ergebnis nicht speichern kann.

## Zurücksetzen

Hat eine Reaktion aufgegeben, behebt der Betrieb die Ursache, etwa die Rechte auf das Basisverzeichnis einer Ablage, und setzt sie als Admin zurück: im Dialog der Lieferung mit "Zurücksetzen" oder über `POST api/v1/delivery/{deliveryId}/reactions/reset`. Das setzt alle Reaktionen der Lieferung zurück, die auf `Error` stehen: Jede bekommt wieder alle Versuche, ist sofort fällig, und ihre Abstände beginnen von vorne. Reaktionen in einem anderen Zustand bleiben, wie sie sind. Steht keine auf `Error`, antwortet geopilot mit `409`. Im Ausführungsprotokoll kommt pro Reaktion eine Zeile `Reset` mit dem Admin dazu.

## Betrieb unter IIS

geopilot führt die Reaktionen nur aus, solange sein Prozess läuft. IIS startet ihn standardmässig erst mit der ersten Anfrage und beendet ihn nach 20 Minuten ohne Anfrage. Dann gibt geopilot eine Lieferung erst weiter, wenn die nächste Anfrage die Applikation weckt, auch wenn das Löschfenster längst geschlossen hat. Damit die Weitergabe pünktlich läuft:

1. Die IIS-Funktion **Application Initialization** installieren (unter **Web Server > Application Development**).
2. Im App-Pool von geopilot unter **Advanced Settings** den **Start Mode** auf `AlwaysRunning` setzen (Standard `OnDemand`) und **Idle Time-out (minutes)** auf `0` (Standard 20).
3. In der Site von geopilot unter **Manage Website > Advanced Settings** **Preload Enabled** auf `True` setzen.

Damit startet geopilot mit dem Server und nach jedem Recycling ohne Anfrage und läuft auch ohne Anfragen weiter. Microsoft beschreibt die Einstellungen unter [Application Initialization Module and Idle Timeout](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/advanced#application-initialization-module-and-idle-timeout). Der Idle-Timeout betrifft geopilot, weil es unter IIS standardmässig in-process läuft.

Ein Recycling des App-Pools, in IIS standardmässig alle 1740 Minuten, startet geopilot neu. Ein laufender Versuch misslingt dabei und wird wiederholt. Wer das vermeiden will, legt das Recycling auf eine feste Zeit in der Nacht.

Im Container läuft geopilot ohne Unterbruch, dort braucht es nichts davon.
