# Mail

Schickt eine Mail über eine weitergegebene Lieferung, etwa an die Stelle, welche sie übernimmt. Der Mail-Kanal ist eine Benachrichtigung (`notifications`): Er läuft, sobald die Lieferung weitergegeben ist, und ändert ihren Zustand nie. Mit dem Schalter `includeDeclarer` geht dieselbe Mail auch an den Lieferanten und ist damit seine Quittung. Wer dem Lieferanten eine eigene Mail schicken will, nimmt eine zweite Benachrichtigung mit einem anderen Template, die nur den Schalter setzt.

## Implementierung

Ein Mail-Kanal muss unter `channels[X].implementation` den Wert `Geopilot.Pipeline.Channels.Mail.MailChannel` definieren.

## Konfiguration

| Parameter | Herkunft | Bedeutung |
| --- | --- | --- |
| `host` | Basis-Konfiguration, unter `Pipeline:ChannelConfigs` | Der Mailserver. |
| `port` | Basis-Konfiguration | Der Port des Mailservers, meist 587 mit STARTTLS und 465 mit TLS von Anfang an. |
| `sender` | Basis-Konfiguration | Die Absenderadresse. Mit ihrer Domain stellt sich der Kanal dem Server vor. |
| `username`, `password` | Basis-Konfiguration, optional | Die Anmeldung am Server. Fehlen beide, sendet der Kanal ohne Anmeldung. |
| `encryption` | Basis-Konfiguration, optional | `StartTls` (Vorgabe), `SslOnConnect` oder `None`. Ohne Verschlüsselung sendet der Kanal nur ohne Anmeldung, etwa an einen Relay im eigenen Netz. |
| `template` | Pipeline-Definition (`default_config`, in einer Reaktion überschreibbar) | Der Pfad des Templates relativ zum Ressourcen-Verzeichnis (`Storage:ResourcesDirectory`), siehe [Template](#template). |
| `to`, `cc`, `bcc` | Pipeline-Definition oder Mandat (`mandate_config`), optional | Listen von Mail-Adressen für `To`, `Cc` und `Bcc`. |
| `includeDeclarer` | Pipeline-Definition oder Mandat (`mandate_config`), optional | Ob der Lieferant ebenfalls ins `To` kommt. Nicht gesetzt heisst nein. |

Die Werte der Basis-Konfiguration setzt nur der Betrieb: Setzt die Pipeline-Definition einen davon, startet geopilot nicht. Server und Absender stehen in den Appsettings:

```json
"Pipeline": {
  "ChannelConfigs": {
    "Geopilot.Pipeline.Channels.Mail.MailChannel": {
      "host": "smtp.example.ch",
      "port": "587",
      "sender": "geopilot@example.ch",
      "username": "geopilot"
    }
  }
}
```

Das Passwort gehört nicht in die Appsettings, sondern in eine Umgebungsvariable oder einen Vault:

```
Pipeline__ChannelConfigs__Geopilot.Pipeline.Channels.Mail.MailChannel__password=...
```

Ob das Mandat die Empfänger pflegt oder der Satz sie festlegt, entscheidet die Definition. Hier trägt jedes Mandat seine Empfänger selbst ein und entscheidet, ob der Lieferant die Mail bekommt:

```yaml
channels:
  - id: info_mail
    implementation: Geopilot.Pipeline.Channels.Mail.MailChannel
    default_config:
      template: vorlagen/info-mail.html
reaction_sets:
  - id: ablage_mit_info_mail
    display_name:
      de: Ablage mit Info-Mail
    transfers:
      - id: ablage
        display_name:
          de: Ablage
        channel_id: ablage
    notifications:
      - id: info_mail
        display_name:
          de: Info-Mail
        channel_id: info_mail
        mandate_config:
          - key: to
            display_name:
              de: Empfänger der Info-Mail
          - key: cc
            display_name:
              de: Kopie an
          - key: includeDeclarer
            display_name:
              de: Lieferant erhält die Info-Mail
```

## Template

Das Template ist eine HTML-Datei im Ressourcen-Verzeichnis. Es ist in der Sprache des Kunden geschrieben, geopilot wählt keine Sprache. Wer mehrere Sprachen braucht, schreibt den Text mehrfach hinein, etwa getrennt mit `<hr>`.

- **Betreff:** Der `<title>` ist der Betreff der Mail. Fehlt er oder ist er leer, startet geopilot nicht.
- **Platzhalter:** Ein Platzhalter setzt fertigen Text ein, ohne Schleifen und Bedingungen. Im Text sind alle Werte für HTML kodiert, im Betreff stehen sie als Text auf einer Zeile. Ein unbekannter Platzhalter, auch ein falsch geschriebener, hält den Start an.
- **Logo:** Das Template bringt es selbst mit, als Base64 im `src` eines Bildes (`<img src="data:image/png;base64,...">`). So zeigen es Outlook ab 2019 (ausser GIF), Outlook im Web, Apple Mail und Thunderbird, nicht aber Gmail und ältere Outlook-Versionen.
- **Änderungen:** geopilot liest das Template bei jedem Zustellversuch neu. Eine geänderte Datei gilt also auch für Mails, die noch warten.

| Platzhalter | Wert |
| --- | --- |
| `{{delivery_id}}` | Die Id der Lieferung. |
| `{{declared_at}}` | Wann die Lieferung deklariert wurde, in Ortszeit wie das Löschfenster, etwa `20.10.2026 10:00`. |
| `{{mandate_name}}`, `{{mandate_name:de}}` | Der Name des Mandats, mit Sprache in dieser Sprache. Fehlt der Name in ihr, gilt dieselbe Reihenfolge wie bei den Titeln im STAC-Katalog: Englisch, Deutsch, Französisch, Italienisch, dann jede weitere. |
| `{{declarer_name}}` | Der Name des Lieferanten, bei einer maschinellen Lieferung der Name des Clients. |
| `{{comment}}` | Der Kommentar des Lieferanten, mit seinen Zeilenumbrüchen. Leer, wenn er keinen geschrieben hat. |
| `{{files}}` | Die gelieferten Dateien als Liste, im Betreff mit Kommas getrennt. |

```html
<html>
  <head>
    <title>Neue Lieferung für {{mandate_name:de}}</title>
  </head>
  <body>
    <p>{{declarer_name}} hat am {{declared_at}} die Lieferung {{delivery_id}} abgegeben:</p>
    {{files}}
    <p>{{comment}}</p>
  </body>
</html>
```

## Ablauf

- Die Mail geht an `to`, `cc` und `bcc`, und mit `includeDeclarer` an den Lieferanten, sofern er eine Person mit Mail-Adresse ist. Ein Client einer maschinellen Lieferung bekommt keine Mail.
- Bleibt kein Empfänger, endet die Reaktion ohne Versand und ohne Fehler, mit einer Meldung dazu.
- Weist der Server einen Empfänger ab, geht die Mail an niemanden hinaus. Der Versuch scheitert mit der abgewiesenen Adresse in der Meldung und wird wiederholt wie jeder andere, zuletzt steht die Reaktion auf `Error`. Ein falscher Empfänger fällt so auf, statt still zu fehlen. Korrigiert wird er am Mandat, für die folgenden Lieferungen.
- Jeder andere Fehler des Servers zählt gleich: Der Versuch scheitert und wird wiederholt.
- Jeder Versuch schickt dieselbe Message-ID, gebildet aus der Job-Id der Lieferung und der Id der Reaktion. Manche Mailserver verwerfen damit eine Wiederholung. Garantiert ist das nicht: Scheitert ein Versuch, nachdem der Server die Mail angenommen hat, kann sie zweimal ankommen.

## Personendaten

Die Mail trägt, soweit das Template sie verwendet, den Namen des Lieferanten und seinen Kommentar, und mit `includeDeclarer` geht sie an seine Adresse. Weist der Server die Adresse des Lieferanten ab, steht sie in der Meldung der Reaktion und damit im Ausführungsprotokoll.

## Betrieb

- geopilot startet nicht, wenn der Kanal seine Konfiguration abweist: ein Template ohne Titel oder mit unbekanntem Platzhalter, eine ungültige Adresse in der Definition, ein Benutzername ohne Passwort oder umgekehrt, oder eine Anmeldung ohne Verschlüsselung. Ungültige Adressen an einem Mandat lehnt das Speichern des Mandats ab.
- Das Zertifikat des Servers wird immer geprüft. Ein Server mit einem eigenen Zertifikat braucht dessen Aussteller im Zertifikatsspeicher der Installation.
- Angemeldet wird nur mit Benutzername und Passwort, nicht mit OAuth. Microsoft 365 schaltet die Anmeldung mit Passwort für SMTP ab Ende 2026 standardmässig ab, ein Admin kann sie wieder einschalten.
- Lokal fängt Mailpit aus dem Docker Compose die Mails ab: SMTP auf Port 1025 ohne Verschlüsselung und Anmeldung, die Mails unter http://localhost:8025.
