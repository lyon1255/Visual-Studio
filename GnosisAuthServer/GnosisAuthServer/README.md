# GnosisAuthServer Dokumentáció

## 1. A Dokumentum Célja

Ez a dokumentum a `GnosisAuthServer` (Auth API) modul részletes műszaki leírása. A dokumentáció egy nagyobb MMO backend keretrendszer részeként kezeli az Auth API-t, nem pedig egy elszigetelt szolgáltatásként.

A dokumentum célja:

* A modul felelősségi körének pontos rögzítése.
* A rendszerbeli adat- és forgalomutak leírása.
* A szerveroldali üzemeltetés és telepítés szabályainak **lépésről lépésre** történő dokumentálása (kezdőbarát megközelítéssel).
* A MySQL adatszerkezet áttekintése.
* A frissítési és karbantartási folyamatok standardizálása.

---

## 2. Architektúra Áttekintés

### 2.1 A modul célja és felelősségi köre

A `GnosisAuthServer` a globális backend egyik központi "kapuőr" szolgáltatása. Feladata nem a tényleges játéklogika (harcrendszer, mozgás) futtatása, hanem a globális, közös backend funkciók biztosítása az összes többi szerver és kliens számára.

**Fő feladatai:**

1. **Játékos hitelesítés (Authentication):** Steam azonosító (ticket) fogadása, ellenőrzése, az adatbázisban a fiók (account) létrehozása vagy betöltése, majd egy biztonságos "belépőkártya" (JWT Access Token) kiadása a játékosnak.
2. **Szerverlista (Realm) kiszolgálása:** Megmondja a bejelentkezett játékos kliensének, hogy milyen játékszerverek (Realmek) érhetőek el, és azoknak mi az IP címe/URL-je.
3. **Belső kommunikáció (Internal Service):** Fogadja a játékszerverek (RealmCore) "életjeleit" (Heartbeat), és ellenőrzi, hogy azok jogosultak-e a hálózathoz csatlakozni.
4. **Globális Játékadatok (GameData):** Ez a rendszer tárolja a hivatalos tárgyakat, varázslatokat, küldetéseket. Ezt az adatot osztja szét a játékszervereknek.
5. **Adminisztrációs vezérlés:** Fiókok kitiltása (ban), új játékszerverek regisztrálása, játékadatok frissítése.

### 2.2 Kommunikáció a többi egységgel (Adatáramlás)

* **Kliens (Játékos) -> Auth API:** A játék elindításakor a kliens ide küldi a Steam belépési adatokat. Cserébe kap egy tokent. A kliens soha nem tudja közvetlenül módosítani a játékadatokat, csak olvashatja a szerverlistát.
* **RealmCore (Játékszerver) -> Auth API:** A játékszerver percenként szól az Auth-nak, hogy "Élek, és 150 játékos van rajtam". Emellett induláskor innen tölti le a legfrissebb tárgyakat és varázslatokat.
* **Admin -> Auth API:** Egy titkosított, rejtett végponton keresztül a fejlesztő (te) utasíthatod az Auth API-t, hogy frissítse a globális adatbázist vagy tiltsa ki egy csaló fiókját.

### 2.3 Hálózati rétegek

* **Belső port (Kestrel):** A modul a VPS-en belül a `127.0.0.1:5158`-as porton fut. Ez kívülről, az internetről **nem** elérhető.
* **Publikus kapu (Nginx):** A külvilág felé az Nginx webszerver kommunikál a szabványos `443`-as (HTTPS) porton. Az Nginx fogadja a titkosított adatforgalmat, visszafejti, és továbbítja a belső 5158-as portra.
* **Biztonság (SSL/TLS):** A rendszer szigorúan megköveteli a HTTPS kapcsolatot. HTTP (80-as port) próbálkozások automatikusan el lesznek utasítva vagy átirányítva HTTPS-re.

### 2.4 A modul összes parancsa (Végpontok)

**Nyilvános végpontok (Játékosoknak):**

* `POST /api/auth/steam` : Elküldi a Steam Ticketet, cserébe visszaadja a belépési adatokat és a JWT Tokent.
* `GET /api/auth/me` : Lekéri a bejelentkezett játékos saját fiókadatait (Ellenőrzi a tokent).
* `GET /api/auth/servers` : Visszaadja a játékosnak az elérhető és aktív játékszerverek listáját.

