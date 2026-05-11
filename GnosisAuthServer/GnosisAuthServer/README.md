````md
# GnosisAuthServer Dokumentáció

## 1. A dokumentum célja

Ez a dokumentum a `GnosisAuthServer` modul részletes műszaki leírása. A modul a teljes MMO backend egyik központi, magas bizalmi szintű komponense, ezért a dokumentáció célja nem csupán a működés bemutatása, hanem a production üzemeltetéshez szükséges technikai és operációs információk egy helyen történő összefoglalása is.

A dokumentum célja:

- a modul szerepének és felelősségi körének pontos rögzítése
- a rendszerbeli adat- és forgalomutak leírása
- a szerveroldali telepítés és üzemeltetés szabályainak dokumentálása
- a fő adatbázis-struktúrák áttekintése
- a karbantartási, frissítési és első indítási folyamatok standardizálása
- a production indítás feltételeinek és ellenőrzési pontjainak rögzítése

A dokumentum célközönsége:

- backend fejlesztők
- DevOps / üzemeltetők
- technikai projektvezetők

Ez a dokumentum nem végfelhasználóknak készült.

---

## 2. Modul áttekintése

### 2.1 Mi a modul célja

A `GnosisAuthServer` a globális backend egyik központi belépési és vezérlési rétege. Feladata nem a tényleges játéklogika futtatása, hanem a teljes rendszer számára közös, globális szolgáltatások biztosítása.

### 2.2 A modul felelősségi köre

A modul fő felelősségi körei:

1. **Játékos-hitelesítés**
   - Steam-alapú beléptetés
   - fiók létrehozása vagy betöltése
   - JWT access token kiadása

2. **Realm lista kiszolgálása**
   - az elérhető játékszerverek listájának visszaadása a kliensnek
   - a realm metadata központi tárolása

3. **Belső szolgáltatások hitelesítése**
   - RealmCore szolgáltatások HMAC alapú hitelesítése
   - heartbeat fogadása
   - belső API-hívások ellenőrzése

4. **Globális GameData kezelése**
   - központi tárgy-, küldetés-, varázslat- és egyéb játékadatok tárolása
   - GameData verzióinformációk és snapshotok kiszolgálása

5. **Schema delivery**
   - adatbázis-migrációs manifest és migration tartalmak kiszolgálása a Realm komponensek számára

6. **Adminisztráció**
   - realm-ek létrehozása és módosítása
   - GameData frissítése
   - account tiltás és tiltás feloldása

7. **Üzemeltetési ellenőrzések**
   - health endpointok
   - induláskori validációk
   - konfigurációs fail-fast ellenőrzések

### 2.3 Mi NEM a modul felelőssége

A `GnosisAuthServer` nem végzi az alábbi feladatokat:

- játéklogika futtatása
- mozgás, harcrendszer, inventory runtime szintű kezelése
- zónák vagy instance-ok futtatása
- node orchestration
- kliensoldali állapotkezelés
- realm-specifikus játékmentések közvetlen kezelése

Ezeket más komponensek végzik, például:

- `GnosisRealmCore`
- Node Agent
- Zone / Game Server komponensek
- kliensoldali rendszerek

### 2.4 Kapcsolódó komponensek

A modul közvetlenül vagy logikailag az alábbi komponensekkel áll kapcsolatban:

- **Kliens**
- **GnosisRealmCore**
- **Admin eszközök / belső operátori kliensek**
- **MySQL**
- **Redis**
- **Nginx reverse proxy**
- **systemd szolgáltatáskezelés**

---

## 3. Architektúra áttekintés

### 3.1 Fő adatáramlások

#### Kliens -> Auth API

A kliens a belépési folyamat során Steam hitelesítési adatokat küld az Auth API felé. Az Auth API ezt ellenőrzi, majd visszaad egy JWT access tokent, amely a további kliensoldali kommunikáció alapja.

A kliens ezen felül realm listát is kérhet, valamint lekérheti a saját account adatait.

#### RealmCore -> Auth API

A RealmCore belső, HMAC-hitelesített végpontokon keresztül kommunikál az Auth API-val.

Tipikus műveletek:

- heartbeat küldés
- GameData verzió lekérdezése
- teljes GameData snapshot lekérdezése
- schema manifest lekérdezése
- migration tartalom lekérdezése

#### Admin -> Auth API

Az admin réteg kizárólag védett, belső célú végpontokon keresztül érheti el az Auth API-t. Ezek a hívások magasabb bizalmi szintet képviselnek, és külön admin hitelesítést, valamint IP vagy hálózati allowlistet igényelnek.

Tipikus műveletek:

- realm létrehozása
- realm módosítása
- GameData csere
- account tiltás és tiltás feloldása

---

### 3.2 Trust boundary-k

A modul négy fő bizalmi réteggel dolgozik:

1. **Publikus kliensforgalom**
   - legalacsonyabb bizalmi szint
   - internet felől érkező kérés
   - hostile client modell

2. **Bearer-tokenes kliensforgalom**
   - JWT alapú hitelesítés
   - account access ellenőrzés

3. **Belső service-forgalom**
   - HMAC alapú hitelesítés
   - nonce replay-védelem
   - ownership alapú engedélyezés

4. **Admin forgalom**
   - admin HMAC hitelesítés
   - IP vagy CIDR allowlist
   - szigorúan korlátozott hálózati elérés

