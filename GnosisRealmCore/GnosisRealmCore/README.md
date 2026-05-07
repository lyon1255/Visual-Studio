# GnosisRealmCore README

## 1. Mi ez a projekt?

A `GnosisRealmCore` a Gnosis Online rendszer realm szintu backend szolgaltatasa.

Ez a projekt a kovetkezokert felel:

* karakter adatok kezeles
* realm szintu adatbazis muveletek
* zone lookup es zone inditas logika
* node allapot es zone allapot nyilvantartas
* global GameData lekerese az AuthApi-bol
* global GameData cache es kesobbi realm override merge alap
* official realm heartbeat kuldese az AuthApi fele
* schema migration futtatasa a sajat realm adatbazison
* kesobbi community realm tamogatas alapjainak biztositasa

A RealmCore **nem** global auth szerver, es **nem** maga a jatekszerver.

A rendszerben a szerepek logikailag igy neznek ki:

* **GnosisAuthServer**

  * global auth
  * canonical GameData
  * schema manifest es migration content source
  * realm lista
* **GnosisRealmCore**

  * realm adatkezeles
  * save/load
  * zone es node orchestration alap
  * global GameData cache
  * remote schema migration vegrehajtas
* **Gnosis Node Agent**

  * zone processzek inditasa es felugyelete
* **Game Server**

  * konkret runtime gameplay

A RealmCore a sajat MySQL adatbazisat kezeli.
A schema valtozasokat **nem helyi SQL fajlokbol** olvassa, hanem az AuthApi-bol keri le, majd a **sajat adatbazisara** hajtja vegre.

Ez nagyon fontos.

---

## 2. Mit csinal a RealmCore?

A RealmCore a realm "agya".

Feladatai:

### 2.1 Save/load

A RealmCore tarolja es betolti a realmhez tartozo adatokat, peldaul:

* karakterek
* inventory
* quest allapot
* guild vagy kesobbi social adatok
* zone utolso helye
* egyeb realm-specifikus progresszio

### 2.2 Zone lookup es inditas

Ha egy kliens vagy egy szerver egy adott zonat akar elerni, a RealmCore:

* megnezi, fut-e mar a zona
* ha fut, visszaadja az IP/port adatokat
* ha nem fut, sorba allit egy node commandot, hogy a NodeAgent inditsa el

### 2.3 Node es zone allapot

A RealmCore nyilvantartja:

* mely node online
* mely zona fut
* melyik node-on fut
* mikor jott utolso heartbeat

### 2.4 Global GameData cache

A RealmCore az AuthApi-bol lekerni a globalis GameData-t, es memoriaban tarolja.

Ez a kesobbi modell alapja:

* AuthApi = canonical source
* RealmCore = cache + merge pont
* GameServer = a RealmCore-tol kapja a vegleges adatot

### 2.5 Remote schema migration

A RealmCore indulaskor:

* lekerni az AuthApi-bol a schema manifestet
* megnezni, mi futott mar le a sajat DB-jeben
* letolteni a hianyzo migrationokat
* checksumot ellenorizni
* lefuttatni oket a sajat realm adatbazisan
* eltárolni, hogy mi futott mar le

### 2.6 Heartbeat az AuthApi fele

Official realm modban a RealmCore periodikusan heartbeatet kuld az AuthApi-nak, hogy:

* a realm latszodjon online-nak
* frissuljon a realm status
* frissuljon a jatekos letszam
* frissuljon a healthy zone szam

---

## 3. High-level mukodesi elv

A RealmCore a kovetkezo kapcsolatokkal dolgozik.

### 3.1 RealmCore -> AuthApi

A RealmCore innen ker le:

* global GameData version
* global GameData snapshot
* schema manifest
* migration tartalom

Es ide kuld:

* official heartbeat

### 3.2 NodeAgent -> RealmCore

A NodeAgent a RealmCore-val beszel:

* node heartbeat
* zone heartbeat
* command polling vagy command execute flow
* zone status frissites

### 3.3 GameServer -> RealmCore

A GameServer a RealmCore-hoz fordul:

* karakter adatert
* save muveletekert
* zone adatokert
* GameData snapshotert vagy kesobbi merged snapshotert

### 3.4 Admin / toolok -> RealmCore

A RealmCore kesobb admin vagy belso toolok fele is biztosithat endpointokat:

* karakter admin
* zone status
* node status
* override kezeles
* community realm konfiguracio

---

## 4. Community-friendly mukodes

