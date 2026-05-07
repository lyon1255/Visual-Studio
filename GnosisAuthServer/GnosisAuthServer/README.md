# GnosisAuthServer README

## 1. Mi ez a projekt?

A `GnosisAuthServer` a Gnosis Online rendszer globalis Auth API-ja.

Ez a szolgaltatas a kovetkezo feladatokat latja el:

- jatekos login Steam ticket alapon
- RSA alapu JWT access token kiadas
- realm lista kiszolgalasa a kliens fele
- canonical global GameData tarolasa es kiszolgalasa
- internal schema manifest es migration tartalom kiszolgalasa a RealmCore fele
- admin endpointok biztositasara realm es GameData kezeleshez
- health check endpointok uzemeltetesi ellenorzeshez

Az Auth API a teljes backend egyik kozponti eleme. Ez nem jatekszerver, nem RealmCore, es nem NodeAgent.

A rendszerben a fo szerepek logikailag igy kulonulnek el:

- **GnosisAuthServer**
  - global auth
  - realm lista
  - canonical GameData
  - schema delivery source
- **GnosisRealmCore**
  - realm save/load
  - realm allapot heartbeat kuldes
  - GameData cache
  - schema migration vegrehajtas a sajat DB-re
- **Gnosis Node Agent**
  - zone processzek inditasa es felugyelete
- **Game Server**
  - runtime gameplay

Az Auth API magasabb bizalmi szintu komponens, mint a realm vagy a jatekszerverek, ezert production kornyezetben erzekeny backend szolgaltataskent kell kezelni.

---

## 2. Fobb felelossegi korok

### 2.1 Jatekos auth

Az Auth API fogadja a login kerelmeket, ellenorzi a Steam ticketet, es access tokent ad vissza.

### 2.2 Realm lista

Az Auth API adja vissza, hogy milyen realmek lathatoak a kliens szamara.

Fontos szabaly:
- hogy egy realm official vagy community, azt az Auth adatbazis tarolja
- ezt nem a heartbeat donti el
- ezt nem a service auth role-ok dontik el
- ezt admin oldalon kell kezelni

### 2.3 Realm heartbeat

Minden RealmCore ugyanarra a belso heartbeat endpointre kuld allapotfrissitest.

A heartbeat csak runtime allapotot frissit:
- `status`
- `current_players`
- `max_players`
- `healthy_zone_count`
- `last_heartbeat_at`

A heartbeat nem modositja:
- `is_official`
- `display_name`
- `region`
- `kind`
- `public_base_url`

### 2.4 Global GameData

Az Auth API tarolja a canonical GameData-t, peldaul:

- Items
- Entities
- Quests
- Spells
- Auras

Ez a globalis alap adatforras. A RealmCore ezt lekerni, cache-elni es kesobb merge-elni fogja a sajat realm override-jaival.

### 2.5 Schema delivery

Az Auth API tarolja es kiszolgalja a RealmCore schema migration manifestet es a migration tartalmat.

Ez azt jelenti, hogy a RealmCore nem helyi migration fajlokkal dolgozik, hanem az Auth API-tol keri le a schema valtozasokat, majd a sajat adatbazisara hajtja vegre azokat.

### 2.6 Admin muveletek

Az admin endpointokkal lehet:
- realmeket letrehozni es modositani
- realm official/community allapotot kezelni
- realm listed/enabled allapotot kezelni
- global GameData snapshotot cserelni
- kesobb command mode-hoz alapot adni

---

## 3. Magas szintu mukodesi elv

A rendszer a kovetkezo fo kommunikacios iranyokat kuloniti el.

### Kliens -> Auth API

A kliens innen kapja:
- Steam login
- JWT access token
- realm lista
- sajat account adatokat

### RealmCore -> Auth API

A RealmCore innen kapja vagy ide kuldi:
- heartbeat
- global GameData
- schema manifest
- migration tartalom

### Admin -> Auth API

Az admin muveletek kulon header + IP szabaly alapjan vedettek.

Fontos:
- a public kliens endpointok es az internal service endpointok kulon szinten vannak kezelve
- a public auth JWT-vel megy
- az internal hivasok HMAC + nonce alapu service auth-tal mennek
- az internal service auth realm ownershipet ellenoriz `AllowedRealmIds` alapjan
- official/community besorolas nem service auth role-bol jon, hanem a DB-bol

---

## 4. Fontos endpointok

### 4.1 Public auth endpointok

#### `POST /api/auth/steam`

