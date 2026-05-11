# GnosisAuthServer README

## 1. Dokumentum celja

Ez a dokumentum a `GnosisAuthServer` modul muszaki leirasa. A dokumentacio egy nagyobb MMO backend keretrendszer reszekent kezeli az Auth API-t, nem elszigetelt szolgaltataskent.

A dokumentum celja:

- a modul felelossegi korenek pontos rogzitese
- a rendszerbeli adat- es forgalomutak leirasa
- a szerveroldali uzemeltetes szabalyainak dokumentalasa
- a MySQL adatszerkezet attekintese
- a production inditas es karbantartas folyamatanak standardizalasa
- az elso inditasi folyamat egyertelmu, lepesrol lepesre torteno leirasa

Ez a dokumentum az Auth API modulra vonatkozik. A Realm, Node Agent, Zone Server es kliensoldali rendszerek kulon dokumentaciot igenyelnek.

---

## 2. Modul azonositas

**Modul neve:** GnosisAuthServer  
**Szerepkor:** Globalis Auth API  
**Funkcio:** hitelesites, JWT token kiadas, realm lista kiszolgalas, canonical GameData kiszolgalas, schema delivery, admin vezerles  
**Uzemi szint:** magas bizalmi szintu backend komponens

---

## 3. Architektura attekintes

### 3.1 A modul celja es felelossegi kore

A `GnosisAuthServer` a globalis backend egyik kozponti szolgaltatasa. A modul feladata nem a jateklogika futtatasa, hanem a globalis, kozos backend funkciok biztositas.

A modul fo felelossegi korei:

1. **Jatekos hitelesites**
   - Steam ticket alapu login
   - account letrehozas vagy frissites
   - JWT access token kiadas

2. **Realm lista kiszolgalas**
   - a kliens szamara lathato realm lista visszaadasa
   - realm metadata kozos tarolasa
   - official/community allapot kozponti kezelese

3. **Internal service integracio**
   - RealmCore heartbeat fogadas
   - service-to-service HMAC auth
   - replay vedelem nonce alapon

4. **Canonical GameData forras**
   - globalis item, entity, quest, spell, aura adatok tarolasa
   - global GameData snapshot kiszolgalasa a realm komponensek fele

5. **Schema delivery**
   - migration manifest kiszolgalasa
   - migration tartalom kiszolgalasa a RealmCore fele

6. **Admin vezerles**
   - realm admin muveletek
   - GameData csere
   - account ban/unban muveletek

7. **Uzemi health ellenorzes**
   - liveness
   - readiness

---

### 3.2 A modul helye a globalis MMO rendszerben

A `GnosisAuthServer` nem helyettesiti a jatekszervert, es nem helyettesiti a realm oldali allapotkezelo komponenseket.

A magas szintu rendszerkapcsolatok:

- **Client -> Auth API**
  - login
  - JWT megszerzese
  - realm lista
  - account adatok

- **RealmCore -> Auth API**
  - heartbeat
  - GameData lekerdezes
  - schema manifest lekerdezes
  - migration lekerdezes

- **Admin -> Auth API**
  - protected admin endpointok
  - realm metadata modositas
  - account ban/unban
  - GameData csere

- **Node Agent / Zone Server**
  - direktben nem ez a modul vezerli a zone processzeket
  - az Auth API csak a globalis auth es konfiguracios forras szerepet latja el

---

### 3.3 Adat- es forgalomutak

#### 3.3.1 Public auth flow

1. A kliens Steam ticketet kuld az Auth API-nak.
2. Az Auth API ellenorzi a ticketet.
3. Az Auth API letrehozza vagy frissiti az accountot.
4. Az Auth API JWT access tokent ad vissza.
5. A kliens ezzel a tokennel eri el a bearer vedett endpointokat.

#### 3.3.2 Realm registration / heartbeat flow

1. A RealmCore HMAC-alairt kerest kuld az internal heartbeat endpointnak.
2. Az Auth API ellenorzi:
   - service identity
   - timestamp
   - nonce
   - body hash
   - signature