A RealmCore ugy lett tervezve, hogy official es community realmhez is jo alap legyen.

A fo kulonbseg:

### Official realm

* AuthApi heartbeat bekapcsolva
* official realm lista integration
* kozponti canonical GameData hasznalat
* kozpontilag kiosztott schema migration
* szigorubb security policy

### Community realm

* ugyanaz a RealmCore futtathato
* sajat realm adatbazissal
* sajat configgal
* sajat override-okkal
* kesobb sajat custom item/entity definiciokkal
* de ugyanazt a migration es GameData alapmodellt hasznalhatja

A rendszer celja, hogy ugyanaz a szoftver tudjon:

* official realmkent futni
* single-box community realmkent futni
* split deploymentben is futni

---

## 5. Fajl- es mappaszerkezet

A telepiteshez **nem kell source kod**.

A telepiteshez a publish output kell.

Javasolt szerkezet:

```text
/opt/gnosis/realmcore/
  app/
    GnosisRealmCore
    GnosisRealmCore.pdb
    appsettings.json
    appsettings.Development.json
    appsettings.Production.json
    logs/
```

A RealmCore jelenlegi publish formaja Linuxon jellemzoen egy kozvetlenul futtathato binaris:

```text
GnosisRealmCore
```

Ez azt jelenti, hogy az inditas altalaban:

```bash
./GnosisRealmCore
```

nem pedig `.dll`-es forma.

---

## 6. A projekt fo reszei

## 6.1 `Program.cs`

Az alkalmazas belepesi pontja.

Feladata:

* konfiguracio betoltese
* adatbazis kapcsolat beallitasa
* service-ek regisztralasa
* forwarded headers beallitasa
* CORS beallitasa
* migration sync inditasa
* GameData cache warm-up inditasa
* controller pipeline inditasa

## 6.2 `Data/RealmDbContext.cs`

A realm adatbazis fo DbContext-je.

Feladata:

* tablák elerese
* EF Core mapping
* adatbazis muveletek

## 6.3 `Services/AuthApiClient.cs`

Az AuthApi-val valo kommunikacioert felel.

Feladata:

* global GameData version lekerese
* global GameData snapshot lekerese
* official heartbeat kuldese
* schema manifest lekerese
* migration tartalom lekerese

## 6.4 `Services/SchemaMigrationService.cs`

A remote schema migration vegrehajto.

Feladata:

* schema manifest lekerese AuthApi-bol
* hianyzo migrationok meghatarozasa
* migration checksum ellenorzes
* migration SQL futtatasa a sajat realm DB-n
* `schema_migrations` tabla karbantartasa

Fontos:

* nem helyi migration fajlokat olvas
* az AuthApi a migration source

## 6.5 `Services/GameDataCacheService.cs`

A global GameData cache service.

Feladata:

* AuthApi-bol global GameData lehuzasa
* memoriaban tarolas
* kesobbi merge alap official/community realmhez

## 6.6 `Services/ZoneOrchestrationService.cs`

Zone lookup es start logika.

Feladata:

* megnezni, fut-e egy zona
* ha fut, visszaadni az IP/portot
* ha nem fut, commandot sorba allitani a node szamara
* zone heartbeat alapjan allapotot frissiteni

## 6.7 `Services/CharacterService.cs`

Karakter adatok kezelesere szolgalo service.

Feladata:

* karakterek betoltese
* karakterek mentese
* karakterhoz kapcsolodo realm adatok kezelese

## 6.8 `Infrastructure/HmacServiceRequestAuthenticator.cs`

Service-to-service auth a belso endpointokhoz.

Feladata:

* HMAC alapu request hitelesites
* nonce alapjan replay vedelem
* service identity ellenorzes

## 6.9 `Infrastructure/MemoryNonceStore.cs`

Nonce tarolas replay vedelemhez.

## 6.10 `Infrastructure/JwtTokenValidator.cs`

A kliens altal hozott JWT access token ellenorzese.

## 6.11 `Infrastructure/LegacyNodeApiKeyValidator.cs`

Atmeneti vagy legacy node auth megoldas, ha a NodeAgent meg nem allt at teljes HMAC/service auth modellre.

## 6.12 `Options/`

A konfiguracios osztalyok.

Fo fajlok:

* `DatabaseOptions.cs`
* `AuthApiOptions.cs`
* `JwtValidationOptions.cs`
* `ServiceAuthOptions.cs`
* `LegacyNodeAuthOptions.cs`
* `SecurityOptions.cs`
* `CorsOptions.cs`
* `RealmOptions.cs`
* `SchemaMigrationOptions.cs`
* `GameDataCacheOptions.cs`

