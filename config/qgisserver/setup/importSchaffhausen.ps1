Measure-Command {
    $job1 = Start-Job -ScriptBlock {
        java -jar .\ili2pg-5.3.0\ili2pg-5.3.0.jar --schemaimport --dbschema current --dbhost localhost --dbdatabase geopilot --dbusr HAPPYWALK --dbpwd SOMBERSPORK --smart2Inheritance --createGeomIdx --createEnumTxtCol --sqlEnableNull --sqlExtRefCols --createBasketCol --createFk '.\SH_Nutzungsplanung_V5_0.ili'
    }
    $job2 = Start-Job -ScriptBlock {
        java -jar .\ili2pg-5.3.0\ili2pg-5.3.0.jar --schemaimport --dbschema next --dbhost localhost --dbdatabase geopilot --dbusr HAPPYWALK --dbpwd SOMBERSPORK --smart2Inheritance --createGeomIdx --createEnumTxtCol --sqlEnableNull --sqlExtRefCols --createBasketCol --createFk '.\SH_Nutzungsplanung_V5_0.ili'
    }
    Wait-Job -Job $job1, $job2
    Receive-Job -Job $job1, $job2
}

Measure-Command {
    $job1 = Start-Job -ScriptBlock {
        java -jar .\ili2pg-5.3.0\ili2pg-5.3.0.jar --import --dbschema current --dbhost localhost --dbdatabase geopilot --dbusr HAPPYWALK --dbpwd SOMBERSPORK --importBatchSize 1000 --skipReferenceErrors --skipGeometryErrors --disableValidation '.\sh_sha_SH_Nutzungsplanung_V5_0_Zeitstand0.xtf'
    }
    $job2 = Start-Job -ScriptBlock {
        java -jar .\ili2pg-5.3.0\ili2pg-5.3.0.jar --import --dbschema next --dbhost localhost --dbdatabase geopilot --dbusr HAPPYWALK --dbpwd SOMBERSPORK --importBatchSize 1000 --skipReferenceErrors --skipGeometryErrors --disableValidation '.\sh_sha_SH_Nutzungsplanung_V5_0_Zeitstand2.xtf'
    }
    Wait-Job -Job $job1, $job2
    Receive-Job -Job $job1, $job2
}

# Expected differences in Grundnutzung_Zonenflaeche

## Added TID="sh_4db33530-650d-4f81-9669-5398e7555555" und TID="sh_483ce228-2d95-4ef2-8117-b7009769829e"
## Deleted TID="sh_422d211d-9a6a-445e-95f4-af2ce4fd9238"
## Changed Attribute "Rechtsstatus" onL TID="sh_c97f63e2-b055-4844-9481-140526d4a467"
## Changed Geometry "Geometrie" on  TID="sh_e0286535-cf4f-4371-9a4c-fb82249d3e82"
## Eqal but different geometry on TID="sh_c0bcc6ae-373f-4b5e-9040-06d3bf5c9f44"

## Create views for the differences in database
Get-Content -Path .\createView.sql | docker container exec -i geopilot_db psql -U HAPPYWALK -d geopilot
