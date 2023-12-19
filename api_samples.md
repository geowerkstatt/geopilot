# API Beispiel Workflow

Dokumentation eines exemplarischen Workflows zur Nutzung der geocop API
API-Doku / Swagger: https://geocop.ch/swagger/index.html****

## Health Check
![POST](https://img.shields.io/badge/POST-yellow.svg?style=flat-square)
https://geocop.ch/health


## Upload und Validierung

Zweck: Upload und Validierung einer Datei

![POST](https://img.shields.io/badge/POST-yellow.svg?style=flat-square)

```
https://geocop.ch/api/v1/Validation
```
Parameter: form-data ( binary file )


Antwort:

```
{
    "jobId": "66c24b44-8ce3-4d88-975f-eaf7dd412c83",
    "status": "processing",
    "validatorResults": {}
}
```

Status-Rückfrage mittels JobId:

![GET](https://img.shields.io/badge/GET-darkgreen.svg?style=flat-square)
```
https://geocop.ch/api/v1/Validation/dd4334ed-5d3d-47c1-9985-c40ae3478ac7
```

Antwort (nach Abschluss der Validierung):

```
{
    "jobId": "dd4334ed-5d3d-47c1-9985-c40ae3478ac7",
    "status": "completed",
    "validatorResults": {
        "ilicheck": {
            "status": "completed",
            "statusMessage": "Die Daten sind modellkonform.",
            "logFiles": {
                "Log": "ka51shzl_log.log",
                "Xtf-Log": "ka51shzl_log.xtf"
            }
        }
    }
}
```

## Abgabe

### Auth-Information abfragen

Die OAuth2-Informationen zum IdentityProvider können wie folgt abgefragt werden:

![GET](https://img.shields.io/badge/GET-darkgreen.svg?style=flat-square)

```
https://geocop.ch/api/v1/user/auth
```

Antwort:

```
{
    "authority": "https://login.microsoftonline.com/16e916d3-12c9-4353-ad04-5a4319422e03/v2.0",
    "clientId": "ac09549e-6cf8-40fe-91a9-25515ec71954",
    "redirectUri": "/",
    "postLogoutRedirectUri": "/",
    "navigateToLoginRequestUrl": false
}
```


### Operate anfragen:

Gibt die zum Upload möglichen Operate retour:

![GET](https://img.shields.io/badge/GET-darkgreen.svg?style=flat-square)
```
https://geocop.ch/api/v1/Mandate?jobId=dd4334ed-5d3d-47c1-9985-c40ae3478ac7
```

Antwort:

```
[
  {
    "id": 4,
    "name": "AFU.Strassenlaerm",
    "fileTypes": [
      ".xtf"
    ],
    "organisations": [],
    "deliveries": []
  },
  {
    "id": 10,
    "name": "TG.Wasser.Mammern",
    "fileTypes": [
      ".xtf"
    ],
    "organisations": [],
    "deliveries": []
  },
```

### Abgabe durchführen

![POST](https://img.shields.io/badge/POST-yellow.svg?style=flat-square)

```
https://geocop.ch/api/v1/Delivery
```

```
{
  "jobId": "dd4334ed-5d3d-47c1-9985-c40ae3478ac7",
  "deliveryMandateId": 4,
  "partialDelivery": false,
  "precursorDeliveryId": null,
  "comment": "Dies ist ein Kommentar"
}
```

Antwort:

```
{
  "id": 25,
  "jobId": "dd4334ed-5d3d-47c1-9985-c40ae3478ac7",
  "date": "2023-12-12T10:45:04.453968Z",
  "declaringUser": {
    "email": "",
    "fullName": "",
    "isAdmin": false,
    "organisations": [],
    "deliveries": []
  },
  "deliveryMandate": {
    "id": 0,
    "name": "",
    "fileTypes": [],
    "organisations": [],
    "deliveries": []
  },
  "assets": [],
  "partial": false,
  "precursorDelivery": null,
  "comment": "Dies ist ein Kommentar",
  "deleted": false
}
```

### Anfrage Items

Gibt die Items (Datenabgaben) zu einer Collection retour:

![GET](https://img.shields.io/badge/GET-darkgreen.svg?style=flat-square)

```
https://geocop.ch/api/stac/collections/coll_4
```

```
{
    "id": "coll_4",
    "stac_version": "1.0.0",
    "links": [
        {
            "rel": "item",
            "title": "Datenabgabe_2023-12-05T15:10:59",
            "href": "https://geocop.ch:443/api/stac/collections/coll_4/items/item_13",
            "type": "application/geo+json"
        },
        {
            "rel": "item",
            "title": "Datenabgabe_2023-12-12T10:45:04",
            "href": "https://geocop.ch:443/api/stac/collections/coll_4/items/item_25",
            "type": "application/geo+json"
        },
        {
            "method": "GET",
            "type": "application/json",
            "rel": "self",
            "title": "AFU.Strassenlaerm",
            "href": "https://geocop.ch/api/stac/collections/coll_4"
        },
        {
            "method": "GET",
            "type": "application/json",
            "rel": "root",
            "href": "https://geocop.ch:443/api/stac"
        }
    ],
    "type": "Collection",
    "extent": {
        "spatial": {
            "bbox": [
                [
                    47.067,
                    7.297,
                    47.523,
                    8.014
                ]
            ]
        },
        "temporal": {
            "interval": [
                [
                    "2023-12-05T15:10:59.589805Z",
                    "2023-12-12T10:45:04.453968Z"
                ]
            ]
        }
    },
    "keywords": [],
    "description": "",
    "license": null,
    "title": "AFU.Strassenlaerm"
}
```

### Anfrage Item

Gibt Details zu einem spezifischen Item retour

![GET](https://img.shields.io/badge/GET-darkgreen.svg?style=flat-square)

```
https://geocop.ch/api/stac/collections/coll_4/items/item_25
```

Die `href`-Angaben im Response entsprechen den Download-Links

```
{
    "stac_version": "1.0.0",
    "type": "Feature",
    "id": "item_25",
    "geometry": {
        "type": "Polygon",
        "coordinates": [
            [
                [
                    47.067,
                    7.297
                ],
                [
                    47.067,
                    8.014
                ],
                [
                    47.523,
                    8.014
                ],
                [
                    47.523,
                    7.297
                ],
                [
                    47.067,
                    7.297
                ]
            ]
        ]
    },
    "properties": {
        "title": "Datenabgabe_2023-12-12T10:45:04",
        "description": "",
        "datetime": "2023-12-12T10:45:04.453968Z"
    },
    "bbox": [
        47.067,
        7.297,
        47.523,
        8.014
    ],
    "assets": {
        "SP_VS_1_4.xtf": {
            "type": "application/interlis+xml",
            "roles": [
                "PrimaryData"
            ],
            "title": "SP_VS_1_4.xtf",
            "href": "https://geocop.ch/api/v1/delivery/assets/73"
        },
        "ilicheck_Log.log": {
            "type": "text/plain",
            "roles": [
                "ValidationReport"
            ],
            "title": "ilicheck_Log.log",
            "href": "https://geocop.ch/api/v1/delivery/assets/74"
        },
        "ilicheck_Xtf-Log.xtf": {
            "type": "application/interlis+xml",
            "roles": [
                "ValidationReport"
            ],
            "title": "ilicheck_Xtf-Log.xtf",
            "href": "https://geocop.ch/api/v1/delivery/assets/75"
        }
    },
```