---

### 3.3 Tipikus request flow-k

#### Steam login flow

1. A kliens elküldi a Steam ticketet az Auth API felé.
2. Az Auth API ellenőrzi a ticketet.
3. Az Auth API megkeresi vagy létrehozza az accountot.
4. Az Auth API JWT access tokent ad vissza.
5. A kliens a tokennel éri el a védett végpontokat.

#### Realm heartbeat flow

1. A RealmCore HMAC-hitelesített kérést küld.
2. Az Auth API ellenőrzi a service azonosítót, timestampet, nonce-ot, body hash-t és aláírást.
3. Az Auth API ellenőrzi, hogy az adott service kezelheti-e az adott realm-et.
4. Az Auth API frissíti a runtime állapotot.

#### Admin ban/unban flow

1. Az admin kliens admin végpontra küld védett kérést.
2. Az Auth API ellenőrzi az admin hitelesítést és az IP / hálózati allowlistet.
3. Az account tiltása vagy tiltásának feloldása megtörténik.
4. Az account access cache invalidálódik.

---

## 4. Hálózati rétegek

### 4.1 Portok

Javasolt topológia:

- **Belső alkalmazásport:** `127.0.0.1:5158`
- **Publikus HTTPS port:** `443`
- **Opcionális HTTP port:** `80`

Az alkalmazás közvetlenül ne legyen kitéve publikus internetes forgalomnak a belső Kestrel porton.

### 4.2 Protokollok

- kliensoldali kommunikáció: HTTPS
- belső szolgáltatáskommunikáció: HTTPS reverse proxy mögött vagy belső hálózaton keresztül
- adatbázis kapcsolat: MySQL
- cache / nonce store: Redis

### 4.3 HTTPS / TLS követelmények

Production környezetben a modul csak HTTPS mögött tekinthető megfelelően üzemeltethetőnek.

Követelmények:

- a publikus forgalmat Nginx vagy más reverse proxy fogadja
- a TLS termináció a reverse proxy rétegben történik
- az Auth API oldalon a HTTPS használata kötelező
- a forwarded header-ek csak megbízható proxyktól származhatnak

### 4.4 Reverse proxy szerepe

Az Nginx feladata:

- TLS termináció
- a publikus forgalom továbbítása a belső 127.0.0.1:5158 címre
- host és forwarded header-ek átadása
- opcionálisan plusz hálózati vagy útvonal-alapú szűrés
- admin plane további szűrése

### 4.5 Belső bind címek

Az alkalmazás javasolt bind címe:

- `127.0.0.1:5158`

Ez biztosítja, hogy az alkalmazás ne legyen közvetlenül elérhető az internet felől a reverse proxy megkerülésével.

### 4.6 Publikus elérési út

A külvilág felől az Auth API kizárólag a reverse proxy által publikált domainen keresztül legyen elérhető, például:

- `https://auth.sajatdomain.hu`

### 4.7 Forwarded header trust modell

A rendszer csak a konfigurált proxyktól vagy hálózatoktól fogadhat el forwarded header-eket. A hibás proxy trust modell könnyen admin vagy IP-szintű védelmek megkerüléséhez vezethet.

### 4.8 Admin plane hálózati izoláció

Az admin végpontokhoz az alábbi védelmek szükségesek:

- admin HMAC hitelesítés
- IP vagy CIDR allowlist
- reverse proxy oldali további korlátozás
- javasolt VPN vagy belső hálózat használata

---

## 5. Endpoint / parancs katalógus

## 5.1 Publikus endpointok

### `POST /api/auth/steam`
- **HTTP metódus:** POST
- **Cél:** Steam login folyamat végrehajtása
- **Ki használja:** kliens
- **Olvas / ír:** account adatokat olvas vagy létrehoz
- **Trust szint:** publikus
- **Mellékhatás:** account létrejöhet vagy frissülhet; JWT token kiadás történik

### `GET /api/auth/me`
- **HTTP metódus:** GET
- **Cél:** a bejelentkezett account adatainak lekérdezése
- **Ki használja:** kliens
- **Olvas / ír:** account adatot olvas
- **Trust szint:** bearer-tokenes kliensforgalom
- **Mellékhatás:** nincs

### `GET /api/auth/servers`
- **HTTP metódus:** GET
- **Cél:** az elérhető realm lista lekérdezése
- **Ki használja:** kliens
- **Olvas / ír:** realm adatokat olvas
- **Trust szint:** bearer-tokenes vagy publikus olvasási szint, implementációtól függően
- **Mellékhatás:** nincs
- **Alias:** `GET /api/realms`

---

## 5.2 Belső endpointok

### `POST /api/internal/realms/heartbeat`
- **HTTP metódus:** POST
- **Cél:** realm runtime állapot frissítése
- **Ki használja:** RealmCore
- **Olvas / ír:** realm runtime mezőket ír
- **Trust szint:** belső, HMAC-hitelesített
- **Mellékhatás:** heartbeat timestamp, státusz és játékosszám frissül

### `GET /api/internal/gamedata/version`
- **HTTP metódus:** GET
- **Cél:** aktuális GameData verzió lekérdezése
- **Ki használja:** RealmCore
- **Olvas / ír:** GameData verzióadatokat olvas
- **Trust szint:** belső, HMAC-hitelesített
- **Mellékhatás:** nincs
- **Alias:** `GET /api/gamedata/version`