## 6.13 `Models/`

Request/response modellek es schema/GameData contractok.

Kulonosen fontos:

* `SchemaContracts.cs`
* zone response modellek
* heartbeat request modellek
* GameData response modellek

## 6.14 `Controllers/`

A konkret HTTP endpointok.

A pontos controller nev a projekt aktualis allapotatol fuggoen valtozhat, de logikailag ezek a blokkok varhatok:

* karakter endpointok
* zone lookup endpointok
* node heartbeat endpointok
* zone heartbeat endpointok
* health endpointok
* esetleg GameData vagy admin endpointok

---

## 7. appsettings konfiguracio leirasa

A projekt a kovetkezo forrasokbol olvas konfiguraciot:

* `appsettings.json`
* `appsettings.{Environment}.json`
* `GNOSIS_REALM_` prefixu environment valtozok

Productionben a legfontosabb fajl:

```text
/opt/gnosis/realmcore/app/appsettings.Production.json
```

---

## 7.1 `Database`

Pelda:

```json
"Database": {
  "ConnectionString": "Server=127.0.0.1;Port=3306;Database=gnosis_realm01;User=gnosis_realm;Password=CHANGE_ME;SslMode=Preferred;"
}
```

Mit csinal:

* ez a RealmCore sajat MySQL kapcsolata

Mit kell modositani:

* adatbazis neve
* user
* jelszo
* SSL policy

Nagyon fontos:

* a RealmCore sajat adatbazist hasznal
* a migrationokat erre az adatbazisra futtatja le

---

## 7.2 `AuthApi`

Pelda:

```json
"AuthApi": {
  "BaseUrl": "https://auth.playgnosis.hu",
  "ServiceId": "official-eu-realm-core",
  "ServiceSecret": "CHANGE_ME_REALMCORE_SHARED_SECRET"
}
```

Mit csinal:

* innen eri el a RealmCore az AuthApi-t

Mit kell modositani:

* `BaseUrl`
* `ServiceId`
* `ServiceSecret`

Ez kulcsfontossagu, mert a RealmCore ezen keresztul keri le:

* global GameData-t
* schema manifestet
* migration tartalmat
* ide kuld heartbeatet is

---

## 7.3 `JwtValidation`

Pelda:

```json
"JwtValidation": {
  "Issuer": "Gnosis.Auth",
  "Audience": "Gnosis.Clients",
  "PublicKeyPemPath": "./keys/auth_public.pem",
  "ClockSkewSeconds": 30
}
```

Mit csinal:

* a kliens access token ellenorzesere szolgalo beallitasok

Mit kell modositani:

* a publikus kulcs eleresi utja
* issuer
* audience

Fontos:

* itt csak a **publikus kulcs** kell
* a privat kulcs az AuthApi-n marad

---

## 7.4 `ServiceAuth`

Pelda:

```json
"ServiceAuth": {
  "Enabled": true,
  "AllowedClockSkewSeconds": 30,
  "NonceTtlSeconds": 90,
  "Clients": [
    {
      "ServiceId": "node-agent-01",
      "Secret": "CHANGE_ME_NODE_SECRET",
      "Roles": [ "node-heartbeat.write", "zone-heartbeat.write", "zone-command.read" ],
      "AllowedRealmIds": [ "official-eu-1" ]
    }
  ]
}
```

Mit csinal:

* belso service-to-service auth
* nodeok, belso szolgaltatasok, es egyeb komponensek hitelesitese

Mit kell modositani:

* service ID-k
* secret-ek
* role-ok
* allowed realm ID-k

---

## 7.5 `LegacyNodeAuth`

Pelda:

```json
"LegacyNodeAuth": {
  "Enabled": false,
  "HeaderName": "X-Legacy-Node-Key",
  "ApiKeys": []
}
```

Mit csinal:

* atmeneti node auth tamogatas
* csak akkor hasznald, ha a NodeAgent meg nem allt at az uj modellre

Javaslat:

* ha lehet, ezt kapcsold ki
* csak atmeneti kompatibilitasi celra hasznald

---

## 7.6 `Security`