3. Az Auth API ellenorzi, hogy az adott service mely realmeket kezelheti.
4. Az Auth API frissiti a realm runtime allapotat.
5. Az official/community metadata nem valtozik heartbeat alapjan.

#### 3.3.3 GameData flow

1. A RealmCore vagy admin endpoint lekerdezi az aktualis GameData verziojat vagy snapshotjat.
2. Az Auth API az aktiv GameData allapotot adja vissza.
3. Az admin csere eseten uj GameData allapot kerul be a rendszerbe.
4. A RealmCore a globalis adatot cache-elheti es merge-elheti sajat override-okkal.

#### 3.3.4 Schema delivery flow

1. A RealmCore lekri a migration manifestet.
2. Az Auth API visszaadja az elerheto migrationok listajat.
3. A RealmCore migration azonosito alapjan lekri a konkret migration tartalmat.
4. A RealmCore a sajat adatbazisan hajtja vegre a valtozast.

#### 3.3.5 Admin flow

1. Az admin kliens HMAC vedett admin kerest kuld.
2. Az Auth API ellenorzi:
   - admin IP vagy halozati allowlist
   - timestamp
   - nonce
   - body hash
   - signature
3. Sikeres auth utan az admin muvelet vegrehajtasra kerul.
4. Account ban/unban eseten az account access cache invalidalodik.

---

## 4. Halozati reteg

### 4.1 Portok es szerepkoruk

Javasolt topologia:

- **Kestrel local bind:** `127.0.0.1:5158`
- **Public TLS endpoint:** Nginx vagy mas reverse proxy a 443-as porton
- **HTTP 80:** opcionalis redirect vagy ACME challenge celra

Az Auth API kozvetlenul ne legyen publikusan kitett plain HTTP endpointkent.

---

### 4.2 SSL es HTTPS kovetelmenyek

A modul production uzemben HTTPS-kotelezett.

Kovetelmenyek:

- a publikus forgalmat reverse proxy fogadja
- a proxy allitja be a megfelelo forwarded headereket
- az Auth API oldalan `RequireHttps=true`
- productionben a startup guard megkoveteli a HTTPS policy aktiv allapotat

---

### 4.3 Hasznalt HTTP metodusok

A rendszer alapvetoen ezeket a metodusokat hasznalja:

- `GET`
  - lekerdezesek
  - health endpointok
  - realm lista
  - GameData es schema olvasas

- `POST`
  - login
  - heartbeat
  - GameData csere

- `PUT`
  - admin update muveletek
  - realm modositas
  - account ban/unban

---

### 4.4 Trust boundary-k

A rendszer negy kulonbozo halozati bizalmi retegben gondolkodik:

1. **Public client traffic**
   - legalacsonyabb bizalmi szint
   - JWT elott nyitott login endpoint
   - bruteforce vedelmet igenyel

2. **Bearer protected client traffic**
   - ervenyes JWT kell
   - account access check tovabbra is kotelezo

3. **Internal service traffic**
   - HMAC service auth
   - nonce replay vedelem
   - service ownership ellenorzes

4. **Admin traffic**
   - HMAC admin auth
   - IP vagy CIDR allowlist
   - erosen korlatozott halozati eleres

---

## 5. Parancsok, endpointok es funkcionalis katalogus

### 5.1 Public auth endpointok

#### `POST /api/auth/steam`
Feladata:
- Steam ticket fogadasa
- account azonositas
- token kiadas

Valasz:
- JWT access token
- account metadata

#### `GET /api/auth/me`
Feladata:
- az aktualis bearer felhasznalo adatainak visszaadasa

Ellenorzes:
- JWT ervenyesseg
- account access validator

#### `GET /api/auth/servers`
Feladata:
- a kliensnek szant realm lista visszaadasa

Alias:
- `GET /api/realms`

---

### 5.2 Internal realm endpointok

#### `POST /api/internal/realms/heartbeat`
Feladata:
- realm runtime allapot frissitese

Frissitett mezok:
- status
- current_players
- max_players
- healthy_zone_count
- last_heartbeat_at

Nem frissitheto heartbeatbol:
- official/community status
- display name
- region
- listed status
- enabled status

---