**Belső végpontok (Játékszervereknek - HMAC titkosítással):**

* `POST /api/internal/realms/heartbeat` : A játékszerver ezen jelenti az állapotát (Játékosok száma, aktív-e).
* `GET /api/internal/gamedata/version` : Lekérdezi, hogy mi az aktuális globális játékadat (GameData) verziószáma.
* `GET /api/internal/gamedata/snapshot` : Letölti az összes hivatalos tárgyat, varázslatot egy nagy csomagban.
* `GET /api/internal/schema/manifest` : Lekéri az adatbázis frissítési csomagok (migrációk) listáját.

**Adminisztrációs végpontok (Csak fejlesztőknek):**

* `GET /api/admin/realms` : Kilistázza az összes regisztrált játékszervert (az offline-okat is).
* `POST /api/admin/realms` : Új játékszerver hozzáadása a rendszerhez.
* `PUT /api/admin/realms/{realmId}` : Egy meglévő játékszerver adatainak (pl. neve, IP címe) módosítása.
* `POST /api/admin/gamedata/replace` : Az Unity Editorból ezen keresztül töltöd fel a legújabb fegyvereket és tárgyakat az adatbázisba.
* `PUT /api/admin/accounts/ban` : Egy adott játékos kitiltása a rendszerből (minden szerverről).

---

## 3. VPS Szerveroldali Beállítások (Részletes útmutató)

Ez a szekció lépésről lépésre végigvezet azon, hogyan kell egy teljesen "szűz" Ubuntu szervert felkészíteni a GnosisAuthServer futtatására.

### 3.1 Szükséges környezeti elemek telepítése

Lépj be a VPS-edre SSH-n keresztül, és futtasd le ezeket a parancsokat. Ez felteszi a webszervert (Nginx), az adatbázist (MySQL) és a memóriatárat (Redis).

```bash
sudo apt update
sudo apt install -y nginx redis-server mysql-server openssl curl unzip

```

### 3.2 Dedikált felhasználó és mappák létrehozása

Biztonsági okokból az alkalmazást nem a `root` (rendszergazda) felhasználóval futtatjuk, hanem létrehozunk neki egy saját, korlátozott jogkörű felhasználót (`gnosisauth`).

```bash
# Létrehozzuk a felhasználót
sudo useradd --system --home /opt/gnosis/authapi --shell /usr/sbin/nologin gnosisauth

# Létrehozzuk a fő mappát
sudo mkdir -p /opt/gnosis/authapi/app
sudo mkdir -p /opt/gnosis/authapi/app/keys

# Átadjuk a mappa tulajdonjogát az új felhasználónak
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi

```