### `GET /api/internal/gamedata/snapshot`
- **HTTP metódus:** GET
- **Cél:** teljes GameData snapshot lekérdezése
- **Ki használja:** RealmCore
- **Olvas / ír:** GameData rekordokat olvas
- **Trust szint:** belső, HMAC-hitelesített
- **Mellékhatás:** nincs
- **Alias:** `GET /api/gamedata/snapshot`

### `GET /api/internal/schema/manifest`
- **HTTP metódus:** GET
- **Cél:** elérhető migrációk listájának lekérdezése
- **Ki használja:** RealmCore
- **Olvas / ír:** schema metadata-t olvas
- **Trust szint:** belső, HMAC-hitelesített
- **Mellékhatás:** nincs

### `GET /api/internal/schema/migrations/{migrationId}`
- **HTTP metódus:** GET
- **Cél:** konkrét migration tartalom lekérdezése
- **Ki használja:** RealmCore
- **Olvas / ír:** migration tartalmat olvas
- **Trust szint:** belső, HMAC-hitelesített
- **Mellékhatás:** nincs

---

## 5.3 Admin endpointok

### `GET /api/admin/realms`
- **HTTP metódus:** GET
- **Cél:** összes realm admin célú listázása
- **Ki használja:** admin eszköz
- **Olvas / ír:** realm adatokat olvas
- **Trust szint:** admin
- **Mellékhatás:** nincs

### `POST /api/admin/realms`
- **HTTP metódus:** POST
- **Cél:** új realm létrehozása
- **Ki használja:** admin eszköz
- **Olvas / ír:** realm adatot ír
- **Trust szint:** admin
- **Mellékhatás:** új realm rekord jön létre

### `PUT /api/admin/realms/{realmId}`
- **HTTP metódus:** PUT
- **Cél:** realm metadata módosítása
- **Ki használja:** admin eszköz
- **Olvas / ír:** realm adatot ír
- **Trust szint:** admin
- **Mellékhatás:** metadata frissül

### `GET /api/admin/gamedata/snapshot`
- **HTTP metódus:** GET
- **Cél:** aktuális GameData állapot admin lekérdezése
- **Ki használja:** admin eszköz
- **Olvas / ír:** GameData rekordokat olvas
- **Trust szint:** admin
- **Mellékhatás:** nincs

### `POST /api/admin/gamedata/replace`
- **HTTP metódus:** POST
- **Cél:** teljes GameData csere
- **Ki használja:** admin eszköz, például Unity Editor integráció
- **Olvas / ír:** GameData rekordokat ír
- **Trust szint:** admin
- **Mellékhatás:** GameData verzió és tartalom változik

### `PUT /api/admin/accounts/ban`
- **HTTP metódus:** PUT
- **Cél:** account tiltása vagy tiltás feloldása
- **Ki használja:** admin eszköz
- **Olvas / ír:** account rekordot ír
- **Trust szint:** admin
- **Mellékhatás:** tiltási állapot változik; cache invalidálás történik

---

## 5.4 Health endpointok

### `GET /health/live`
- **HTTP metódus:** GET
- **Cél:** folyamat szintű liveness
- **Ki használja:** monitoring, reverse proxy, operátor
- **Olvas / ír:** nincs üzleti adat
- **Trust szint:** általában publikus vagy belső monitoring
- **Mellékhatás:** nincs

### `GET /health/ready`
- **HTTP metódus:** GET
- **Cél:** readiness ellenőrzés
- **Ki használja:** monitoring, operátor
- **Olvas / ír:** nincs üzleti adat
- **Trust szint:** belső monitoring javasolt
- **Mellékhatás:** nincs

---

## 6. Command mode / operátori parancsok

A projekt command mode modulokat is tartalmaz.

Jelenleg ismert modulok:

- `version`
- `doctor`
- `db`
- `jwt`
- `environment`
- `realms`
- `services`
- `game-data`
- `security`

Rövid céljuk:

- **version**
  - verzió- vagy buildinformációk lekérdezése

- **doctor**
  - diagnosztikai ellenőrzések

- **db**
  - adatbázis kapcsolódó ellenőrzések

- **jwt**
  - JWT és kulcskezeléshez kapcsolódó operátori funkciók

- **environment**
  - környezeti állapot ellenőrzése

- **realms**
  - realm adminisztrációs műveletek

- **services**
  - service hitelesítési vagy service-konfigurációs ellenőrzések

- **game-data**
  - GameData adminisztráció és diagnosztika

- **security**
  - biztonsági jellegű operátori funkciók, például IP tiltási lista

A pontos CLI szintaxis ebből a dokumentumból nem minden esetben ellenőrizhető a kapott anyag alapján. Ha szükséges, külön operátori kézikönyv javasolt.

---

## 7. VPS szerveroldali beállítások

### 7.1 Szükséges környezeti elemek

A modulhoz az alábbi komponensek szükségesek:

- Ubuntu LTS vagy más stabil Linux disztribúció
- systemd
- Nginx
- MySQL
- Redis
- OpenSSL
- curl
- unzip

PHP nem szükséges.

### 7.2 Szükséges szolgáltatások