### 5.3 Internal GameData endpointok

#### `GET /api/internal/gamedata/version`
Feladata:
- aktualis GameData verzio metadata visszaadasa

#### `GET /api/internal/gamedata/snapshot`
Feladata:
- teljes global GameData snapshot visszaadasa

#### `GET /api/internal/gamedata/prefabs`
Feladata:
- prefab vagy prefab-jellegu metadata visszaadasa

Aliasok:
- `GET /api/gamedata/version`
- `GET /api/gamedata/snapshot`
- `GET /api/gamedata/prefabs`

---

### 5.4 Internal schema endpointok

#### `GET /api/internal/schema/manifest`
Feladata:
- migration manifest visszaadasa

#### `GET /api/internal/schema/migrations/{migrationId}`
Feladata:
- adott migration tartalmanak visszaadasa

---

### 5.5 Admin endpointok

#### `GET /api/admin/realms`
Feladata:
- minden realm admin celu listazasa

#### `POST /api/admin/realms`
Feladata:
- uj realm letrehozasa

#### `PUT /api/admin/realms/{realmId}`
Feladata:
- meglevo realm metadata modositas

#### `GET /api/admin/gamedata/snapshot`
Feladata:
- aktualis GameData allapot lekerdezese admin celra

#### `POST /api/admin/gamedata/replace`
Feladata:
- global GameData cserelese

#### `PUT /api/admin/accounts/ban`
Feladata:
- account tiltasa vagy tiltasanak feloldasa
- ban reason frissitese
- account access cache invalidalasa

---

### 5.6 Health endpointok

#### `GET /health/live`
Feladata:
- processz szintu elerhetoseg jelzese

#### `GET /health/ready`
Feladata:
- uzemkeszseg jelzese
- adatbazis kapcsolati keszenlet ellenorzese

---

### 5.7 Command mode modulok

A projekt command mode modulokat is tartalmaz. Ezek CLI oldali uzemeltetesi vagy karbantartasi muveleteket tamogatnak.

Jelenlegi modulok:
- version
- doctor
- db
- jwt
- environment
- realms
- services
- game-data
- security

Ezek pontos CLI-szintaxisa kulon operatori dokumentacioban tarthato fenn, de logikailag a kovetkezo csoportokat fedik le:

- **version**
  - build vagy verzio informacio
- **doctor**
  - uzemi diagnosztika
- **db**
  - adatbazis allapot vagy kapcsolat ellenorzes
- **jwt**
  - JWT kulcs vagy token jellegu adminisztracios muveletek
- **environment**
  - kornyezeti allapot ellenorzes
- **realms**
  - realm adminisztracio
- **services**
  - service auth jellegu ellenorzes
- **game-data**
  - GameData adminisztracios vagy diagnosztikai muveletek
- **security**
  - biztonsagi segedmuveletek, peldaul IP ban lista kezeles

---

## 6. VPS szerveroldali beallitasok

### 6.1 Szu kseges kornyezeti elemek

A modulhoz az alabbi komponensek szuksegesek:

- Linux VPS, javasolt Ubuntu LTS
- systemd
- Nginx reverse proxy
- MySQL vagy MariaDB kompatibilis MySQL uzemmod
- Redis
- OpenSSL
- megfelelo Linux user es group az Auth futtatasahoz
- TLS certificate kezeles, javasolt Let's Encrypt

**PHP nem szukseges.**  
Ez a modul nem PHP alapu.

---

### 6.2 Javasolt csomagok telepitese Ubuntu szerveren

```bash
sudo apt update
sudo apt install -y nginx redis-server mysql-server openssl curl unzip
````

Ha kulon MySQL hostot hasznalsz, a helyi MySQL csomag nem kotelezo.

---

### 6.3 Javasolt Linux user letrehozasa

```bash
sudo useradd --system --home /opt/gnosis/authapi --shell /usr/sbin/nologin gnosisauth
sudo mkdir -p /opt/gnosis/authapi/app
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi
```

---

### 6.4 Konyvtarszerkezet

Javasolt telepitesi szerkezet:

```text
/opt/gnosis/authapi/
  app/
    GnosisAuthServer
    appsettings.json
    keys/
    logs/
    SchemaMigrations/
      realmcore/