Pelda:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ],
  "KnownIPNetworks": []
}
```

Mit csinal:

* HTTPS policy
* trusted proxy lista
* forwarded headers kezeles

Mit kell modositani:

* ha ugyanazon a gepen van Nginx, a `127.0.0.1` jo
* ha kulon reverse proxy van, annak IP-je kell ide

Fontos:

* a `KnownIPNetworks` az uj forma
* a regi `KnownNetworks` mar ne legyen benne

---

## 7.7 `Cors`

Pelda:

```json
"Cors": {
  "AllowedOrigins": []
}
```

Mit csinal:

* browser alapu kliensek CORS policy-ja

Megjegyzes:

* ha nincs webes frontend, ez kevesbe fontos
* ha lesz admin web UI vagy launcher oldal, annak origin-jeit itt kell megadni

---

## 7.8 `Realm`

Pelda:

```json
"Realm": {
  "RealmId": "official-eu-1",
  "RealmName": "Gnosis Official EU",
  "Region": "EU",
  "HeartbeatIntervalSeconds": 30,
  "ZoneStartupPollSeconds": 15
}
```

Mit csinal:

* a realm alap metadata-ja
* heartbeat periodus
* zone startup poll timeout

Mit kell modositani:

* realm ID
* realm nev
* regio
* timing beallitasok

---

## 7.9 `SchemaMigration`

Pelda:

```json
"SchemaMigration": {
  "Enabled": true,
  "AllowDestructiveMigrations": false,
  "Channel": "realmcore"
}
```

Mit csinal:

* engedelyezi a remote schema sync-et
* destructive migration guard
* logikai channel nev

Mit kell modositani:

* altalaban csak `AllowDestructiveMigrations`, ha tenyleg kell

Fontos:

* itt mar nincs `DirectoryPath`
* a RealmCore nem helyi migration fajlokbol dolgozik

---

## 7.10 `GameDataCache`

Pelda:

```json
"GameDataCache": {
  "Enabled": true,
  "RefreshIntervalSeconds": 300,
  "WarmOnStartup": true
}
```

Mit csinal:

* GameData cache viselkedese
* refresh periodus
* startup warm-up

Mit kell modositani:

* ha ritkabban vagy surubben akarod frissiteni a cache-t

---

## 8. Remote schema migration modell

Ez az egyik legfontosabb resz.

A RealmCore **nem helyi SQL fajlokat** olvas.
Ahelyett ezt csinalja:

1. indulaskor lekeri az AuthApi-bol a schema manifestet
2. megnézi a sajat `schema_migrations` tablajat
3. ami migration hianyzik, azt egyenkent lekeri az AuthApi-bol
4. checksumot ellenoriz
5. lefuttatja a sajat MySQL adatbazisan
6. beirja a `schema_migrations` tablaba

Ez azt jelenti, hogy:

* a migration source az AuthApi
* a migration executor a RealmCore
* a sajat DB ownership a RealmCore-nal marad

Ez a helyes modell official es community realmhez is.

---

## 9. Global GameData cache modell

A GameData logika jelenleg ugy van kitalalva, hogy:

* AuthApi = canonical source
* RealmCore = cache + merge pont
* GameServer = RealmCore-tol kap vegleges adatot

A jelenlegi allapotban a RealmCore:

* le tudja kerni a global GameData snapshotot
* cache-elni tudja
* startupkor warmolni tudja a cache-t

Kesobb erre lehet rahuzni:

* realm override-ok
* disable / tombstone logika
* custom item/entity definiciok
* community specifikus merge

---

## 10. Elso telepites Ubuntu VPS-en

Ez a README abbol indul ki, hogy:

* a publish outputot sajat gepedrol feltoltod
* a source kodot nem teszed fel
* a RealmCore sajat MySQL adatbazist hasznal
* az AuthApi mar mukodik

## 10.1 Mappak letrehozasa

```bash
sudo mkdir -p /opt/gnosis/realmcore/app
sudo mkdir -p /opt/gnosis/realmcore/app/logs
```

## 10.2 Publish output feltoltese

A publish output **tartalmat** toltsd fel ide:

```text
/opt/gnosis/realmcore/app
```

## 10.3 Futtathato jog

Ha kell:

```bash
chmod +x /opt/gnosis/realmcore/app/GnosisRealmCore
```

## 10.4 MySQL telepitese

Ha meg nincs:

```bash
sudo apt-get update
sudo apt-get install -y mysql-server
```

## 10.5 Realm adatbazis letrehozasa

Lepj be MySQL-be:

```bash
sudo mysql
```

Majd pelda:

```sql
CREATE DATABASE gnosis_realm01 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE USER 'gnosis_realm'@'127.0.0.1' IDENTIFIED BY 'EROS_DB_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_realm01.* TO 'gnosis_realm'@'127.0.0.1';