Steam login.

Feladata:
- SteamId + ticket fogadasa
- Steam validacio
- account letrehozas vagy frissites
- access token kiadas

#### `GET /api/auth/me`

Az aktualis felhasznalo adatait adja vissza ervenyes JWT mellett.

#### `GET /api/auth/servers`

Realm lista endpoint a kliens szamara.

Alias endpoint:
- `GET /api/realms`

---

### 4.2 Internal realm endpointok

#### `POST /api/internal/realms/heartbeat`

Kozos realm heartbeat endpoint official es community RealmCore szamara.

Feladata:
- realm status frissitese
- current players frissitese
- max players frissitese
- healthy zone count frissitese
- utolso heartbeat ido frissitese

Ez az endpoint nem publikus. Csak service auth-tal hivhato.

Megjegyzes:
- a hivo csak a sajat realmjet frissitheti
- ezt az `AllowedRealmIds` korlatozza
- a heartbeat nem modosithat official/community allapotot

---

### 4.3 Internal GameData endpointok

#### `GET /api/internal/gamedata/version`

A global GameData aktualis verziojat adja vissza.

Alias:
- `GET /api/gamedata/version`

#### `GET /api/internal/gamedata/snapshot`

A teljes global GameData snapshotot adja vissza.

Alias:
- `GET /api/gamedata/snapshot`

#### `GET /api/internal/gamedata/prefabs`

Prefab registry jellegu endpoint.

Alias:
- `GET /api/gamedata/prefabs`

Megjegyzes:
A jelenlegi kodban ez a valasz a verzio metadata mellett prefab listara van elokeszitve, de a konkret prefab adatmodell kezeleset kulon figyelni kell a `GameDataService` implementacioban.

---

### 4.4 Internal schema endpointok

#### `GET /api/internal/schema/manifest`

A RealmCore innen kerdezi le, hogy milyen migrationok leteznek.

#### `GET /api/internal/schema/migrations/{migrationId}`

A RealmCore innen keri le egy adott migration teljes tartalmat.

Fontos:
- ezek az endpointok belso endpointok
- service auth kell hozzajuk
- a RealmCore ezeket a sajat DB schema frissitesehez hasznalja

---

### 4.5 Admin endpointok

#### `GET /api/admin/realms`

Az osszes realm listazasa admin celra.

#### `POST /api/admin/realms`

Realm letrehozas.

#### `PUT /api/admin/realms/{realmId}`

Realm modositas.

Tipikus admin muveletek:
- display name modositas
- region modositas
- public URL modositas
- listed allapot modositas
- enabled allapot modositas
- `is_official` modositas

#### `GET /api/admin/gamedata/snapshot`

A jelenlegi global GameData snapshot admin lekerdezese.

#### `POST /api/admin/gamedata/replace`

A global GameData teljes cserje.

---

### 4.6 Health endpointok

#### `GET /health/live`

A process eletben van-e.

#### `GET /health/ready`

Az alkalmazas kesz allapotban van-e, kulonosen adatbazis kapcsolat szempontjabol.

---

## 5. Fajl- es mappaszerkezet

A szerverre telepiteshez nem kell source kod.

A telepiteshez a publish output kell.

Javasolt szerkezet:

```text
/opt/gnosis/authapi/
  app/
    GnosisAuthServer
    GnosisAuthServer.pdb
    appsettings.json
    appsettings.Development.json
    appsettings.Production.json
    dotnet-tools.json
    keys/
      auth_private.pem
      auth_public.pem
    SchemaMigrations/
      realmcore/
        0001_initial_schema.mysql
        0002_....mysql
    logs/
  bootstrap.sql
```

### Fontos

A jelenlegi publish kimenet Linuxon futtathato binaris:

```text
GnosisAuthServer
```

Tehat a futtatas nem `dotnet GnosisAuthServer.dll`, hanem kozvetlen binary inditas.

### Fontos a schema mappanal

A kod alapjan a helyes elvart mappa:

```text
SchemaMigrations/realmcore
```

A file kiterjesztes pedig:

```text
*.mysql
```

Ez fontos, mert a `SchemaCatalogService` ezt a mappat olvassa, es csak ezt a kiterjesztest figyeli.

---

## 6. A projekt fo reszei

### `Program.cs`

Az alkalmazas belepesi pontja.