A production működéshez legalább az alábbi szolgáltatások szükségesek:

- `nginx`
- `mysql`
- `redis-server`
- `gnosis-authapi` systemd service

### 7.3 Linux user és mappastruktúra

Javasolt dedikált rendszerfelhasználó:

- `gnosisauth`

Javasolt mappastruktúra:

```text
/opt/gnosis/authapi/
  .env
  app/
    GnosisAuthServer
    appsettings.json
    keys/
      auth_private.pem
      auth_public.pem
    logs/
    SchemaMigrations/
      realmcore/
````

### 7.4 Nginx szerepe

Az Nginx biztosítja:

* a TLS terminációt
* a publikus HTTPS belépési pontot
* a belső Kestrel port védelmét
* a forwarded header-ek továbbítását
* az opcionális admin útvonal-korlátozást

### 7.5 Redis szükségessége

A Redis production környezetben kötelező, mert:

* a nonce alapú replay-védelem több példányos környezetben csak shared store-ral működik helyesen
* az admin és service auth replay-védelme distributed store-t igényel

### 7.6 MySQL szükségessége

A MySQL tárolja:

* account adatokat
* realm metadata-t és runtime állapotot
* GameData verziókat és snapshot rekordokat
* IP tiltási listákat
* egyéb perzisztens globális state-et

### 7.7 systemd használata

A szolgáltatás systemd alapon fusson, mert ez biztosítja:

* automatikus indulást
* újraindítást hiba esetén
* naplózást
* egységes operátori kezelést

### 7.8 SSL / TLS tanúsítványok logikája

A publikus domainhez tartozó TLS tanúsítvány a reverse proxy rétegben szükséges.

Javasolt hely:

* `/etc/letsencrypt/live/<domain>/fullchain.pem`
* `/etc/letsencrypt/live/<domain>/privkey.pem`

### 7.9 Fájljogosultságok

Kulcsjogosultsági elvek:

* a private key csak a szolgáltatás felhasználója által legyen olvasható
* az alkalmazás binárisai ne legyenek publikus írásra nyitva
* a `.env` fájl ne legyen széles körben olvasható
* a kulcsok, konfigurációk és logok külön kezelendők

---

### 7.10 Telepítési parancsok

#### Alap csomagok telepítése

```bash
sudo apt update
sudo apt install -y nginx redis-server mysql-server openssl curl unzip
```

#### Dedikált user és mappák létrehozása

```bash
sudo useradd --system --home /opt/gnosis/authapi --shell /usr/sbin/nologin gnosisauth
sudo mkdir -p /opt/gnosis/authapi/app
sudo mkdir -p /opt/gnosis/authapi/app/keys
sudo mkdir -p /opt/gnosis/authapi/app/logs
sudo mkdir -p /opt/gnosis/authapi/app/SchemaMigrations/realmcore
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi
```

#### JWT kulcsok létrehozása

```bash
sudo openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
sudo openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi/app/keys
sudo chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
sudo chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

## 8. Konfigurációs modell

### 8.1 Konfigurációs források

A modul két fő konfigurációs forrást használ:

* `appsettings.json`
* környezeti változók `GNOSIS_AUTH_` prefixszel

### 8.2 Mi menjen appsettings-be

Az `appsettings.json` mintakonfig céljára alkalmas az alábbi típusú értékekhez:

* URL bind cím
* általános timeoutok
* logikai defaultok
* dokumentációs jellegű minták
* nem érzékeny kapcsolási paraméterek

### 8.3 Mi menjen environment variable-be

Az alábbi értékek environment variable-be valók:

* adatbázis connection string
* admin API kulcs
* service HMAC secret-ek
* Steam publisher key
* Redis connection string
* production környezetre jellemző titkos vagy érzékeny konfigurációk

### 8.4 Mely értékek tekinthetők secretnek

Secretnek tekintendő:

* `Database:ConnectionString`
* `Admin:ApiKey`
* `ServiceAuth:Clients[*]:Secret`
* `Steam:PublisherKey`
* `NonceStore:RedisConnectionString`
* bármely privát kulcs elérési útja és tartalma operatív szempontból érzékeny

### 8.5 Mely értékek maradhatnak mintakonfigban

Mintakonfigban maradhat:

* nem éles URL
* alapértelmezett timeout
* mintaként üres string
* nem érzékeny metadata
* development célú semleges érték

### 8.6 Development és production konfiguráció elválasztása

Ajánlott modell:

* a repo-ban egy minta `appsettings.json` maradjon
* production környezetben a kritikus értékek environment variable-ből jöjjenek
* a deployment réteg biztosítsa a valós értékeket

### 8.7 Hibás konfigurációk, amelyek blokkolják az indulást

Productionben az indulást blokkolnia kell például az alábbiaknak:

* üres vagy placeholder connection string
* üres admin secret
* üres service secret
* nem elérhető MySQL
* Redis használat mellett nem elérhető Redis
* HTTPS policy hiánya productionben
* distributed nonce store hiánya productionben
* üres admin allowlist productionben

---

## 9. Induláskori validációk

### 9.1 Mit ellenőriz maga az alkalmazás induláskor

Az alkalmazás induláskor ellenőrzi vagy ellenőrizheti:

* az adatbázis kapcsolat jelenlétét és elérhetőségét
* a JWT kulcsok elérési útját
* a Redis kapcsolatot, ha distributed nonce store aktív
* a production startup guard szabályokat
* a kötelező modulregisztrációkat

### 9.2 Mit NEM az alkalmazás ellenőriz

Az alábbiak deploy vagy üzemeltetési checklist kérdések:

* az Nginx helyes route-olása
* a TLS tanúsítvány érvényessége
* a firewall szabályok helyessége
* a systemd helyes environment file használata
* a domain DNS helyessége
* a publikus internet felől tényleges kitettség
* a backup rutin tényleges működése

### 9.3 Mely hibák esetén kell fail-fast módon megállnia

Az alkalmazásnak nem szabad elindulnia például az alábbi esetekben:

* nincs adatbázis kapcsolat
* nincs JWT kulcs
* productionben nincs HTTPS megkövetelve
* productionben nincs Redis alapú nonce store
* placeholder secret maradt éles konfigurációban
* admin allowlist hiányzik productionben

---

## 10. MySQL adatbázis struktúra

Megjegyzés: a pontos séma a kód, a bootstrap SQL és az esetleges migrációk alapján tekinthető véglegesnek. Az alábbi leírás a fő, logikai adattárolási modellt rögzíti.

### 10.1 Fő táblák

* `accounts`
* `realms`
* `gamedata_versions`
* `gamedata_items`
* `gamedata_entities`
* `gamedata_quests`
* `gamedata_spells`
* `gamedata_auras`
* `banned_ip_addresses`

---

### 10.2 `accounts`

| Mezőnév          | Adattípus    | Index      | Alapértelmezett   | Foreign Key | Magyarázat                      |
| ---------------- | ------------ | ---------- | ----------------- | ----------- | ------------------------------- |
| `id`             | INT UNSIGNED | PK         | AUTO_INCREMENT    | -           | belső technikai azonosító       |
| `steam_id`       | VARCHAR(32)  | UNIQUE     | nincs             | -           | globalis account azonosító      |
| `display_name`   | VARCHAR(128) | opcionális | NULL              | -           | utolsó ismert megjelenített név |
| `is_banned`      | TINYINT(1)   | INDEX      | 0                 | -           | tiltási állapot                 |
| `ban_reason`     | VARCHAR(256) | -          | NULL              | -           | tiltás oka                      |
| `created_at_utc` | DATETIME     | INDEX      | CURRENT_TIMESTAMP | -           | létrehozási idő                 |
| `updated_at_utc` | DATETIME     | INDEX      | CURRENT_TIMESTAMP | -           | utolsó módosítás ideje          |

**Kritikus mezők:**

* `steam_id` egyedi kell legyen
* `id` unsigned, mert negatív értéknek nincs értelme
* `is_banned` indexelhető operátori lekérdezésekhez
* `created_at_utc` és `updated_at_utc` audit célra szükséges

---

### 10.3 `realms`

| Mezőnév             | Adattípus    | Index  | Alapértelmezett | Foreign Key | Magyarázat                   |
| ------------------- | ------------ | ------ | --------------- | ----------- | ---------------------------- |
| `id`                | INT UNSIGNED | PK     | AUTO_INCREMENT  | -           | belső technikai azonosító    |
| `realm_id`          | VARCHAR(64)  | UNIQUE | nincs           | -           | stabil realm azonosító       |
| `display_name`      | VARCHAR(128) | INDEX  | nincs           | -           | kliens által látott név      |
| `public_base_url`   | VARCHAR(255) | -      | NULL            | -           | publikus cím                 |
| `is_official`       | TINYINT(1)   | INDEX  | 0               | -           | official / community jelölés |
| `status`            | VARCHAR(32)  | INDEX  | `offline`       | -           | runtime státusz              |
| `current_players`   | INT UNSIGNED | -      | 0               | -           | aktuális játékosszám         |
| `last_heartbeat_at` | DATETIME     | INDEX  | NULL            | -           | utolsó heartbeat ideje       |

**Kritikus mezők:**

* `realm_id` egyedi kell legyen
* `is_official` admin által kezelt mező
* `status` runtime állapot
* `last_heartbeat_at` liveness jellegű mező

---

### 10.4 `gamedata_versions`

| Mezőnév            | Adattípus    | Index             | Alapértelmezett   | Foreign Key | Magyarázat                   |
| ------------------ | ------------ | ----------------- | ----------------- | ----------- | ---------------------------- |
| `id`               | INT UNSIGNED | PK                | AUTO_INCREMENT    | -           | technikai kulcs              |
| `version_number`   | INT UNSIGNED | UNIQUE vagy INDEX | nincs             | -           | monoton verziószám           |
| `version_tag`      | VARCHAR(64)  | INDEX             | nincs             | -           | ember által olvasható verzió |
| `content_hash`     | VARCHAR(128) | INDEX             | nincs             | -           | tartalom hash                |
| `is_active`        | TINYINT(1)   | INDEX             | 0                 | -           | aktív snapshot jelző         |
| `notes`            | VARCHAR(512) | -                 | NULL              | -           | admin megjegyzés             |
| `published_at_utc` | DATETIME     | INDEX             | CURRENT_TIMESTAMP | -           | publikálási idő              |

**Kritikus mezők:**