```

---

### 6.5 JWT kulcspar letrehozasa

```bash
sudo mkdir -p /opt/gnosis/authapi/app/keys
sudo openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
sudo openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi/app/keys
sudo chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
sudo chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

### 6.6 Redis kovetelmeny

Production uzemben a Redis kotelezo, mert a nonce replay vedelem distributed modban kell hogy fusson.

Javasolt ellenorzes:

```bash
sudo systemctl enable redis-server
sudo systemctl start redis-server
redis-cli ping
```

Elvart valasz:

```text
PONG
```

---

### 6.7 Nginx konfiguracios logika

Az Auth API lokal loopback cimre fusson, a publikus kapcsolatot Nginx vegye at.

Nginx feladata:

* TLS terminacio
* host alapjan route-olas
* forwarded header-ek tovabbitasa
* opcionis rate limiting vagy IP allowlist a proxy szintjen
* admin utak tovabbi halozati korlatozasa

Peldakonfiguracio:

```nginx
server {
    listen 443 ssl http2;
    server_name auth.example.com;

    ssl_certificate /etc/letsencrypt/live/auth.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/auth.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5158;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

### 6.8 Admin utak halozati izolacioja

Az admin endpointokat ket retegben kell vedeni:

1. **Auth alkalmazas szinten**

   * HMAC admin auth
   * timestamp
   * nonce
   * body hash
   * IP vagy CIDR allowlist

2. **Halozati szinten**

   * Nginx location limit
   * firewall
   * VPN vagy belso subnet
   * cloud security group

Javaslat:

* az admin endpoint ne legyen altalanosan publikus
* csak VPN, belso subnet vagy dedikalt admin hostokrol legyen elerheto

---

### 6.9 Linux fajlrendszer jogosultsagok

Javasolt elvek:

* a futtathato binary a `gnosisauth` userhez tartozzon
* a private key csak az Auth user altal legyen olvashato
* a log konyvtar irhato legyen a service usernek
* a migration fajlok csak olvashatoak legyenek
* a publish output ne legyen altalanosan irhato

Peldak:

```bash
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi
sudo chmod 750 /opt/gnosis/authapi
sudo chmod 750 /opt/gnosis/authapi/app
sudo chmod 750 /opt/gnosis/authapi/app/logs
sudo chmod 640 /opt/gnosis/authapi/app/appsettings.json
sudo chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
sudo chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

## 7. Elso inditasi tutorial

Ez a resz az Auth API elso production-jellegu inditasanak ajanlott sorrendjet irja le.

### 7.1 Elokeszites

1. Telepitsd a szerveroldali komponenseket:

   * Nginx
   * Redis
   * MySQL
   * OpenSSL

2. Hozd letre a dedikalt Linux usert.

3. Hozd letre a telepitesi konyvtarakat.

4. Toltsd fel a publish outputot a szerverre.

5. Generald le a JWT kulcsparat.

6. Hozd letre az adatbazist es a bootstrap schema-t.

---

### 7.2 Adatbazis letrehozasa

Peldafolyamat:

```bash
mysql -u root -p
```

```sql
CREATE DATABASE gnosis_auth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gnosis_auth'@'127.0.0.1' IDENTIFIED BY 'EROS_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'127.0.0.1';
FLUSH PRIVILEGES;
```

Ezutan futtasd a bootstrap SQL-t a sajat projekted szerinti tartalommal.

---

### 7.3 Kornyezeti valtozok beallitasa