Feladata:
- konfiguracio betoltese
- szolgaltatasok regisztralasa
- adatbazis kapcsolat ellenorzese
- JWT auth pipeline felallitasa
- rate limiting bekotese
- CORS bekotese
- forwarded headers kezeles
- Kestrel URL beallitas
- middleware pipeline inditasa

### `Controllers/AuthController.cs`

A public login es account endpointok.

Feladata:
- Steam login
- account letrehozas
- token kiadas
- `me` endpoint

### `Controllers/RealmsController.cs`

A kliens fele realm listat adja vissza.

### `Controllers/InternalRealmsController.cs`

A kozos belso heartbeat endpoint.

### `Controllers/GameDataController.cs`

A global GameData endpointok es az admin oldali snapshot csere.

### `Controllers/InternalSchemaController.cs`

A schema manifest es migration content kiszolgalasa a RealmCore fele.

### `Controllers/AdminRealmsController.cs`

Realm admin muveletek.

### `Controllers/HealthController.cs`

Liveness es readiness endpointok.

### `Data/AuthDbContext.cs`

Az EF Core DbContext.

### `Data/Account.cs`

A jatekos account tabla modellje.

### `Data/Realm.cs`

A realm registry tabla modellje.

Fontos modell szabaly:
- `is_official` az elsodleges official/community jelzes
- `realm_type` a leegyszerusitett modellben mar nem szukseges, es kesobb kiveheto

### `Data/GameDataEntities.cs`

A global GameData tablakat leiro modellek.

### `Infrastructure/`

Belso auth es vedelmi segedosztalyok.

Peldak:
- `HmacServiceRequestAuthenticator`
- `MemoryNonceStore`
- `HeaderAdminRequestValidator`
- `ServiceAuthHeaderNames`
- `ServiceAuthContext`

### `Security/`

RSA kulcsbetoltes.

Peldak:
- `FileRsaKeyProvider`
- `IRsaKeyProvider`

### `Services/`

Uzleti logika.

Peldak:
- `JwtTokenService`
- `SteamTicketValidator`
- `RealmRegistryService`
- `GameDataService`
- `SchemaCatalogService`

### `Options/`

A konfiguracios szekciokhoz tartozo strongly typed osztalyok.

Peldak:
- `DatabaseOptions`
- `JwtOptions`
- `SteamOptions`
- `RealmRegistryOptions`
- `ServiceAuthOptions`
- `SecurityOptions`
- `CorsOptions`
- `AdminOptions`
- `SchemaDeliveryOptions`

### `Sql/bootstrap.sql`

Az elso indulashoz szukseges alap adatbazis schema.

### `keys/README.txt`

Leiras a JWT PEM kulcsokrol.

---

## 7. appsettings konfiguracio leirasa

A projekt a kovetkezo forrasokbol olvas konfiguraciot:

- `appsettings.json`
- `appsettings.{Environment}.json`
- `GNOSIS_AUTH_` prefixu environment valtozok

Productionben a legfontosabb:

```text
/opt/gnosis/authapi/app/appsettings.Production.json
```

### 7.1 `Urls`

Pelda:

```json
"Urls": "http://127.0.0.1:5158"
```

Mit csinal:
- megmondja, hogy a Kestrel hol hallgasson

Javaslat:
- maradjon `127.0.0.1:5158`
- a publikus HTTPS-t az Nginx vegye at

### 7.2 `Database`

Pelda:

```json
"Database": {
  "ConnectionString": "Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=CHANGE_ME;SslMode=Required;"
}
```

Mit csinal:
- ez a MySQL kapcsolat

Mit kell modositani:
- adatbazis nev
- user
- jelszo

### 7.3 `Jwt`

Pelda:

```json
"Jwt": {
  "PrivateKeyPemPath": "./keys/auth_private.pem",
  "PublicKeyPemPath": "./keys/auth_public.pem",
  "Issuer": "Gnosis.Auth",
  "Audience": "Gnosis.Clients",
  "AccessTokenMinutes": 20,
  "KeyId": "gnosis-auth-main"
}
```

Mit csinal:
- JWT RSA kulcsok es token metadata

### 7.4 `Steam`

Pelda:

```json
"Steam": {
  "Enabled": true,
  "AppId": 0,
  "PublisherKey": "CHANGE_ME",
  "AllowMockTicketsInDevelopment": false
}
```

Mit csinal:
- Steam ticket validacio beallitasai

### 7.5 `RealmRegistry`

Pelda:

```json
"RealmRegistry": {
  "HeartbeatTimeoutSeconds": 90,
  "HideUnhealthyRealms": true
}
```