* egyszerre csak egy aktív verzió lehet
* `version_number` verziókezeléshez szükséges
* `content_hash` integritási és cache célokra hasznos

---

### 10.5 `gamedata_items`

| Mezőnév          | Adattípus    | Index | Alapértelmezett | Foreign Key                        | Magyarázat                            |
| ---------------- | ------------ | ----- | --------------- | ---------------------------------- | ------------------------------------- |
| `id`             | INT UNSIGNED | PK    | AUTO_INCREMENT  | -                                  | technikai kulcs                       |
| `version_number` | INT UNSIGNED | INDEX | nincs           | `gamedata_versions.version_number` | verzióhoz tartozás                    |
| `asset_id`       | VARCHAR(100) | INDEX | nincs           | -                                  | asset azonosító                       |
| `json_data`      | LONGTEXT     | -     | nincs           | -                                  | a rekord teljes szerializált tartalma |

Ugyanez a logikai minta alkalmazható az alábbi táblákra is:

* `gamedata_entities`
* `gamedata_quests`
* `gamedata_spells`
* `gamedata_auras`

**Kritikus mezők:**

* a `version_number` nélkül nem lehet korrekt aktív verzióra szűrni
* a `json_data` tárolja a teljes kanonikus tartalmat
* az `asset_id` logikai azonosító

---

### 10.6 `banned_ip_addresses`

| Mezőnév          | Adattípus    | Index | Alapértelmezett   | Foreign Key | Magyarázat           |
| ---------------- | ------------ | ----- | ----------------- | ----------- | -------------------- |
| `id`             | INT UNSIGNED | PK    | AUTO_INCREMENT    | -           | technikai kulcs      |
| `ip_address`     | VARCHAR(64)  | INDEX | nincs             | -           | tiltott IP vagy cím  |
| `enabled`        | TINYINT(1)   | INDEX | 1                 | -           | aktív tiltási rekord |
| `reason`         | VARCHAR(256) | -     | NULL              | -           | tiltás oka           |
| `expires_at_utc` | DATETIME     | INDEX | NULL              | -           | lejárati idő         |
| `created_at_utc` | DATETIME     | INDEX | CURRENT_TIMESTAMP | -           | létrehozási idő      |

---

### 10.7 Relációk

* egy `gamedata_versions` rekordhoz sok `gamedata_items` és más GameData rekord tartozhat
* az `accounts` tábla jelenlegi logikai modellben önálló
* a `realms` tábla önálló registry és runtime állapot tábla
* a `banned_ip_addresses` tábla védelmi célú önálló tábla

### 10.8 Kritikus mezők magyarázata

* **UNIQUE**

  * duplikációt akadályoz meg
  * kritikus azonosítókhoz szükséges

* **UNSIGNED**

  * negatív technikai azonosítók és számlálók kizárása

* **NULLABLE**

  * csak ott engedhető meg, ahol az adat valóban opcionális

* **VERSIONING**

  * GameData-nál szükséges a biztonságos verziókezeléshez

* **AUDIT mezők**

  * `created_at_utc`, `updated_at_utc`, `published_at_utc`
  * hibakereséshez és operációs visszakövethetőséghez fontosak

---

## 11. Első indítási tutorial

Ez a szekció egy teljesen új, üres Ubuntu VPS-ből indul ki.

### 11.1 Telepítési sorrend

1. alaprendszer frissítése
2. szükséges csomagok telepítése
3. dedikált Linux user létrehozása
4. mappastruktúra létrehozása
5. JWT kulcsok generálása
6. MySQL adatbázis és user létrehozása
7. környezeti változók definiálása
8. systemd service létrehozása
9. reverse proxy konfiguráció
10. alkalmazás indítása
11. első health ellenőrzések

### 11.2 Adatbázis létrehozása

```bash
sudo mysql -u root
```

```sql
CREATE DATABASE gnosis_auth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gnosis_auth'@'127.0.0.1' IDENTIFIED BY 'ERŐS_JELSZÓ';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'127.0.0.1';
FLUSH PRIVILEGES;
EXIT;
```

### 11.3 Environment változók beállítása

Például egy `.env` fájlban:

```ini
ASPNETCORE_ENVIRONMENT=Production
GNOSIS_AUTH__Database__ConnectionString=Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=ERŐS_JELSZÓ;SslMode=Required;
GNOSIS_AUTH__Jwt__PrivateKeyPemPath=/opt/gnosis/authapi/app/keys/auth_private.pem
GNOSIS_AUTH__Jwt__PublicKeyPemPath=/opt/gnosis/authapi/app/keys/auth_public.pem
GNOSIS_AUTH__Steam__AppId=123456
GNOSIS_AUTH__Steam__PublisherKey=VALÓS_STEAM_PUBLISHER_KEY
GNOSIS_AUTH__ServiceAuth__Clients__0__Secret=VALÓS_SERVICE_SECRET_1
GNOSIS_AUTH__ServiceAuth__Clients__1__Secret=VALÓS_SERVICE_SECRET_2
GNOSIS_AUTH__Admin__ApiKey=VALÓS_ADMIN_SECRET
GNOSIS_AUTH__Admin__AllowedIpNetworks__0=10.0.0.0/24
GNOSIS_AUTH__NonceStore__UseDistributedCache=true
GNOSIS_AUTH__NonceStore__RedisConnectionString=127.0.0.1:6379,abortConnect=false
```

