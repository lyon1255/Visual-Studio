# GnosisRealmCore README

## 1. Mi ez a projekt?

A `GnosisRealmCore` a Gnosis Online realm-szintu backend API-ja. Ez a szolgaltatas kezeli az adott realmhez tartozo tartos adatokat, a karaktereket, a realm szintu zone es node allapotot, valamint a globalis AuthApi-bol lehuzott canonical GameData realm-specifikus osszefesuleset.

Ez a projekt community-friendly szemlelettel keszult. Ugy lett tervezve, hogy:
- official realmkent is futhasson,
- community realmkent is telepitheto legyen,
- egyetlen VPS-en vagy tobb szerveren is mukodhessen,
- ne igenyelje a source kod szerverre telepiteset,
- a schema frissitesek automatikusan telepuljenek inditaskor.

## 2. Fobb felelossegi korok

A RealmCore a kovetkezo dolgokat vegzi:

- karakterek listazasa, letrehozasa, torlese
- karakter teljes save/load
- zone lookup es zone startup orchestration
- node registry es node parancsqueue
- zone heartbeat fogadas
- globalis GameData lehuzasa az AuthApi-bol
- realm override-ok merge-elese memoriaba
- official realm heartbeat kuldese az AuthApi fele

## 3. Miert jo ez official es community realmhez is?

A rendszer ugyanazzal a kodbazissal tamogatja a ket modot.

### Official realm
- globalis AuthApihoz kapcsolodik
- megjelenik a realm listaban
- hivatalos heartbeatet kuld
- canonical GameData-t hasznal

### Community realm
- sajat realm ID-val es sajat adatbazissal fut
- sajat override-okkal dolgozik
- ugyanaz a telepitesi modell
- kesobb sajat content pipeline-hoz is bovitheto

## 4. Publish output alapjan telepites

Ez a README abból indul ki, hogy a projektet Visual Studio-ból publisholod, es a publish outputot toltod fel a szerverre. A source kod szerverre telepitese nem szukseges.

Javasolt mappa:

```text
/opt/gnosis/realmcore/
  app/
    GnosisRealmCore
    GnosisRealmCore.pdb
    appsettings.json
    appsettings.Development.json
    appsettings.Production.json
    SchemaMigrations/
    keys/
      auth_public.pem
```

## 5. Adatbazis es schema frissitesek

A RealmCore szandekosan nem `dotnet ef migrations` workflow-ra epul a szerveren, hanem sajat SQL migration runnerre. Ez community-friendly megoldas, mert:

- nem kell source kod a szerverre
- nem kell `dotnet ef` futtatasa a VPS-en
- official es community realmeken ugyanaz az update mechanizmus fut
- uj oszlop, uj tabla vagy akar torles is automatikusan kioszthato egy release-ben

A schema frissitesek a publish output `SchemaMigrations` mappajaban vannak. Inditaskor a `SchemaMigrationService` vegigmegy ezeken, es a meg nem alkalmazott migrationok SQL-jeit lefuttatja.

A migrationok allapota a `schema_migrations` tablaban tarolodik.

### Fontos szabaly
A migration fajlok sorrendje es tartalma release utan mar ne valtozzon. Uj modositas mindig uj SQL migration file legyen.

Pelda:
- `0001_initial_schema.sql`
- `0002_add_realm_flags.sql`
- `0003_destructive_drop_old_column.sql`

Ha egy migration nevében benne van, hogy `destructive`, akkor az destruktivnak minosul. Ilyenkor a `SchemaMigrations:AllowDestructiveMigrations` szabaly dont.

## 6. Elso inditas Ubuntu VPS-en

### 6.1 Kotelezo csomagok

```bash
sudo apt-get update
sudo apt-get install -y nginx mysql-server openssl
```

### 6.2 Mappak letrehozasa

```bash
sudo mkdir -p /opt/gnosis/realmcore/app
sudo mkdir -p /opt/gnosis/realmcore/app/keys
sudo mkdir -p /opt/gnosis/realmcore/app/logs
```

### 6.3 Publish output feltoltese

A publisholt fajlokat kozvetlenul az `app` mappaba kell feltolteni.

### 6.4 AuthApi publikus kulcs feltoltese

A RealmCore-nak ellenoriznie kell az AuthApi altal kiadott JWT-t. Ehhez a publikus RSA kulcs kell.

Helye:
```text
/opt/gnosis/realmcore/app/keys/auth_public.pem
```

### 6.5 MySQL adatbazis letrehozasa

```bash
sudo mysql
```

```sql
CREATE DATABASE gnosis_realm01 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE USER 'gnosis_realm'@'127.0.0.1' IDENTIFIED BY 'CHANGE_ME';
GRANT ALL PRIVILEGES ON gnosis_realm01.* TO 'gnosis_realm'@'127.0.0.1';

CREATE USER 'gnosis_realm'@'localhost' IDENTIFIED BY 'CHANGE_ME';
GRANT ALL PRIVILEGES ON gnosis_realm01.* TO 'gnosis_realm'@'localhost';

FLUSH PRIVILEGES;
EXIT;
```