Mit csinal:
- realm lathatosagi szabalyok
- heartbeat timeout

### 7.6 `ServiceAuth`

Pelda:

```json
"ServiceAuth": {
  "Enabled": true,
  "AllowedClockSkewSeconds": 30,
  "NonceTtlSeconds": 90,
  "Clients": [
    {
      "ServiceId": "official-eu-realm-core",
      "Secret": "CHANGE_ME_REALMCORE_SHARED_SECRET",
      "AllowedRealmIds": [ "official-eu-1" ]
    }
  ]
}
```

Mit csinal:
- internal service-to-service auth
- HMAC alapu hitelesites
- nonce alapu replay vedelem
- realm ownership ellenorzes `AllowedRealmIds` alapjan

Fontos:
- a leegyszerusitett modellben nincs role rendszer
- minden RealmCore ugyanazzal a belso jogosultsagi modellel dolgozik
- official/community kulonbseg nem itt van, hanem a `realms.is_official` mezoben

### 7.7 `Admin`

Pelda:

```json
"Admin": {
  "Enabled": true,
  "HeaderName": "X-Gnosis-Admin-Key",
  "ApiKey": "CHANGE_ME_ADMIN_KEY",
  "AllowedIpAddresses": [ "127.0.0.1" ]
}
```

Mit csinal:
- admin endpointok vedelme

### 7.8 `Security`

Pelda:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ],
  "KnownIPNetworks": []
}
```

Mit csinal:
- HTTPS policy
- megbizhato proxyk
- forwarded headers kezeles

### 7.9 `Cors`

Pelda:

```json
"Cors": {
  "AllowedOrigins": []
}
```

Mit csinal:
- browser alapu eleresek CORS policy-ja

### 7.10 `SchemaDelivery`

Pelda:

```json
"SchemaDelivery": {
  "Enabled": true,
  "DirectoryPath": "SchemaMigrations/realmcore",
  "Channel": "realmcore"
}
```

Mit csinal:
- megmondja, honnan olvassa az Auth API a migration fajlokat
- ez a schema manifest es migration content endpoint forrasa

---

## 8. Realm ownership es official/community modell

Ez a projekt az official/community kulonbseget a DB-ben tarolja.

A helyes szabalyrendszer:

1. A realm official/community allapotat a `realms.is_official` mezoben taroljuk.
2. Ezt csak admin oldal modositja.
3. A heartbeat ezt nem irhatja at.
4. A kliens realm lista mindig a DB aktualis allapotat mutatja.
5. A service secret csak hitelesitesre valo, nem resource azonositasra.
6. A realm ownershipet a `ServiceAuth.Clients[].AllowedRealmIds` korlatozza.

Ezert:
- `realm_id` a stabil eroforras-azonosito
- `secret` a hitelesitesi adat
- `is_official` az admin altal kezelt metadata

---

## 9. JWT RSA kulcsok

Ez a projekt RSA kulcspaar alapjan ir ala JWT tokeneket.

A fajlok:

```text
/opt/gnosis/authapi/app/keys/auth_private.pem
/opt/gnosis/authapi/app/keys/auth_public.pem
```

A `FileRsaKeyProvider` ezeket tolti be indulaskor.

Pelda letrehozas:

```bash
mkdir -p /opt/gnosis/authapi/app/keys
openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

## 10. Elso telepites Ubuntu VPS-en

Ez a README abbol indul ki, hogy:
- a publish outputot a sajat gepedrol toltesz fel
- a source kodot nem telepited a szerverre
- az AuthApi `app` mappaba kerul

### 10.1 Mappak letrehozasa

```bash
sudo mkdir -p /opt/gnosis/authapi/app
sudo mkdir -p /opt/gnosis/authapi/app/keys
sudo mkdir -p /opt/gnosis/authapi/app/logs
sudo mkdir -p /opt/gnosis/authapi/app/SchemaMigrations/realmcore
```

### 10.2 Publish output feltoltese

A Visual Studio publish output tartalmat toltsd fel ide:

```text
/opt/gnosis/authapi/app
```

### 10.3 Futtathato jog

```bash
chmod +x /opt/gnosis/authapi/app/GnosisAuthServer
```

### 10.4 appsettings.Production.json

Hozd letre itt:

```text
/opt/gnosis/authapi/app/appsettings.Production.json
```

Es production ertekekkel ird felul a default configot.

### 10.5 Kezi inditas