### 11.4 Fontos megjegyzés az `.env` használathoz

Az alkalmazás közvetlenül nem feltétlenül `.env` fájlt olvas, hanem rendszerkörnyezeti változókat. A `.env` fájl jellemzően a systemd `EnvironmentFile` beállításán keresztül kerül betöltésre. Ezért a `.env` használata akkor helyes, ha a service konfiguráció ezt explicit módon beolvassa.

### 11.5 Service létrehozása

```bash
sudo nano /etc/systemd/system/gnosis-authapi.service
```

Példatartalom:

```ini
[Unit]
Description=Gnosis Auth API Service
After=network.target mysql.service redis-server.service

[Service]
WorkingDirectory=/opt/gnosis/authapi/app
ExecStart=/opt/gnosis/authapi/app/GnosisAuthServer
User=gnosisauth
Group=gnosisauth
EnvironmentFile=/opt/gnosis/authapi/.env
Restart=always
RestartSec=5
SyslogIdentifier=gnosis-authapi

[Install]
WantedBy=multi-user.target
```

### 11.6 Reverse proxy beállítása

```bash
sudo nano /etc/nginx/sites-available/default
```

Példakonfiguráció:

```nginx
server {
    listen 80;
    server_name auth.pelda.hu;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name auth.pelda.hu;

    ssl_certificate /etc/letsencrypt/live/auth.pelda.hu/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/auth.pelda.hu/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5158;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### 11.7 Első indítás

```bash
sudo systemctl daemon-reload
sudo systemctl enable gnosis-authapi
sudo systemctl start gnosis-authapi
sudo systemctl status gnosis-authapi
```

### 11.8 Első ellenőrzések

```bash
curl -I http://127.0.0.1:5158/health/live
curl -I http://127.0.0.1:5158/health/ready
sudo nginx -t
sudo systemctl status nginx
sudo systemctl status redis-server
sudo systemctl status mysql
```

### 11.9 Mit kell látni sikeres induláskor

* a systemd service `active (running)` állapotban van
* az Nginx config érvényes
* a MySQL és Redis szolgáltatás fut
* a `health/live` elérhető
* a `health/ready` sikeresen válaszol
* az alkalmazás nem áll le konfigurációs hibával

---

## 12. Deployment és karbantartás

### 12.1 Frissítés menete adatvesztés nélkül

Javasolt folyamat:

1. az új build elkészítése
2. adatbázis backup
3. konfiguráció és kulcs backup
4. szolgáltatás leállítása
5. új build feltöltése staging vagy célmappába
6. fájljogosultságok ellenőrzése
7. service újraindítása
8. health check ellenőrzés
9. admin és belső végpontok smoke tesztelése

### 12.2 Staging / csere / újraindítás logika

Ajánlott frissítési modell:

* új artifact feltöltése külön staging könyvtárba
* ellenőrzés
* szolgáltatás leállítása
* célmappa cseréje vagy szinkronizálása
* szolgáltatás újraindítása

A vakon végrehajtott tömeges törlés kockázatos. A frissítési folyamatot célszerű staging alapú cserével vagy kontrollált deploy skripttel végezni.

### 12.3 Rollback szempontok

Rollback esetén szükséges:

* előző build megőrzése
* adatbázis backup
* kulcsok és konfiguráció változatlan megőrzése
* migration kompatibilitás ellenőrzése

### 12.4 Kulcsok és secretek kezelése frissítéskor

Frissítéskor:

* a `keys/` tartalmát nem szabad felülírni
* a `.env` vagy environment file tartalmát nem szabad véletlenül lecserélni
* a production secret-eket ne a build artifact tartalmazza

### 12.5 Konfigurációs drift veszélyek

Tipikus drift kockázatok:

* más `.env` tartalom a szerveren, mint amit az operátor feltételez
* más Nginx config, mint amit a projekt dokumentál
* helytelen proxy trust beállítások
* environment variable hiány systemd alatt

### 12.6 Downtime minimalizálási javaslatok

* staging alapú deploy
* előzetes config-ellenőrzés
* health check utáni forgalomengedés
* backup és rollback terv előkészítése

---

## 13. Logging és hibakeresés

### 13.1 Logforrások

A hibakeresés tipikus helyei:

* systemd journal
* Nginx access log
* Nginx error log
* MySQL log
* Redis service állapot
* alkalmazás-specifikus log kimenet, ha külön logfájl használatban van

### 13.2 Javasolt ellenőrző parancsok

```bash
sudo journalctl -u gnosis-authapi -f
sudo journalctl -u gnosis-authapi -n 200 --no-pager
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
sudo systemctl status nginx
sudo systemctl status mysql
sudo systemctl status redis-server
```

### 13.3 Tipikus hibák, tünetek, okok

#### Tünet: az alkalmazás nem indul

Valószínű okok:

* hibás connection string
* hiányzó JWT kulcs
* hibás environment file
* Redis nem érhető el production módban
* placeholder secret maradt konfigurációban

#### Tünet: 502 Bad Gateway

Valószínű okok:

* Nginx fut, de az alkalmazás nem
* az alkalmazás összeomlott indulás után
* rossz proxy_pass cím

#### Tünet: 401 a belső végpontokon

Valószínű okok:

* hibás HMAC secret
* rossz nonce vagy timestamp
* hibás body hash
* service identity eltérés

#### Tünet: 403 admin végponton

Valószínű okok:

* admin IP nincs allowlistben
* helytelen admin signature
* reverse proxy nem a valós kliens IP-t adja át

#### Tünet: `health/ready` hibás

Valószínű okok:

* adatbázis kapcsolat hiba
* bootstrap vagy séma hiány
* migrációs inkonzisztencia

---

## 14. Biztonsági mentés

### 14.1 Mit kell menteni

* MySQL adatbázis
* JWT kulcsok
* environment file vagy más secret forrás
* deploy artifact vagy az előző stabil build
* Nginx konfiguráció
* systemd service definíció

### 14.2 Milyen gyakran

Minimum javaslat:

* adatbázis: napi
* kulcsok: változáskor és rendszeresen ellenőrzött offline mentés
* konfiguráció: minden változtatás után
* artifact: minden release előtt és után

### 14.3 Adatbázis backup

```bash
mysqldump -u root -p gnosis_auth > /opt/gnosis/authapi/gnosis_auth_backup.sql
```

### 14.4 Kulcs backup

```bash
tar -czf /opt/gnosis/authapi/keys_backup.tar.gz /opt/gnosis/authapi/app/keys
```

### 14.5 Visszaállítási javaslat

A backup csak akkor tekinthető valós védelemnek, ha restore teszt is történik. Javasolt rendszeresen ellenőrizni, hogy:

* az SQL dump visszaállítható
* a kulcsfájlok használhatók
* a szolgáltatás újraindítható backupból

---

## 15. Production launch checklist

Indulás előtt az alábbiakat kötelező ellenőrizni.

### 15.1 Security feltételek

* minden secret environment variable-ből jön
* nincs placeholder érték productionben
* HTTPS aktív
* admin allowlist be van állítva
* admin plane nem nyitott publikus internet felé
* Redis distributed nonce store aktív
* JWT kulcsok léteznek és megfelelő jogosultságokkal rendelkeznek

### 15.2 Infra feltételek

* Nginx fut és helyesen proxyz
* MySQL fut és elérhető
* Redis fut és elérhető
* systemd service engedélyezve van
* a domain helyes helyre mutat

### 15.3 Konfigurációs feltételek

* a `.env` vagy environment file helyes
* a systemd service ténylegesen betölti a környezeti változókat
* a connection string helyes
* a service secret-ek megfelelnek a Realm oldali konfigurációnak

### 15.4 Health check ellenőrzések

* `health/live` sikeresen válaszol
* `health/ready` sikeresen válaszol
* az alkalmazás nem áll le indulás után

### 15.5 Admin plane ellenőrzés

* admin végpont csak engedélyezett hálózatból érhető el
* admin HMAC hitelesítés működik
* tiltott hálózatról a hozzáférés elutasításra kerül

### 15.6 Redis és MySQL elérhetőség

* MySQL kapcsolat működik
* Redis kapcsolat működik
* productionben a distributed nonce store ténylegesen Redisre épül

### 15.7 CI állapot

* a build sikeres
* a security-critical tesztek sikeresek
* a release artifact a várt verzió

### 15.8 Ismert korlátok

* a pontos teljes séma csak a kód, a bootstrap SQL és a migrációk együttese alapján tekinthető véglegesnek
* a command mode pontos CLI szintaxisa külön operátori dokumentációt igényelhet
* a zero-downtime GameData frissítés vagy teljes stage-and-swap modell külön vizsgálatot igényelhet, ha még nincs végigvezetve

---

## 16. Ismert korlátok és feltételezések

### 16.1 Feltételezések

* a szolgáltatás .NET alapú backend binárisként fut
* a környezeti változók systemd `EnvironmentFile` használatával kerülnek betöltésre
* a reverse proxy réteg Nginx
* a production nonce store Redis alapú

### 16.2 Ami nem ellenőrizhető teljes bizonyossággal a kapott anyagból

* a teljes és végleges adatbázis-séma minden oszlopa
* az összes migration fájl tartalma
* az összes command mode pontos CLI szintaxisa
* a teljes production Nginx konfiguráció
* a tényleges backup és restore gyakorlat

### 16.3 Mi kellene a teljesen végleges dokumentációhoz

* bootstrap SQL vagy migrációs fájlok teljes készlete
* production Nginx konfiguráció teljes változata
* systemd service végleges fájlja
* deploy pipeline vagy release folyamat leírása
* a RealmCore oldal auth és service konfigurációja
* hálózati topológiai diagram
* production monitoring / alerting leírás

---

## 17. Rövid összefoglaló

A `GnosisAuthServer` az MMO backend központi Auth és globális metadata szolgáltatása. Kezeli a játékos-hitelesítést, a realm listát, a belső HMAC-hitelesített kommunikációt, a globális GameData kiszolgálását, a schema delivery folyamatot és az adminisztrációs műveleteket.

A modul production üzemeltetése csak akkor tekinthető megfelelőnek, ha:

* a szolgáltatás reverse proxy mögött fut
* a secretek nem a repo-ban vannak
* a Redis alapú replay-védelem aktív
* az admin sík hálózatilag is izolált
* a MySQL és Redis stabilan elérhető
* a startup-validációk és az operátori checklist is teljesül