Minimalis production pelda:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export GNOSIS_AUTH__Database__ConnectionString="Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=EROS_JELSZO;SslMode=Required;"
export GNOSIS_AUTH__Jwt__PrivateKeyPemPath=/opt/gnosis/authapi/app/keys/auth_private.pem
export GNOSIS_AUTH__Jwt__PublicKeyPemPath=/opt/gnosis/authapi/app/keys/auth_public.pem
export GNOSIS_AUTH__Steam__AppId=123456
export GNOSIS_AUTH__Steam__PublisherKey=VALOS_STEAM_PUBLISHER_KEY
export GNOSIS_AUTH__ServiceAuth__Clients__0__Secret=REALMCORE_SECRET_1
export GNOSIS_AUTH__ServiceAuth__Clients__1__Secret=REALMCORE_SECRET_2
export GNOSIS_AUTH__Admin__ApiKey=EROS_ADMIN_SECRET
export GNOSIS_AUTH__Admin__AllowedIpNetworks__0=10.0.0.0/24
export GNOSIS_AUTH__NonceStore__UseDistributedCache=true
export GNOSIS_AUTH__NonceStore__RedisConnectionString=127.0.0.1:6379,abortConnect=false
```

---

### 7.4 Kezi inditas

```bash
cd /opt/gnosis/authapi/app
./GnosisAuthServer
```

Mit kell varni:

* ha a konfiguracio hibas, a szerver azonnal leall
* ha a MySQL nem erheto el, nem indul el
* ha productionben a Redis nincs rendben, nem indul el
* ha placeholder vagy ures secret maradt, nem indul el

---

### 7.5 Elso ellenorzesek

A processz inditasa utan ellenorizd:

```bash
curl -I http://127.0.0.1:5158/health/live
curl -I http://127.0.0.1:5158/health/ready
```

Ezutan ellenorizd:

* Nginx config ervenyes
* public domain TLS alatt mukodik
* admin endpoint halozatilag korlatozott
* Redis elerheto
* MySQL stabilan csatlakozik

---

### 7.6 systemd service letrehozasa

Peldafajl:

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

Aktivalas:

```bash
sudo systemctl daemon-reload
sudo systemctl enable gnosis-authapi
sudo systemctl start gnosis-authapi
sudo systemctl status gnosis-authapi
```

---

## 8. Konfiguracios modell

### 8.1 Konfiguracio forrasok

A modul ket fo konfiguracios forrast hasznal:

* `appsettings.json`
* `GNOSIS_AUTH_` prefixu environment valtozok

Productionben az alapelv:

* a fajl csak minta maradjon
* minden secret environment valtozobol jojjon

---

### 8.2 Production startup guardok

Az Auth API indulaskor ellenorzi:

* Database connection string jelenletet
* JWT key pathokat
* Redis connection stringet, ha distributed nonce store aktiv
* MySQL tenyleges elerhetoseget
* Redis tenyleges elerhetoseget, ha aktiv
* productionben a HTTPS policy aktivitasat
* productionben a distributed nonce store kotelezettseget
* mock ticket tiltast productionben
* productionben az admin allowlist jelenletet
* productionben az ures vagy placeholder secret tiltast

---

### 8.3 Mit nem az alkalmazas ellenoriz?

A kovetkezok nem startup guardok, hanem uzemeltetesi ellenorzesek:

* Nginx tenyleges route-olasa
* TLS cert ervenyessege
* firewall szabalyok
* cloud security group
* VPN vagy belso halo valos elerhetosege
* systemd environment valtozok helyessege
* CI build es teszt workflow allapota
* fajljogosultsagok helyessege
* backup rutinok tenyleges letezese

---

## 9. MySQL adatbazis struktura

### 9.1 Fo tablacsoportok

A modulhoz tartozo fo adattablak logikailag a kovetkezok:

* `accounts`
* `realms`
* `gamedata_versions`
* `gamedata_items`
* `gamedata_entities`
* `gamedata_quests`
* `gamedata_spells`
* `gamedata_auras`
* `banned_ip_addresses`

Megjegyzes:

* a pontos bootstrap SQL es EF migracios allapot a projekt adatmodelljehez kotott
* az alanti szerkezet a jelenlegi Auth modul logikai adatszerkezetet irja le

---

### 9.2 `accounts`

| Mezo           | Tipus        | Index            | Default           | FK | Magyarazat                       |
| -------------- | ------------ | ---------------- | ----------------- | -- | -------------------------------- |
| id             | INT UNSIGNED | PK               | auto increment    | -  | belso technikai azonosito        |
| steam_id       | VARCHAR(32)  | UNIQUE           | nincs             | -  | a globalis account kulcs         |
| display_name   | VARCHAR(128) | index opcionalis | NULL              | -  | utolso ismert megjelenitesi nev  |
| is_banned      | TINYINT(1)   | index            | 0                 | -  | account tiltasi allapot          |
| ban_reason     | VARCHAR(256) | -                | NULL              | -  | admin altal rogzitett tiltasi ok |
| created_at_utc | DATETIME     | index            | current timestamp | -  | letrehozas ideje                 |
| updated_at_utc | DATETIME     | index            | current timestamp | -  | utolso modositas ideje           |

**Kritikus mezok:**

* `steam_id` unique kell legyen, mert ez a jatekos globalis azonositoja
* `is_banned` indexelheto, ha admin lista vagy report kesobb raepul

---

### 9.3 `realms`

| Mezo               | Tipus        | Index  | Default           | FK | Magyarazat                          |
| ------------------ | ------------ | ------ | ----------------- | -- | ----------------------------------- |
| id                 | INT UNSIGNED | PK     | auto increment    | -  | belso technikai kulcs               |
| realm_id           | VARCHAR(64)  | UNIQUE | nincs             | -  | stabil globalis eroforras-azonosito |
| display_name       | VARCHAR(128) | index  | nincs             | -  | kliens oldali nev                   |
| region             | VARCHAR(32)  | index  | nincs             | -  | regio jeloles                       |
| public_base_url    | VARCHAR(255) | -      | NULL              | -  | publikus realm URL                  |
| is_official        | TINYINT(1)   | index  | 0                 | -  | official/community status           |
| is_enabled         | TINYINT(1)   | index  | 1                 | -  | technikai aktiv allapot             |
| is_listed          | TINYINT(1)   | index  | 1                 | -  | kliens lista lathatosag             |
| status             | VARCHAR(32)  | index  | offline           | -  | runtime allapot                     |
| current_players    | INT UNSIGNED | -      | 0                 | -  | aktualis jatekos szam               |
| max_players        | INT UNSIGNED | -      | 0                 | -  | maximum jatekos szam                |
| healthy_zone_count | INT UNSIGNED | -      | 0                 | -  | egeszseges zone processzek szama    |
| last_heartbeat_at  | DATETIME     | index  | NULL              | -  | utolso heartbeat ideje              |
| created_at_utc     | DATETIME     | index  | current timestamp | -  | letrehozas ideje                    |
| updated_at_utc     | DATETIME     | index  | current timestamp | -  | utolso modositas ideje              |

**Kritikus mezok:**

* `realm_id` unique kell legyen, mert a service ownership erre epul
* `is_official` nem szarmazhat heartbeatbol
* `status` runtime adat, nem admin metadata

---

### 9.4 `gamedata_versions`

| Mezo             | Tipus        | Index             | Default           | FK | Magyarazat                 |
| ---------------- | ------------ | ----------------- | ----------------- | -- | -------------------------- |
| id               | INT UNSIGNED | PK                | auto increment    | -  | technikai kulcs            |
| version_number   | INT UNSIGNED | UNIQUE vagy index | nincs             | -  | monoton verzioszam         |
| version_tag      | VARCHAR(64)  | index             | nincs             | -  | olvashato verzio azonosito |
| content_hash     | VARCHAR(128) | index             | nincs             | -  | tartalom hash              |
| is_active        | TINYINT(1)   | index             | 0                 | -  | aktiv snapshot jelzo       |
| notes            | VARCHAR(512) | -                 | NULL              | -  | admin megjegyzes           |
| published_at_utc | DATETIME     | index             | current timestamp | -  | publikacio ideje           |

**Kritikus mezok:**

* egyszerre csak egy aktiv verzio lehet
* `content_hash` jo cache es integritas referencia

---

### 9.5 `gamedata_items`, `gamedata_entities`, `gamedata_quests`, `gamedata_spells`, `gamedata_auras`

Ezek szerkezetileg azonos logikai mintat kovetnek.

| Mezo           | Tipus        | Index                       | Default           | FK                                                   | Magyarazat                           |
| -------------- | ------------ | --------------------------- | ----------------- | ---------------------------------------------------- | ------------------------------------ |
| id             | INT UNSIGNED | PK                          | auto increment    | -                                                    | technikai kulcs                      |
| version_number | INT UNSIGNED | composite index             | nincs             | `gamedata_versions.version_number` logikai kapcsolat | az adott GameData verziohoz tartozas |
| asset_id       | VARCHAR(100) | composite unique vagy index | nincs             | -                                                    | canonical asset azonosito            |
| class_type     | VARCHAR(100) | composite index             | nincs             | -                                                    | tipus vagy osztaly jeloles           |
| json_data      | LONGTEXT     | -                           | nincs             | -                                                    | szerializalt tartalom                |
| is_enabled     | TINYINT(1)   | index                       | 1                 | -                                                    | aktiv rekord jelzo                   |
| last_updated   | DATETIME     | index                       | current timestamp | -                                                    | utolso modositas ideje               |

**Kritikus mezok:**

* `version_number + asset_id + class_type` egyutt kritikus azonosito
* `json_data` LONGTEXT kell legyen, mert a canonical tartalom merete valtozo
* `version_number` nelkul nem lehet biztonsagos aktiv verzios olvasast csinalni

---

### 9.6 `banned_ip_addresses`

| Mezo           | Tipus        | Index | Default           | FK | Magyarazat           |
| -------------- | ------------ | ----- | ----------------- | -- | -------------------- |
| id             | INT UNSIGNED | PK    | auto increment    | -  | technikai kulcs      |
| ip_address     | VARCHAR(64)  | index | nincs             | -  | tiltott IP           |
| enabled        | TINYINT(1)   | index | 1                 | -  | aktiv tiltasi rekord |
| reason         | VARCHAR(256) | -     | NULL              | -  | tiltasi ok           |
| expires_at_utc | DATETIME     | index | NULL              | -  | lejaro tiltashoz     |
| created_at_utc | DATETIME     | index | current timestamp | -  | letrehozas ideje     |

**Kritikus mezok:**

* `ip_address` indexelese fontos request-time ellenorzeshez
* `expires_at_utc` szukseges az idozitett tiltashoz

---

### 9.7 Relaciok

A fo logikai kapcsolatok:

* **accounts**

  * jelenlegi modellben onallo tabla
  * egy jatekos = egy globalis account

* **realms**

  * onallo registry tabla
  * runtime es admin metadata ugyanitt talalkozik, de logikailag elvalasztva kezelendo

* **gamedata_versions -> gamedata_* tablák**

  * egy-a-sokhoz kapcsolat
  * egy verziohoz sok item/entity/quest/spell/aura tartozik

* **banned_ip_addresses**

  * onallo vedelmi tabla
  * nincs klasszikus FK kapcsolata az account tablaval

---

### 9.8 Miert fontos bizonyos mezok jellege?

* **UNIQUE**

  * `steam_id`, `realm_id`, bizonyos esetben `version_number`
  * duplikacio ellen ved

* **UNSIGNED**

  * ID-k, jatekosszamlalok, verzioszamok
  * negativ ertekek kizartak

* **INDEX**

  * heartbeat, auth, admin lista, readiness es report celra fontos
  * runtime mezoknel kulonosen kritikus

* **NULL kezelese**

  * ahol az adat tenylegesen opcionalis, ott `NULL`
  * ahol az adat azonositasra vagy auth logikara kell, ott ne legyen `NULL`

---

## 10. Deployment es karbantartas

### 10.1 Frissites menete adatvesztes nelkul

Az Auth API frissiteset ugy kell kezelni, hogy:

1. legyen backup az adatbazisrol
2. legyen backup a configrol es kulcsokrol
3. a publish output uj verziot kulon staging konyvtarba toltsd fel
4. ellenorizd a fajljogosultsagokat
5. ellenorizd a migration vagy bootstrap kompatibilitast
6. allitsd le a systemd service-t
7. csereld az uj binarist es szukseges fajlokat
8. inditsd ujra a service-t
9. ellenorizd a `live` es `ready` endpointokat
10. ellenorizd az admin authot es legalabb egy internal HMAC endpointot

Fontos:

* a `keys/` tartalmat ne irjad felul rutinbol
* az `appsettings.json` minta maradhat, de a valos secretek env varbol jojjenek
* GameData vagy schema valtozasnal rollback terv kotelezo

---

### 10.2 Log fajlok es hibakereses

A hibakereses tipikus helyei:

* systemd journal
* Nginx access log
* Nginx error log
* alkalmazas sajat log konyvtara, ha kulon file sink is van
* MySQL log
* Redis status es service log

Peldak:

```bash
sudo journalctl -u gnosis-authapi -n 200 --no-pager
sudo journalctl -u gnosis-authapi -f
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
sudo systemctl status redis-server
sudo systemctl status mysql
```

---

### 10.3 Tipikus hibakeresesi folyamat

#### Auth nem indul

1. `systemctl status`
2. `journalctl`
3. config es env var ellenorzes
4. MySQL kapcsolat ellenorzes
5. Redis kapcsolat ellenorzes
6. key file path ellenorzes

#### `ready` hibas

1. DB kapcsolat
2. schema / bootstrap allapot
3. user/jelszo/host elteres
4. migration inkonzisztencia

#### Internal 401/403

1. service identity helyes-e
2. secret egyezik-e
3. nonce es timestamp ervenyes-e
4. Redis nonce store mukodik-e
5. `AllowedRealmIds` megfelelo-e

#### Admin 401/403

1. admin HMAC rendben van-e
2. allowlist megfelelo-e
3. reverse proxy nem veszti-e el a kliens IP-t
4. VPN / subnet tenyleg onnan jon-e

---

### 10.4 Backup strategia

Legalabb harom reteg javasolt:

1. **MySQL backup**

   * napi dump
   * retention policy
   * kulon tarhelyre mentes

2. **Key backup**

   * private/public JWT kulcsok
   * offline vagy vedett tarolas

3. **Deploy backup**

   * elozo publish output megorzese
   * gyors rollbackhez

Peldak:

```bash
mysqldump -u root -p --databases gnosis_auth > /opt/backups/gnosis_auth_$(date +%F).sql
tar -czf /opt/backups/gnosis_auth_keys_$(date +%F).tar.gz /opt/gnosis/authapi/app/keys
```

A backupokrol javasolt:

* titkositott tarolas
* offsite masolat
* rendszeres restore teszt

---

## 11. Production launch checklist

### 11.1 Startup guard altal ellenorzott tetelek

Az alkalmazas maga ellenorzi:

* MySQL kapcsolat
* Redis kapcsolat, ha distributed nonce store aktiv
* DB connection string jelenlete
* JWT key pathok jelenlete
* production HTTPS policy
* production distributed nonce kotelezettseg
* mock ticket tiltasa
* admin allowlist jelenlete
* placeholder vagy ures secret tiltasa

---

### 11.2 Kulso checklist tetelek

Ezeket kulon neked kell ellenorizni:

* Nginx helyesen proxyz
* TLS cert ervenyes
* Auth csak loopbacken hallgat
* admin endpoint nem publikus
* firewall rendben van
* cloud security group rendben van
* systemd a helyes env varokkal fut
* CI build es teszt zold
* logolas megfelelo
* backup rutin letezik
* rollback terv dokumentalt

---

## 12. Gyors osszefoglalo

A `GnosisAuthServer` a globalis MMO backend magas bizalmi szintu Auth modulja.

Feladata:

* public login
* JWT token kiadas
* realm lista
* internal realm heartbeat
* canonical GameData
* schema delivery
* admin muveletek
* account ban/unban
* health es uzemi validacio

Production uzemben az Auth API csak akkor tekintheto megfeleloen vedettnek, ha:

* a secretek nem fajlban, hanem env varban vannak
* a Redis replay vedelem aktiv
* az admin endpoint halozatilag izolalt
* a reverse proxy megfeleloen mukodik
* a MySQL es Redis elerhetoseg stabil
* a backup es rollback folyamat dokumentalt
* a startup guardok mellett a deployment checklist is teljesul

```
```