### 6.6 appsettings.Production.json letrehozasa

A publish output melle hozz letre egy `appsettings.Production.json` fajlt. A legfontosabb mezok:

- `Database:ConnectionString`
- `AuthApi:BaseUrl`
- `AuthApi:ServiceId`
- `AuthApi:ServiceSecret`
- `JwtValidation:PublicKeyPemPath`
- `Realm:RealmId`
- `Realm:DisplayName`
- `Realm:Region`
- `Realm:Kind`
- `Realm:PublicBaseUrl`
- `LegacyNodeAuth:ApiKey`
- `ServiceAuth:Clients`

### 6.7 Kezi inditas teszthez

```bash
cd /opt/gnosis/realmcore/app
chmod +x ./GnosisRealmCore
ASPNETCORE_ENVIRONMENT=Production ./GnosisRealmCore
```

### 6.8 Health endpoint teszt

```bash
curl http://127.0.0.1:5159/health/live
curl http://127.0.0.1:5159/health/ready
```

## 7. appsettings szekciok

### `Urls`
A helyi Kestrel endpoint. Javasolt:
```json
"Urls": "http://127.0.0.1:5159"
```

### `Database`
A realm DB kapcsolat.

### `AuthApi`
Az AuthApi URL-je es az a service identity, amivel a RealmCore az AuthApi-t hivja.

### `JwtValidation`
Az AuthApi publikus kulcsa es a vart issuer/audience.

### `Realm`
A realm identitasa. Community realmnel itt add meg a sajat nevet, regiojat es `Kind` erteket.

### `ServiceAuth`
A NodeAgent, GameServer vagy mas belso szolgaltatasok HMAC auth adatai.

### `LegacyNodeAuth`
Atmeneti kompatibilitasi opcio a jelenlegi `X-Server-Admin-Key` fejlec alapjan.

### `Security`
HTTPS es trusted proxy beallitasok.

### `SchemaMigrations`
A startupkori SQL migration runner beallitasai.

## 8. Fo API endpointok

### Public / kliens oldali
- `GET /health/live`
- `GET /health/ready`
- `GET /api/character/list`
- `POST /api/character/create`
- `GET /api/character/{id}/details`
- `DELETE /api/character/{id}`
- `GET /api/zone/find/{zoneName}`

### Belso / jatekszerver oldali
- `POST /api/character/save`
- `POST /api/heartbeat/node`
- `GET /api/gamedata`
- `GET /api/gamedata/version`

### Belso / nodeagent oldali
- `POST /api/internal/nodes/register`
- `POST /api/internal/nodes/heartbeat`
- `GET /api/internal/nodes/{nodeId}/commands/next`
- `POST /api/internal/nodes/{nodeId}/commands/{commandId}/ack`

## 9. Hogyan mukodik a GameData itt?

A RealmCore indulaskor vagy frissiteskor:
1. lehuzza az AuthApi canonical snapshotjat
2. beolvassa a sajat `realm_game_data_overrides` rekordjait
3. merge-eli a ket reteget
4. memoriaban cache-eli az eredmenyt
5. ezt adja tovabb a jatekszervereknek

A jatekszerver kozvetlenul nem az AuthApi-bol dolgozik.

## 10. Mi tortenik, ha a schema valtozik egy uj release-ben?

A publisholt release tartalmazza a kovetkezo SQL migration fajlokat is. Inditaskor:
- a mar alkalmazott migrationokat a `schema_migrations` tabla alapjan atugorja
- az ujakat lefuttatja
- ha destruktiv migration van es a config engedi, az oszlopok/tablak torlese is megtortenik

Ez azt jelenti, hogy official es community realmeken ugyanazzal a release csomaggal vegezheto schema update.

## 11. systemd service pelda

```ini
[Unit]
Description=Gnosis Realm Core API
After=network.target

[Service]
WorkingDirectory=/opt/gnosis/realmcore/app
ExecStart=/opt/gnosis/realmcore/app/GnosisRealmCore
User=gnosisrealm
Group=gnosisrealm
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5
SyslogIdentifier=gnosis-realmcore

[Install]
WantedBy=multi-user.target
```

## 12. Nginx pelda

```nginx
server {
    listen 80;
    server_name realm.playgnosis.hu;

    location / {
        proxy_pass http://127.0.0.1:5159;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

## 13. Fontos megjegyzesek

- A RealmCore publisholva lesz community hasznalatra is, ezert a README publikus telepitesre irt.
- A source kod szerverre telepitese nem szukseges.
- A schema update rendszer szandekosan release-alapu.
- A jelenlegi package mar fel van keszitve a kesobbi NodeAgent rewrite-ra.
- A jelenlegi Unity kliens es szerver route-jaihoz igyekszik kompatibilis maradni.