Minden fájlt (a lefordított C# programodat) az `/opt/gnosis/authapi/app` mappába kell majd feltöltened (pl. FTP-n vagy SCP-vel).

### 3.3 JWT Titkosítási kulcsok generálása

A rendszer ezekkel a kulcsokkal írja alá a játékosok belépési tokenjeit. Ezt a szerveren kell legenerálni.

```bash
sudo openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
sudo openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem

# Jogosultságok beállítása, hogy senki más ne férhessen hozzá
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi/app/keys
sudo chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
sudo chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem

```

### 3.4 MySQL Adatbázis létrehozása

Lépj be a MySQL parancssorába, és hozd létre az adatbázist, illetve az ahhoz tartozó felhasználót.

```bash
sudo mysql -u root

```

A MySQL parancssorban (a `mysql>` jelzés után) futtasd ezeket:

```sql
CREATE DATABASE gnosis_auth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gnosis_auth'@'127.0.0.1' IDENTIFIED BY 'ErosJelszoIde123!';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'127.0.0.1';
FLUSH PRIVILEGES;
EXIT;

```

*(Természetesen az `ErosJelszoIde123!` részt cseréld le a sajátodra).*

### 3.5 Hol és hogyan állítsuk be az Environment (Környezeti) változókat?

A jelszavakat és titkos kulcsokat SOHA nem tároljuk a program kódjában vagy konfigurációs fájljaiban. Erre egy rejtett `.env` fájlt használunk. Ezt a fájlt a rendszer automatikusan beolvassa a program indulásakor.

**1. Hozd létre az `.env` fájlt:**

```bash
sudo nano /opt/gnosis/authapi/.env

```

**2. Másold bele az alábbiakat, és töltsd ki a saját adataiddal:**
*(Figyelem: Ebben a fájlban nincsenek szóközök az egyenlőségjelek mellett, és idézőjelek sem kellenek a legtöbb esetben!)*

```ini
ASPNETCORE_ENVIRONMENT=Production

# Adatbázis kapcsolat (Írd át a jelszót arra, amit a MySQL-ben megadtál!)
GNOSIS_AUTH__Database__ConnectionString=Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=ErosJelszoIde123!;SslMode=Required;

# JWT Kulcsok elérési útja (Ezeket generáltuk az előbb, ehhez ne nyúlj)
GNOSIS_AUTH__Jwt__PrivateKeyPemPath=/opt/gnosis/authapi/app/keys/auth_private.pem
GNOSIS_AUTH__Jwt__PublicKeyPemPath=/opt/gnosis/authapi/app/keys/auth_public.pem

# Steam Integráció (Ide kellenek a Steamworks adataid)
GNOSIS_AUTH__Steam__AppId=123456
GNOSIS_AUTH__Steam__PublisherKey=A_TE_TITKOS_STEAM_KULCSOD

# Játékszerverek (Realmek) hitelesítő kulcsai (Ezt te találod ki, a Realm is ezt fogja használni)
GNOSIS_AUTH__ServiceAuth__Clients__0__Secret=RealmCoreTitkosKulcs1
GNOSIS_AUTH__ServiceAuth__Clients__1__Secret=RealmCoreTitkosKulcs2

# Adminisztrátori hozzáférés (Ez kell a GameData feltöltéséhez az Unity Editorból)
GNOSIS_AUTH__Admin__ApiKey=SzuperTitkosAdminJelszo123
GNOSIS_AUTH__Admin__AllowedIpNetworks__0=0.0.0.0/0

# Redis beállítások a Heartbeat és biztonság érdekében
GNOSIS_AUTH__NonceStore__UseDistributedCache=true
GNOSIS_AUTH__NonceStore__RedisConnectionString=127.0.0.1:6379,abortConnect=false

```

Mentsd el a fájlt: `Ctrl + X`, majd `Y`, majd `Enter`.

### 3.6 A Systemd Service létrehozása (Automatikus futtatás)

Hogy a programod a háttérben fusson, és szerver újraindulás esetén magától elinduljon, létre kell hoznunk egy szolgáltatást (service). Ennek a fájlnak a feladata, hogy elindítsa a programot, és megmondja neki, hogy olvassa be az előbb létrehozott `.env` fájlt!

**1. Hozd létre a service fájlt:**

```bash
sudo nano /etc/systemd/system/gnosis-authapi.service

```

**2. Másold bele ezt a tartalmat:**

```ini
[Unit]
Description=Gnosis Auth API Service
After=network.target mysql.service redis-server.service

[Service]
WorkingDirectory=/opt/gnosis/authapi/app
ExecStart=/opt/gnosis/authapi/app/GnosisAuthServer
User=gnosisauth
Group=gnosisauth

# ITT OLVASSA BE AZ ADATAIDAT A RENDSZER:
EnvironmentFile=/opt/gnosis/authapi/.env

Restart=always
RestartSec=5
SyslogIdentifier=gnosis-authapi

[Install]
WantedBy=multi-user.target

```

Mentsd el: `Ctrl + X`, majd `Y`, majd `Enter`.

### 3.7 Nginx fordított proxy (Reverse Proxy) beállítása

Be kell állítanunk, hogy az internetről érkező forgalmat az Nginx továbbítsa a programod felé.

**1. Nyisd meg az Nginx beállításait:**

```bash
sudo nano /etc/nginx/sites-available/default

```

**2. Illeszd be ezt a blokkot (vagy módosítsd a meglévőt):**
*(Cseréld ki az `auth.playgnosis.hu`-t a saját domainodra!)*

```nginx
server {
    listen 80;
    server_name auth.playgnosis.hu;
    
    # Automatikus átirányítás HTTPS-re
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name auth.playgnosis.hu;

    # Ide kellenek az SSL tanúsítványok (pl. Certbot-tal generálva)
    ssl_certificate /etc/letsencrypt/live/auth.playgnosis.hu/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/auth.playgnosis.hu/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5158;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_cache_bypass $http_upgrade;
    }
}

```

Mentsd el, majd teszteld az Nginxet és indítsd újra:

```bash
sudo nginx -t
sudo systemctl restart nginx

```

### 3.8 Az alkalmazás elindítása

Ha minden fájlt feltöltöttél az `/opt/gnosis/authapi/app` mappába (a lefordított C# alkalmazást), indítsd el a rendszert!

```bash
sudo systemctl daemon-reload
sudo systemctl enable gnosis-authapi
sudo systemctl start gnosis-authapi

```

**Ellenőrzés, hogy fut-e hibátlanul:**

```bash
sudo systemctl status gnosis-authapi

```

---

## 4. MySQL Adatbázis Struktúra

Ez a modul a következő alapvető adatbázis táblákat használja a működéshez. Ezt a struktúrát a programod Entity Framework Core (vagy hasonló ORM) moduljának kell felépítenie.

### 4.1 `accounts` (Felhasználói Fiókok)

Ez a tábla tárolja a játékosokat.

| Mező | Típus | Index | Alapértelmezett | FK | Magyarázat |
| --- | --- | --- | --- | --- | --- |
| `id` | INT UNSIGNED | PK | Auto Increment | - | Belső, technikai azonosító a rendszerben. A negatív értékeket tiltja az UNSIGNED. |
| `steam_id` | VARCHAR(32) | UNIQUE | Nincs | - | A játékos hivatalos Steam azonosítója. UNIQUE (Egyedi), mert egy Steam profilhoz csak egy fiók tartozhat. |
| `display_name` | VARCHAR(128) | Opcionális | NULL | - | A játékos utoljára használt látható neve. |
| `is_banned` | TINYINT(1) | Indexelt | 0 | - | Igaz/Hamis érték. Ha 1, a játékos nem tud belépni. |
| `ban_reason` | VARCHAR(256) | - | NULL | - | Az adminisztrátor által megadott kitiltási indoklás. |
| `created_at_utc` | DATETIME | Indexelt | CURRENT_TIMESTAMP | - | A fiók létrehozásának pontos ideje. |
| `updated_at_utc` | DATETIME | Indexelt | CURRENT_TIMESTAMP | - | A fiók utolsó módosításának ideje. |

### 4.2 `realms` (Játékszerverek Listája)

Ebben a táblában tartjuk nyilván az elérhető szervereket (pl. Forest Zone, PVP Server).

| Mező | Típus | Index | Alapértelmezett | FK | Magyarázat |
| --- | --- | --- | --- | --- | --- |
| `id` | INT UNSIGNED | PK | Auto Increment | - | Belső azonosító. |
| `realm_id` | VARCHAR(64) | UNIQUE | Nincs | - | A szerver belső kódneve (pl. 'eu-official-1'). |
| `display_name` | VARCHAR(128) | Indexelt | Nincs | - | A játékosok által látott név (pl. "Gnosis Hivatalos EU"). |
| `public_base_url` | VARCHAR(255) | - | NULL | - | A szerver publikus IP címe / URL-je és portja. A FishNet ide fog csatlakozni. |
| `is_official` | TINYINT(1) | Indexelt | 0 | - | Igaz/Hamis. Jelöli, hogy ez hivatalos, vagy közösségi szerver-e. |
| `status` | VARCHAR(32) | Indexelt | 'offline' | - | Szöveges állapot (online, offline, karbantartás). |
| `current_players` | INT UNSIGNED | - | 0 | - | Az éppen fent lévő játékosok száma (a Heartbeat frissíti percenként). |
| `last_heartbeat_at` | DATETIME | Indexelt | NULL | - | Mikor szólt utoljára a szerver. Ha ez túl régi, a rendszer offline-nak jelöli. |

### 4.3 Globális Játékadatok (`gamedata_versions` és `gamedata_items`)

Ezek tárolják az Unityből feltöltött tárgyakat és fegyvereket.

**`gamedata_versions`**

| Mező | Típus | Index | Alapértelmezett | FK | Magyarázat |
| --- | --- | --- | --- | --- | --- |
| `version_number` | INT UNSIGNED | UNIQUE | Nincs | - | A folyamatosan növekvő verziószám. |
| `is_active` | TINYINT(1) | Indexelt | 0 | - | Jelöli, hogy jelenleg melyik a "hivatalos" éles verzió. Egyszerre csak egy lehet 1-es. |

**`gamedata_items`**

| Mező | Típus | Index | Alapértelmezett | FK | Magyarázat |
| --- | --- | --- | --- | --- | --- |
| `id` | INT UNSIGNED | PK | Auto Increment | - | Technikai kulcs. |
| `version_number` | INT UNSIGNED | Indexelt | Nincs | `gamedata_versions.version_number` | Jelzi, hogy ez a fegyver/tárgy melyik verzióhoz tartozik. Ez a Külső Kulcs (Foreign Key). |
| `asset_id` | VARCHAR(100) | Indexelt | Nincs | - | Az Unity belső azonosítója a tárgyhoz (pl. "sword_iron_01"). |
| `json_data` | LONGTEXT | - | Nincs | - | Ide kerül az Unity által generált teljes JSON adat (Sebzés, név, ikon azonosító). Azért LONGTEXT, mert a mérete akármekkora lehet. |

**Relációk és Kritikus mezők magyarázata:**

* Az `accounts` tábla `steam_id` mezője szigorúan **UNIQUE** kell legyen. Ha nem lenne az, egy játékos véletlenül kétszer is beregisztrálhatna a rendszerbe ugyanazzal a Steammel, ami tönkretenné az azonosítást.
* A `gamedata_items` egy-a-sokhoz (One-to-Many) kapcsolatban van a `gamedata_versions` táblával. Egy verzióhoz rengeteg tárgy tartozhat. Ezt a kapcsolatot a `version_number` köti össze.

---

## 5. Deployment (Frissítés) és Karbantartás

Amikor továbbfejleszted a programot, és új verziót akarsz feltölteni a szerverre, kövesd ezt a biztonságos folyamatot!

### 5.1 A frissítés biztonságos menete (Adatvesztés nélkül)

1. **Fordítsd le (Publish) a kódot** a gépeden.
2. Lépj be a VPS-re, és **állítsd le** a jelenlegi programot:
```bash
sudo systemctl stop gnosis-authapi

```


3. **Töröld a régi fájlokat**, DE a `keys` mappát és az `.env` fájlt Szigorúan Hagyd Meg!
```bash
# Töröl mindent az app mappában, KIVÉVE a kulcsokat
sudo find /opt/gnosis/authapi/app -type f -not -path "*/keys/*" -delete

```


4. **Töltsd fel** az új fájlokat az `/opt/gnosis/authapi/app` mappába.
5. Indítsd el a rendszert újra:
```bash
sudo systemctl start gnosis-authapi

```



### 5.2 Hibakeresés (Mit csinálj, ha nem indul el?)

Ha a program elindul, de valami nem jó, a naplófájlokban (logokban) fogod megtalálni a hiba okát.

**Hogyan nézd meg az alkalmazás hibaüzeneteit?**
Ezzel a paranccsal élőben olvashatod a programod által kiírt piros hibaüzeneteket:

```bash
sudo journalctl -u gnosis-authapi -f

```

*(A kilépéshez nyomj `Ctrl + C`-t).*

**Gyakori hibák és megoldásaik:**

* *Hiba:* "Connection refused" vagy "Access denied for user".
* *Megoldás:* Az `.env` fájlban elírtad a MySQL jelszót, vagy a MySQL szerver nem fut.


* *Hiba:* A böngésző "502 Bad Gateway" hibát ír, amikor megnyitod az oldalt.
* *Megoldás:* Az Nginx működik, de a C# programod leállt a háttérben. Nézd meg a hibaüzenetét a fenti `journalctl` paranccsal!



### 5.3 Biztonsági Mentés (Backup)

Soha ne bízz abban, hogy a VPS nem romlik el. Legalább hetente érdemes lementeni az adatbázist és a titkosítási kulcsokat a saját számítógépedre.

**Adatbázis mentése a VPS-en (egy fájlba):**

```bash
mysqldump -u root -p gnosis_auth > /opt/gnosis/authapi/gnosis_auth_backup.sql

```

Ezt a `gnosis_auth_backup.sql` fájlt, valamint a `/opt/gnosis/authapi/app/keys` mappát egyszerűen töltsd le magadhoz FTP-n keresztül. Ezekből egy esetleges szerver-összeomlás után 10 perc alatt teljesen visszaállítható az egész játékosbázis.