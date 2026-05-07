# GnosisAuthServer README

## 1. Mi ez a projekt?

A `GnosisAuthServer` a Gnosis Online rendszer globalis Auth API-ja.

Ez a szolgaltatas a kovetkezo feladatokat latja el:

* jatekos login Steam ticket alapon
* RSA alapu JWT access token kiadas
* official es community realm lista kiszolgalasa a kliens fele
* official realm heartbeat fogadas belso, hitelesitett endpointon
* globalis, canonical GameData tarolasa
* internal schema manifest es migration tartalom kiszolgalasa a RealmCore fele
* admin endpointok kiszolgalasa realm es GameData kezeleshez
* health check endpointok biztosítása uzemeltetesi ellenorzeshez

Ez a projekt a teljes backend egyik kozponti eleme, nem pedig a teljes rendszer onmagaban.

A teljes rendszerben a szerepek logikailag igy kulonulnek el:

* **GnosisAuthServer**

  * globalis auth
  * realm lista
  * canonical GameData
  * schema delivery source
* **GnosisRealmCore**

  * realm save/load
  * merged GameData cache
  * schema migration vegrehajtas a sajat DB-re
  * zone, node es heartbeat logika
* **Gnosis Node Agent**

  * zone processzek inditasa es felugyelete
* **Game Server**

  * konkret runtime gameplay

Az Auth API magasabb bizalmi szintu komponens, mint a realm vagy a jatekszerverek, ezert production kornyezetben ugy kell uzemeltetni, mint egy erzekeny backend szolgaltatast.

---

## 2. Fobb felelossegi korok

### 2.1 Jatekos auth

Az Auth API fogadja a login kerelmeket, ellenorzi a Steam ticketet, es access tokent ad vissza.

### 2.2 Realm lista

Az Auth API adja vissza, hogy milyen realmek lathatoak a kliens szamara.

### 2.3 Official realm heartbeat

Az official RealmCore hitelesitett heartbeatet kuld az Auth API-nak. Ez alapjan az Auth API frissiti a realm statuszt, jatekos letszamot es a healthy zone darabszamot.

### 2.4 Global GameData

Az Auth API tarolja a canonical GameData-t, peldaul:

* Items
* Entities
* Quests
* Spells
* Auras

Ez a globalis alap adatforras. A RealmCore ezt lekerni, cache-elni es merge-elni fogja a sajat realm override-jaival.

### 2.5 Schema delivery

Az Auth API tarolja es kiszolgalja a RealmCore schema migration manifestet es a migration tartalmat.

Ez azt jelenti, hogy a RealmCore nem helyi migration fajlokkal dolgozik, hanem az Auth API-tol keri le a schema valtozasokat, majd a sajat adatbazisara hajtja vegre azokat.

### 2.6 Admin muveletek

Az admin endpointokkal lehet:

* realmeket letrehozni es modositani
* globalis GameData snapshotot cserelni
* admin szintu ellenorzo muveleteket vegezni

---

## 3. Magas szintu mukodesi elv

A rendszer a kovetkezo fo kommunikacios iranyokat kuloniti el.

### Kliens -> Auth API

A kliens innen kapja:

* Steam login
* JWT access token
* realm lista
* sajat account adatokat

### Official RealmCore -> Auth API

Az official realm backend innen kapja vagy ide kuldi:

* heartbeat
* global GameData
* schema manifest
* migration tartalom

### Admin -> Auth API

Az admin muveletek kulon header alapjan vannak vedve.

Fontos:

* a public kliens endpointok es az internal service endpointok kulon szinten vannak kezelve
* a public auth JWT-vel megy
* az internal hivasok HMAC + nonce alapu service auth-tal mennek
* az admin muveletek kulon header + IP whitelist alapjan vedettek

---

## 4. Fontos endpointok

## 4.1 Public auth endpointok

### `POST /api/auth/steam`

Steam login.

Feladata:

* SteamId + ticket fogadasa
* Steam validacio
* account letrehozas vagy frissites
* access token kiadas

### `GET /api/auth/me`

Az aktualis felhasznalo adatait adja vissza ervenyes JWT mellett.

### `GET /api/auth/servers`

Realm lista endpoint a kliens szamara.

Alias endpoint:

* `GET /api/realms`

---

## 4.2 Internal official realm endpointok

### `POST /api/internal/official-realms/heartbeat`

Official realm heartbeat endpoint.

Feladata:

* official realm status frissitese
* current players
* max players
* healthy zone count
* utolso heartbeat ido frissitese

Ez az endpoint nem publikus.
Csak service auth-tal hivhato.

---

## 4.3 Internal GameData endpointok

### `GET /api/internal/gamedata/version`

A global GameData aktualis verziojat adja vissza.

Alias:

* `GET /api/gamedata/version`

### `GET /api/internal/gamedata/snapshot`

A teljes global GameData snapshotot adja vissza.

Alias:

* `GET /api/gamedata/snapshot`

### `GET /api/internal/gamedata/prefabs`

Prefab registry jellegu endpoint.

Alias:

* `GET /api/gamedata/prefabs`

Megjegyzes:
a jelenlegi kodban ez a valasz a verzio metadata mellett prefab listara van elokeszitve, de a konkret prefab adatmodell kezeleset kulon figyelni kell a GameDataService implementacioban.

---

## 4.4 Internal schema endpointok

### `GET /api/internal/schema/manifest`

A RealmCore innen kerdezi le, hogy milyen migrationok leteznek.

### `GET /api/internal/schema/migrations/{migrationId}`

A RealmCore innen keri le egy adott migration teljes tartalmat.

Fontos:

* ezek az endpointok belso endpointok
* service auth kell hozzajuk
* a RealmCore ezeket a sajat DB schema frissitesehez hasznalja

---

## 4.5 Admin endpointok

### `GET /api/admin/realms`

Az osszes realm listazasa admin celra.

### `POST /api/admin/realms`

Realm letrehozas.

### `PUT /api/admin/realms/{realmId}`

Realm modositas.

### `GET /api/admin/gamedata/snapshot`

A jelenlegi global GameData snapshot admin lekerdezese.

### `POST /api/admin/gamedata/replace`

A global GameData teljes cserje.

---

## 4.6 Health endpointok

### `GET /health/live`

A process eletben van-e.

### `GET /health/ready`

Az alkalmazas kesz allapotban van-e, kulonosen adatbazis kapcsolat szempontjabol.

---

## 5. Fajl- es mappaszerkezet

A szerverre telepiteshez **nem kell source kod**.

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

A jelenlegi publish kimenet nalad egy Linuxon futtathato binaris:

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

* konfiguracio betoltese
* szolgaltatasok regisztralasa
* adatbazis kapcsolat ellenorzese
* JWT auth pipeline felallitasa
* rate limiting bekotese
* CORS bekotese
* forwarded headers kezeles
* Kestrel URL beallitas
* middleware pipeline inditasa

### `Controllers/AuthController.cs`

A public login es account endpointok.

Feladata:

* Steam login
* account letrehozas
* token kiadas
* `me` endpoint

### `Controllers/RealmsController.cs`

A kliens fele realm listat adja vissza.

### `Controllers/InternalOfficialRealmsController.cs`

Az official realm heartbeat endpoint.

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

### `Data/GameDataEntities.cs`

A global GameData tablakat leiro modellek.

A jelenlegi tablák:

* `gamedata_items`
* `gamedata_entities`
* `gamedata_quests`
* `gamedata_spells`
* `gamedata_auras`
* `gamedata_versions`

### `Infrastructure/`

Belso auth es vedelmi segedosztalyok.

Peldak:

* `HmacServiceRequestAuthenticator`
* `MemoryNonceStore`
* `HeaderAdminRequestValidator`
* `ServiceAuthHeaderNames`
* `ServiceRoles`

### `Security/`

RSA kulcsbetoltes.

Peldak:

* `FileRsaKeyProvider`
* `IRsaKeyProvider`

### `Services/`

Uzleti logika.

Peldak:

* `JwtTokenService`
* `SteamTicketValidator`
* `RealmRegistryService`
* `GameDataService`
* `SchemaCatalogService`

### `Options/`

A konfiguracios szekciokhoz tartozo strongly typed osztalyok.

Peldak:

* `DatabaseOptions`
* `JwtOptions`
* `SteamOptions`
* `RealmRegistryOptions`
* `ServiceAuthOptions`
* `SecurityOptions`
* `CorsOptions`
* `AdminOptions`
* `SchemaDeliveryOptions`

### `Sql/bootstrap.sql`

Az elso indulashoz szukseges alap adatbazis schema.

### `keys/README.txt`

Leiras a JWT PEM kulcsokrol.

---

## 7. appsettings konfiguracio leirasa

A projekt a kovetkezo forrasokbol olvas konfiguraciot:

* `appsettings.json`
* `appsettings.{Environment}.json`
* `GNOSIS_AUTH_` prefixu environment valtozok

A gyakorlatban productionben ez a legfontosabb:

```text
/opt/gnosis/authapi/app/appsettings.Production.json
```

## 7.1 `Urls`

Pelda:

```json
"Urls": "http://127.0.0.1:5158"
```

Mit csinal:

* megmondja, hogy a Kestrel hol hallgasson

Javaslat:

* maradjon `127.0.0.1:5158`
* a publikus HTTPS-t az Nginx vegye at

---

## 7.2 `Database`

Pelda:

```json
"Database": {
  "ConnectionString": "Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=CHANGE_ME;SslMode=Required;"
}
```

Mit csinal:

* ez a MySQL kapcsolat

Mit kell modositani:

* adatbazis nev
* user
* jelszo

Fontos:

* productionben ne maradjon `CHANGE_ME`
* ha helyi MySQL-t hasznalsz, a `127.0.0.1` teljesen jo

---

## 7.3 `Jwt`

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

* itt vannak a JWT RSA kulcsokra es a token metadata-ra vonatkozo beallitasok

Mit kell modositani:

* a kulcsok pathja, ha nem a default helyen vannak
* `Issuer` es `Audience` csak akkor, ha a teljes auth modellt attervezed
* `AccessTokenMinutes`, ha mas session hosszt akarsz

Fontos:

* ha a PEM fajlok nem leteznek, az alkalmazas indulaskor exceptiont dob

---

## 7.4 `Steam`

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

* a Steam ticket validacio beallitasai

Mit kell modositani:

* `AppId`
* `PublisherKey`

Megjegyzes:

* development modban lehet bypass, de productionben ez maradjon kikapcsolva

---

## 7.5 `RealmRegistry`

Pelda:

```json
"RealmRegistry": {
  "HeartbeatTimeoutSeconds": 90,
  "HideUnhealthyRealms": true
}
```

Mit csinal:

* realm lathatosagi szabalyok
* heartbeat timeout

Mit kell modositani:

* ha mas heartbeat ritmussal dolgozol

---

## 7.6 `ServiceAuth`

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
      "Roles": [ "official-realm-heartbeat.write", "realm-gamedata.read" ],
      "AllowedRealmIds": [ "official-eu-1" ]
    }
  ]
}
```

Mit csinal:

* internal service-to-service auth
* HMAC alapu hitelesites
* nonce alapu replay vedelem

Mit kell modositani:

* `ServiceId`
* `Secret`
* `Roles`
* `AllowedRealmIds`

Fontos:

* a RealmCore schema sync-hez a szolgaltatasnak kelleni fog a `realm-schema.read` role is
* ezt productionben add hozza annal a service identity-nel, amelyik a schema endpointokat hivja

Javasolt pelda:

```json
"Roles": [
  "official-realm-heartbeat.write",
  "realm-gamedata.read",
  "realm-schema.read"
]
```

---

## 7.7 `Admin`

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

* admin endpointok vedelme

Mit kell modositani:

* `ApiKey`
* `AllowedIpAddresses`

Javaslat:

* az admin API csak localhostrol vagy fix admin IP-rol legyen elerheto

---

## 7.8 `Security`

Pelda:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ]
}
```

Mit csinal:

* HTTPS policy
* megbizhato proxyk

Mit kell modositani:

* ha az Nginx ugyanazon a gepen fut, a `127.0.0.1` jo
* ha kulon proxy gep lesz, annak az IP-je kell ide

---

## 7.9 `Cors`

Pelda:

```json
"Cors": {
  "AllowedOrigins": []
}
```

Mit csinal:

* bongeszo alapu eleresek CORS policy-ja

Megjegyzes:

* ha ures, a kod fallback origin-t allit be
* Unity kliensnel a CORS tipikusan kevesbe fontos, de ha lesz webes admin vagy launcher frontend, itt kell megadni az origin-eket

---

## 7.10 `SchemaDelivery`

Pelda:

```json
"SchemaDelivery": {
  "Enabled": true,
  "DirectoryPath": "SchemaMigrations/realmcore",
  "Channel": "realmcore"
}
```

Mit csinal:

* megmondja, honnan olvassa az Auth API a migration fajlokat
* ez a schema manifest es migration content endpoint forrasa

Mit kell modositani:

* ha nem az alap mappaban akarod tarolni a migrationokat
* ha kesobb kulon csatornat vezetsz be

Fontos:

* ez jelenleg **mappa alapon mukodik**
* nem kell minden migrationhoz uj kodot irni
* eleg az uj `*.mysql` fajlt a megfelelo mappaba rakni

Ha relativ path:

* a mappa a publisholt alkalmazas `ContentRootPath`-jahoz kepest lesz ertelmezve

Tehat productionben a default path jellemzoen ezt jelenti:

```text
/opt/gnosis/authapi/app/SchemaMigrations/realmcore
```

---

## 8. JWT RSA kulcsok

Ez a projekt RSA kulcspaar alapjan ir ala JWT tokeneket.

A fajlok:

```text
/opt/gnosis/authapi/app/keys/auth_private.pem
/opt/gnosis/authapi/app/keys/auth_public.pem
```

A `FileRsaKeyProvider` ezeket tolti be indulaskor.

Ha a fajlok nem leteznek, az alkalmazas nem indul el.

Pelda letrehozas:

```bash
mkdir -p /opt/gnosis/authapi/app/keys
openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

## 9. Elso telepites Ubuntu VPS-en

Ez a README abból indul ki, hogy:

* a publish outputot a sajat gepedrol toltesz fel
* a source kodot nem telepited a szerverre
* az AuthApi `app` mappaba kerul

## 9.1 Mappak letrehozasa

```bash
sudo mkdir -p /opt/gnosis/authapi/app
sudo mkdir -p /opt/gnosis/authapi/app/keys
sudo mkdir -p /opt/gnosis/authapi/app/logs
sudo mkdir -p /opt/gnosis/authapi/app/SchemaMigrations/realmcore
```

## 9.2 Publish output feltoltese

A Visual Studio publish output **tartalmat** toltsd fel ide:

```text
/opt/gnosis/authapi/app
```

Nem a publish mappat mint almappat, hanem a publish mappa belso fajljait.

## 9.3 Futtathato jog

Ha kell:

```bash
chmod +x /opt/gnosis/authapi/app/GnosisAuthServer
```

## 9.4 MySQL adatbazis letrehozasa

A `bootstrap.sql` fajlt kulon toltsd fel peldaul ide:

```text
/opt/gnosis/authapi/bootstrap.sql
```

Majd hozd letre az adatbazist es a usert MySQL-ben, utana futtasd le a schema-t.

## 9.5 appsettings.Production.json

Hozd letre itt:

```text
/opt/gnosis/authapi/app/appsettings.Production.json
```

Es production ertekekkel ird felul a default configot.

## 9.6 JWT kulcsok letrehozasa

Generalj RSA PEM kulcsokat az elozo fejezet szerint.

## 9.7 Kezi inditas

A jelenlegi publish formatumodnal a helyes inditas:

```bash
cd /opt/gnosis/authapi/app
ASPNETCORE_ENVIRONMENT=Production ./GnosisAuthServer
```

## 9.8 Ellenorzes

Peldaul:

```bash
curl http://127.0.0.1:5158/health/live
curl http://127.0.0.1:5158/health/ready
```

Ha Nginx mogott mar megy:

```bash
curl https://auth.te-domainod.hu/health/live
curl https://auth.te-domainod.hu/health/ready
```

---

## 10. systemd service

Javasolt `systemd` service.

Pelda:

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

Fontos:

* a binary kozvetlenul fut
* nem `dotnet ...dll`

---

## 11. Nginx reverse proxy

Javasolt topologia:

* Auth API csak `127.0.0.1:5158`
* Nginx fogadja a publikus 80/443 forgalmat
* Nginx tovabbit a helyi Auth API fele

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

## 12. Rate limiting

A kod alapjan a projekt rate limitinget hasznal.

A jelenlegi policy-k:

* `login`
* `realm-list`
* `official-heartbeat`
* `realm-gamedata-read`
* `admin-write`

Ez fontos vedelmi reteg brute-force, flood vagy hibas belso integracio ellen.

---

## 13. Security modell roviden

A jelenlegi AuthApi security modellje:

* Steam ticket validacio public login endpointnal
* RSA alapu JWT token kiadas
* internal HMAC service auth
* nonce alapu replay vedelem
* admin header + IP whitelist
* rate limiting
* loopback bind javasolt
* reverse proxy mogotti uzem
* health endpointok kulon
* MySQL kapcsolat kotelezo

Fontos:

* a `CHANGE_ME` placeholder ertekeket production elott mindig cserelni kell
* a private key soha ne keruljon repoba
* a service secret-ek ne maradjanak defaulton
* az admin API kulcs legyen eros
* a schema migration endpoint maradjon internal

---

## 14. Schema delivery mukodes

A jelenlegi schema delivery modell:

1. az AuthApi figyeli a `SchemaMigrations/realmcore` mappat
2. beolvassa a `*.mysql` fajlokat
3. checksumot szamol rajuk
4. a manifest endpoint visszaadja a migration listat
5. a migration endpoint visszaadja az egyes migrationok tartalmat
6. a RealmCore ezt hasznalja a sajat DB schema frissitesehez

Ez azt jelenti, hogy uj migrationhoz eleg:

* uj `*.mysql` fajlt letrehozni a megfelelo mappaban
* nem kell hozza uj endpointot irni
* nem kell a kodban uj migration class

### Fontos naming szabaly

Javasolt fajlnev:

```text
0001_initial_schema.mysql
0002_add_zone_tables.mysql
0003_drop_old_column.destructive.mysql
```

A destructive migrationokat a kod fajlnev alapjan ismeri fel.

---

## 15. Leggyakoribb hibak

### `JWT private key file was not found`

Oka:

* nincs PEM fajl
* rossz path van a configban

### `Access denied for user ...`

Oka:

* rossz MySQL user
* rossz jelszo
* `localhost` vs `127.0.0.1` eltérés

### `Address already in use`

Oka:

* a portot mar fogja egy masik process vagy systemd service

### `401` internal endpointon

Oka:

* hianyzik a service auth header
* rossz HMAC secret
* rossz timestamp / nonce
* nincs megfelelo role

### `403` internal endpointon

Oka:

* a service identity be van engedve, de nincs meg a szukseges role

### `ready` health check nem jo

Oka:

* nincs adatbazis kapcsolat
* nincs lefuttatva a bootstrap schema
* hibas DB connection string

---

## 16. Production checklist

Telepites elott:

* csereld a `CHANGE_ME` ertekeket
* generalj RSA kulcspárt
* allitsd be a valos Steam AppId es PublisherKey ertekeket
* allitsd be a valos admin API kulcsot
* add hozza a helyes service role-okat
* ellenorizd a `SchemaDelivery` mappat es fajlneveket
* tedd az AuthApi-t Nginx moge
* engedelyezd a systemd service-t
* teszteld a `live` es `ready` endpointot

---

## 17. Gyors osszefoglalo

A `GnosisAuthServer` jelenlegi allapotaban:

* kezeli a public Steam login flow-t
* RSA JWT-t ad ki
* listazza a realmeket
* fogadja az official heartbeatet
* tarolja a canonical GameData-t
* admin endpointokat ad
* internal schema manifestet es migration tartalmat ad a RealmCore-nak
* systemd + Nginx mogotti Linuxos uzemre van tervezve

A projekt telepitesenek lenyege:

1. publish output feltoltese az `app` mappaba
2. MySQL adatbazis letrehozasa
3. bootstrap schema lefuttatasa
4. RSA kulcsok generalasa
5. `appsettings.Production.json` beallitasa
6. binary inditas vagy systemd service
7. Nginx reverse proxy
8. health endpoint ellenorzes

Ez a README csak a **GnosisAuthServer** projektre vonatkozik.