CREATE USER 'gnosis_realm'@'localhost' IDENTIFIED BY 'EROS_DB_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_realm01.* TO 'gnosis_realm'@'localhost';

FLUSH PRIVILEGES;
EXIT;
```

Megjegyzes:

* `localhost` es `127.0.0.1` kulon kezeles sok hibat megelőz

## 10.6 JWT public key

Ha a RealmCore validalja a kliens access tokent, szuksege lesz az AuthApi publikus kulcsara.

Masold at az AuthApi publikus kulcsat peldaul ide:

```text
/opt/gnosis/realmcore/app/keys/auth_public.pem
```

## 10.7 appsettings.Production.json

Hozd letre itt:

```text
/opt/gnosis/realmcore/app/appsettings.Production.json
```

Es allitsd be:

* sajat DB connection string
* AuthApi URL
* AuthApi service secret
* JWT public key path
* realm metadata
* schema migration settings

## 10.8 Kezi inditas

A jelenlegi publish forma szerint:

```bash
cd /opt/gnosis/realmcore/app
ASPNETCORE_ENVIRONMENT=Production ./GnosisRealmCore
```

Indulaskor a kovetkezok tortennek:

* DB kapcsolat felall
* remote schema sync lefut
* global GameData cache warm-up lefut
* API endpointok felallnak

---

## 11. systemd service

Javasolt `systemd` service.

Pelda:

```ini
[Unit]
Description=Gnosis Realm Core
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

---

## 12. Nginx reverse proxy

Ha a RealmCore HTTP API-jat publikalni akarod domainen keresztul, akkor Nginx moge tedd.

Pelda topologia:

* RealmCore helyben hallgat, peldaul `127.0.0.1:5159`
* Nginx fogadja a publikus kero forgalmat
* Nginx tovabbit RealmCore fele

Pelda host:

```nginx
server {
    server_name realm.example.com;

    location / {
        proxy_pass http://127.0.0.1:5159;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
    }

    listen 443 ssl;
    ssl_certificate /etc/letsencrypt/live/realm.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/realm.example.com/privkey.pem;
}
```

Megjegyzes:

* ha a RealmCore csak belso backend, akkor nem kell feltetlen publikus domain ala rakni
* ha viszont a kliens vagy a GameServer HTTP-n eri el, akkor kellhet

---

## 13. Leggyakoribb hibak

### `Access denied for user ...`

Oka:

* rossz DB user
* rossz jelszo
* `localhost` vs `127.0.0.1`

### `401` az AuthApi hivaskor

Oka:

* rossz `ServiceId`
* rossz `ServiceSecret`
* rossz HMAC signature
* rossz timestamp / nonce

### `403` a schema endpointon

Oka:

* az AuthApi oldalon a RealmCore service identity-hez nincs hozzaadva a `realm-schema.read` role

### `JWT validation` hiba

Oka:

* rossz public key path
* nem egyezik issuer / audience
* rossz token

### `Address already in use`

Oka:

* ugyanazon a porton mar fut masik process vagy service

### startup migration hiba

Oka:

* az AuthApi nem ad manifestet
* az AuthApi migration checksum nem egyezik
* destructive migration tiltva van
* a migration SQL hibas

---

## 14. Production checklist

Telepites elott:

* legyen mukodo AuthApi
* add hozza a RealmCore service identity-t az AuthApi `ServiceAuth` configjahoz
* legyen benne a `realm-schema.read` role
* legyen benne a `realm-gamedata.read` role
* official realm esetben legyen heartbeat jogosultsag is
* hozd letre a realm DB-t
* allitsd be a DB usert
* masold at az AuthApi publikus kulcsat
* allitsd be az `appsettings.Production.json`-t
* teszteld kezi inditassal
* csak utana rakd `systemd` ala

---

## 15. Gyors osszefoglalo

A `GnosisRealmCore` a realm szintu backend szolgaltatas.

Feladata:

* save/load
* zone es node orchestration alap
* AuthApi integration
* global GameData cache
* remote schema migration vegrehajtas
* official heartbeat kuldes
* community-friendly realm alap biztositas

A legfontosabb tervezesi elvek:

* a RealmCore sajat MySQL adatbazist hasznal
* a migration forrasa az AuthApi
* a migration vegrehajtasa a RealmCore feladata
* a GameData canonical source az AuthApi
* a RealmCore cache-el es kesobb merge-el
* a GameServer a RealmCore-tol kap adatot