```bash
cd /opt/gnosis/authapi/app
ASPNETCORE_ENVIRONMENT=Production ./GnosisAuthServer
```

---

## 11. systemd service

Javasolt `systemd` service.

```ini
[Unit]
Description=Gnosis Auth API
After=network.target

[Service]
WorkingDirectory=/opt/gnosis/authapi/app
ExecStart=/opt/gnosis/authapi/app/GnosisAuthServer
User=gnosisauth
Group=gnosisauth
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5
SyslogIdentifier=gnosis-authapi

[Install]
WantedBy=multi-user.target
```

---

## 12. Nginx reverse proxy

Javasolt topologia:
- Auth API csak `127.0.0.1:5158`
- Nginx fogadja a publikus 80/443 forgalmat
- Nginx tovabbit a helyi Auth API fele

Pelda auth host:

```nginx
server {
    server_name auth.example.com;

    location / {
        proxy_pass http://127.0.0.1:5158;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
    }

    listen 443 ssl;
    ssl_certificate /etc/letsencrypt/live/auth.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/auth.example.com/privkey.pem;
}
```

---

## 13. Rate limiting

A projekt rate limitinget hasznal.

A jelenlegi fo policy-k:
- `login`
- `realm-list`
- `realm-heartbeat`
- `realm-gamedata-read`
- `realm-schema-read`
- `admin-write`

Ez fontos vedelmi reteg brute-force, flood vagy hibas belso integracio ellen.

---

## 14. Security modell roviden

A jelenlegi Auth API security modellje:
- Steam ticket validacio public login endpointnal
- RSA alapu JWT token kiadas
- internal HMAC service auth
- nonce alapu replay vedelem
- admin header + IP whitelist
- rate limiting
- loopback bind javasolt
- reverse proxy mogotti uzem
- realm ownership `AllowedRealmIds` alapjan
- official/community status DB-bol
- schema migration endpoint internal marad

---

## 15. Kesesobbi admin command mode

A projekt tervezett kovetkezo kenyelmi lepese egy `gnosis-auth` command mode vagy CLI admin layer lehet.

Pelda tervezett parancsok:
- `gnosis-auth help`
- `gnosis-auth realms list`
- `gnosis-auth realms show official-eu-1`
- `gnosis-auth realms set-official official-eu-1 true`
- `gnosis-auth realms set-listed community-eu-01 false`
- `gnosis-auth realms set-enabled community-eu-01 false`

Ez jobb irany, mint a nyers MySQL vagy phpMyAdmin.

---

## 16. Leggyakoribb hibak

### `JWT private key file was not found`

Oka:
- nincs PEM fajl
- rossz path van a configban

### `Access denied for user ...`

Oka:
- rossz MySQL user
- rossz jelszo
- `localhost` vs `127.0.0.1` elteres

### `Address already in use`

Oka:
- a portot mar fogja egy masik process vagy systemd service

### `401` internal endpointon

Oka:
- hianyzik a service auth header
- rossz HMAC secret
- rossz timestamp / nonce

### `403` internal endpointon

Oka:
- a hivo nincs benne az `AllowedRealmIds` listaban az adott realmre

### `ready` health check nem jo

Oka:
- nincs adatbazis kapcsolat
- nincs lefuttatva a bootstrap schema
- hibas DB connection string

---

## 17. Production checklist

Telepites elott:
- csereld a `CHANGE_ME` ertekeket
- generalj RSA kulcspart
- allitsd be a valos Steam AppId es PublisherKey ertekeket
- allitsd be a valos admin API kulcsot
- allitsd be a helyes service secret-eket
- allitsd be a helyes `AllowedRealmIds` ertekeket
- ellenorizd a `SchemaDelivery` mappat es fajlneveket
- tedd az Auth API-t Nginx moge
- engedelyezd a systemd service-t
- teszteld a `live` es `ready` endpointot

---

## 18. Gyors osszefoglalo

A `GnosisAuthServer` jelenlegi leegyszerusitett modellje:
- kezeli a public Steam login flow-t
- RSA JWT-t ad ki
- listazza a realmeket
- fogadja az osszes RealmCore heartbeatjet egy kozos endpointon
- a canonical GameData forrasa
- internal schema manifestet es migration tartalmat ad a RealmCore-nak
- admin oldalon kezeli a realm metadata-t, koztuk az `is_official` allapotot
- systemd + Nginx mogotti Linuxos uzemre van tervezve

Ez a README csak a **GnosisAuthServer** projektre vonatkozik.
