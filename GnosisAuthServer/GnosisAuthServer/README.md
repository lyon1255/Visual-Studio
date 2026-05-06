# GnosisAuthServer README

## 1. Mi ez a projekt?

A `GnosisAuthServer` a Gnosis Online rendszer globalis Auth API-ja.

Ez a szolgaltatas felel a kovetkezokert:

* jatekos bejelentkeztetes
* access token kiadas
* realm lista kiszolgalasa
* official realm status nyilvantartasa
* globalis, canonical GameData tarolasa
* admin muveletek kiszolgalasa

Ez a projekt nem a teljes backend, hanem a rendszer egyik kozponti eleme.

A hosszabb tavu architekturaban a szerepek igy neznek ki:

* **AuthApi**

  * globalis auth
  * realm lista
  * canonical GameData
* **RealmCoreApi**

  * save/load
  * merged GameData cache
  * zone info
  * realm szintu logika
* **NodeAgent**

  * zone process inditas es felugyelet
* **GameServer**

  * konkret gameplay es zonafuttatas

Az `AuthApi` magasabb bizalmi szintu komponens, mint a realm vagy a jatek szerverek. Ezert productionben nem szabad ugy kezelni, mint egy egyszeru publikus weboldalt.

---

## 2. Fobb felelossegi korok

### 2.1 Jatekos auth

Az Auth API fogadja a login kerelmeket, ellenorzi az auth folyamatot, es access tokent ad vissza.

### 2.2 Realm lista

Az Auth API adja vissza, hogy milyen official realmek lathatoak online-nak.

### 2.3 Official realm heartbeat

Az official RealmCoreApi hitelesitett heartbeatet kuld ide. Az Auth API ezek alapjan tudja, hogy egy realm elerheto-e.

### 2.4 Global GameData

Az Auth API tarolja az alap, canonical GameData-t, peldaul:

* Items
* Entities
* Quests
* Spells
* Auras

A kesobbi RealmCoreApi ezt lekerni, cache-elni es merge-elni fogja a sajat override-jaival.

### 2.5 Admin muveletek

Az admin endpointokkal lehet peldaul:

* realmeket kezelni
* global GameData-t cserelni
* bizonyos fejlesztoi muveleteket vegrehajtani

---

## 3. Fajl- es mappaszerkezet

Ez a README **nem** szamol a `/opt/gnosis/authapi/src` mappaval.
A source kod szerverre telepitese **nem szukseges**.

A telepiteshez csak a publisholt output kell.

Javasolt szerkezet:

```text
/opt/gnosis/authapi/
  app/
    GnosisAuthServer
    GnosisAuthServer.pdb
    appsettings.json
    appsettings.Development.json
    dotnet-tools.json
    appsettings.Production.json
    keys/
      auth_private.pem
      auth_public.pem
    logs/
```

### Fontos

A te publish formadban **nem `.dll` fajl** jon letre, hanem egy futtathato fajl:

```text
GnosisAuthServer
```

Ez azt jelenti, hogy a systemd service **nem** `dotnet GnosisAuthServer.dll` modon fog indulni, hanem **kozvetlenul a binarist** kell futtatni.

---

## 4. A projekt fo reszei

### `Program.cs`

Az alkalmazas belepesi pontja.

Feladata:

* konfiguracio betoltese
* szolgaltatasok regisztralasa
* security pipeline felallitasa
* adatbazis kapcsolat ellenorzese
* Kestrel inditasa

### `Controllers/AuthController.cs`

A kliens oldali auth folyamathoz tartozo vegpontok.

Tipikus feladatai:

* login
* token kiadas
* `me`
* realm lista

### `Controllers/RealmStatusController.cs`

A hivatalos realm heartbeat endpoint.

Tipikus feladatai:

* heartbeat fogadas
* realm status frissites
* unhealthy realm kezeles

### `Controllers/AdminRealmsController.cs`

Admin vegpontok a realm registryhez.

Tipikus feladatai:

* realm letrehozas
* realm modositas
* realm engedelyezes / tiltas
* service auth adatok kezelese

### `Controllers/GameDataController.cs`

A globalis GameData kiszolgalasa es admin kezeles.

Tipikus feladatai:

* version endpoint
* snapshot endpoint
* admin replace endpoint

### `Controllers/HealthController.cs`

Health check vegpontok.

Tipikus feladatai:

* `live`
* `ready`

### `Data/`

Az adatbazis modellek es a DbContext.

Tipikus fajlok:

* `Account.cs`
* `Realm.cs`
* `RealmHeartbeatNonce.cs`
* `MasterDbContext.cs`

### `Models/`

A request, response es GameData contract modellek.

### `Options/`

A konfiguracios osztalyok.

### `Security/`

A JWT, HMAC, nonce es replay vedelemhez kapcsolodo kod.

### `Services/`

Az uzleti logika, peldaul:

* auth service
* token service
* GameData service
* Steam validator
* nonce service

### `Sql/bootstrap.sql`

Az adatbazis alap schema-ja.

Ez az egyetlen source jellegu fajl, amit az elso telepiteshez kulon fel kell tolteni a szerverre, ha nincs automatikus migration.

---

## 5. Security modell roviden

A projekt security modellje az alabbi alapelvekre epul:

* nincs nyitott `AllowAnyOrigin` policy productionben
* nincs automatikus realm regisztracio egy random heartbeat alapjan
* a JWT RSA kulcspaar alapon megy
* a service-to-service auth HMAC + nonce alapu
* replay vedelem van
* az Auth API publikus oldala reverse proxy mogott fut
* az alkalmazas maga csak helyi vagy belso endpointon hallgasson
* a MySQL es Redis ne legyen publikus internetre nyitva
* admin endpoint ne legyen barki szamara elerheto

---

## 6. Telepitesi modell

Ez a README az alabbi egyszeru uzemi moddal szamol:

* 1 darab Ubuntu VPS
* AuthApi a sajat gepen fut
* Nginx reverse proxy kezeli a publikus bejovo forgalmat
* MySQL helyben fut
* kesobb RealmCore es NodeAgent is mehet ugyanarra a gepe vagy kulon gepre

A lenyeg:

* **AuthApi** -> helyi porton hallgat
* **Nginx** -> publikus HTTPS
* **MySQL** -> helyi eleres
* **JWT kulcsok** -> kulon PEM fajlokban

---

## 7. Mire van szukseg az elso inditashoz?

Szukseges csomagok:

* nginx
* mysql-server
* openssl
* unzip

Ha a szerveren **nem** akarsz buildelni, akkor a source kod es a `dotnet publish` szerver oldalon **nem szukseges**.

Mivel te Visual Studio-bol publisholsz, ez a helyes folyamat:

1. Visual Studio-bol publish
2. a publish outputot feltoltod a VPS-re
3. kulon feltoltod a `bootstrap.sql` fajlt
4. letrehozod az RSA kulcsokat
5. letrehozod az `appsettings.Production.json` fajlt
6. beallitod a MySQL adatbazist
7. elinditod az alkalmazast
8. utana systemd ala rakod
9. vegul Nginx reverse proxy moge teszed

---

## 8. A helyes telepitesi celmappa

A publisholt fajlokat **kozvetlenul** ide kell feltolteni:

```text
/opt/gnosis/authapi/app
```

Tehat **nem** igy:

```text
/opt/gnosis/authapi/app/GnosisAuthServer/
```

hanem igy:

```text
/opt/gnosis/authapi/app/GnosisAuthServer
/opt/gnosis/authapi/app/appsettings.json
/opt/gnosis/authapi/app/appsettings.Development.json
/opt/gnosis/authapi/app/dotnet-tools.json
```

A `keys` mappat is itt kell letrehozni:

```text
/opt/gnosis/authapi/app/keys
```

---

## 9. Elso telepites lepesrol lepesre

## 9.1 Mappak letrehozasa

```bash
sudo mkdir -p /opt/gnosis/authapi/app
sudo mkdir -p /opt/gnosis/authapi/app/keys
sudo mkdir -p /opt/gnosis/authapi/app/logs
```

## 9.2 A publisholt fajlok feltoltese

A sajat gepeden publishold a projektet, majd a publish output **tartalmat** toltsd fel ide:

```text
/opt/gnosis/authapi/app
```

Fontos:

* nem a publish mappat mint almappat
* hanem a publish mappaban levo fajlokat

A vegeredmeny legyen peldaul:

```text
/opt/gnosis/authapi/app/GnosisAuthServer
/opt/gnosis/authapi/app/GnosisAuthServer.pdb
/opt/gnosis/authapi/app/appsettings.json
```

## 9.3 Futtathato jog ellenorzese

Ha Linuxon a binaris nem futtathato, add meg neki:

```bash
chmod +x /opt/gnosis/authapi/app/GnosisAuthServer
```

## 9.4 `bootstrap.sql` feltoltese

A source kod teljes feltoltese **nem kell**, de az adatbazis schema miatt a `bootstrap.sql` fajlt fel kell tenni valahova.

Javasolt hely:

```text
/opt/gnosis/authapi/bootstrap.sql
```

Tehat a sajat gepedrol a `Sql/bootstrap.sql` fajlt kulon told fel ide.

## 9.5 MySQL adatbazis letrehozasa

Lepj be MySQL-be:

```bash
sudo mysql
```

Majd futtasd:

```sql
CREATE DATABASE gnosis_auth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE USER 'gnosis_auth'@'127.0.0.1' IDENTIFIED BY 'EROS_DB_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'127.0.0.1';

CREATE USER 'gnosis_auth'@'localhost' IDENTIFIED BY 'EROS_DB_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'localhost';

FLUSH PRIVILEGES;
EXIT;
```

**Mi tortenik itt?**

* letrejon az adatbazis
* letrejon a DB user
* megkapja a jogokat
* kulon letrehozzuk `127.0.0.1`-re es `localhost`-ra is, hogy ne legyen csatlakozasi problema

## 9.6 A bootstrap SQL lefuttatasa

Fontos:
mivel te nem hasznalsz `src` mappat, ez **nem** lesz jo:

```bash
mysql -u gnosis_auth -p gnosis_auth < /opt/gnosis/authapi/src/...
```

A helyes parancs nalad ez:

```bash
mysql -h 127.0.0.1 -u gnosis_auth -p gnosis_auth < /opt/gnosis/authapi/bootstrap.sql
```

Amikor a jelszot irod, a terminal nem fog karaktereket mutatni. Ez normalis.

## 9.7 JWT RSA kulcsok letrehozasa

Az alkalmazas RSA kulcspaarat var PEM fajlokban. Ezeket neked kell letrehozni.

```bash
mkdir -p /opt/gnosis/authapi/app/keys
openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

Ha ezek nem leteznek, az alkalmazas nem fog elindulni.

---

## 10. `appsettings.Production.json` letrehozasa

A `Production` konfiguraciot itt kell letrehozni:

```text
/opt/gnosis/authapi/app/appsettings.Production.json
```

### Fontos

A JSON szabvany **nem tamogat kommenteket**.
Ezert az alabbi pelda **magyarazo jellegu**. A `//` kezdetu sorokat hasznalhatod mintanak, de ha tenyleges JSON fajlt hozol letre, akkor a kommenteket torolni kell.

Az alabbi pelda azt mutatja, hogy **melyik sort mikor es mire kell modositani**.

```json
{
  "Urls": "http://127.0.0.1:5158",
  "Database": {
    "ConnectionString": "Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=EROS_DB_JELSZO;SslMode=Preferred;"
  },
  "Jwt": {
    "PrivateKeyPemPath": "/opt/gnosis/authapi/app/keys/auth_private.pem",
    "PublicKeyPemPath": "/opt/gnosis/authapi/app/keys/auth_public.pem",
    "Issuer": "Gnosis.Auth",
    "Audience": "Gnosis.Clients",
    "AccessTokenMinutes": 20,
    "KeyId": "gnosis-auth-main"
  },
  "Steam": {
    "Enabled": true,
    "AppId": 123456,
    "PublisherKey": "STEAM_PUBLISHER_KEY",
    "AllowMockTicketsInDevelopment": false
  },
  "RealmRegistry": {
    "HeartbeatTimeoutSeconds": 90,
    "HideUnhealthyRealms": true
  },
  "ServiceAuth": {
    "Enabled": true,
    "AllowedClockSkewSeconds": 30,
    "NonceTtlSeconds": 90,
    "Clients": [
      {
        "ServiceId": "official-eu-realm-core",
        "Secret": "EROS_REALMCORE_SECRET",
        "Roles": [ "official-realm-heartbeat.write", "realm-gamedata.read" ],
        "AllowedRealmIds": [ "official-eu-1" ]
      }
    ]
  },
  "Admin": {
    "Enabled": true,
    "HeaderName": "X-Gnosis-Admin-Key",
    "ApiKey": "EROS_ADMIN_KULCS",
    "AllowedIpAddresses": [ "127.0.0.1" ]
  },
  "Security": {
    "RequireHttps": false,
    "KnownProxies": [ "127.0.0.1" ]
  },
  "Cors": {
    "AllowedOrigins": [ "https://auth.pelda.hu" ]
  }
}
```

### Melyik sort mire kell modositani?

#### `"Urls": "http://127.0.0.1:5158"`

Ez azt mondja meg, hogy az Auth API hol hallgasson.

Mit allits be?

* egy szerveres uzemben jo a `127.0.0.1:5158`
* ez azt jelenti, hogy csak helyben erheto el
* a publikus forgalmat majd az Nginx tovabbitja ide

Mikor kell modositani?

* csak akkor, ha masik portot akarsz hasznalni

#### `"ConnectionString"`

Ez az adatbazis kapcsolat.

Mit allits be?

* a sajat adatbazis nevedet
* a sajat DB usert
* a sajat DB jelszot

Mikor kell modositani?

* mindig, mert ez szerverfuggo

#### `"PrivateKeyPemPath"` es `"PublicKeyPemPath"`

Ez a JWT RSA kulcsok helye.

Mit allits be?

* pontosan oda mutasson, ahova a PEM fajlokat letrehoztad

Mikor kell modositani?

* ha nem az alap `/opt/gnosis/authapi/app/keys/` utvonalat hasznalod

#### `"Issuer"`

A token kibocsato neve.

Mit allits be?

* maradhat `Gnosis.Auth`

Mikor kell modositani?

* csak ha kesobb tobb auth providered lenne

#### `"Audience"`

A token celkozonsege.

Mit allits be?

* maradhat `Gnosis.Clients`

Mikor kell modositani?

* ha a token ellenorzo oldalon mas ertekre epitesz

#### `"AccessTokenMinutes": 20`

A jatekos access token elettartama percben.

Mit allits be?

* kezdesnek a `20` jo

Mikor kell modositani?

* ha rovidebb vagy hosszabb sessiont akarsz

#### `"KeyId"`

Kulcsazonosito.

Mit allits be?

* maradhat `gnosis-auth-main`

Mikor kell modositani?

* kulcsrotacio esetben

#### `"Steam": { ... }`

A Steam auth beallitasai.

Mit allits be?

* `Enabled`: `true`, ha tenyleg Steam authot akarsz
* `AppId`: a sajat Steam AppId
* `PublisherKey`: a sajat Steam web API/publisher key
* `AllowMockTicketsInDevelopment`: productionben `false`

Mikor kell modositani?

* mindig, amikor valos Steam loginra allsz at

#### `"HeartbeatTimeoutSeconds": 90`

Mennyi ido utan tekintsuk a realm-et lejartnak, ha nem jon uj heartbeat.

Mit allits be?

* `90` jo kezdesnek

Mikor kell modositani?

* ha mas heartbeat periodust hasznalsz

#### `"ServiceAuth"`

A belso szolgaltatasok hitelesitese.

Mit allits be?

* a RealmCore service ID-jat
* a sajat eros shared secretet
* a szerepkoroket
* az engedelyezett realm ID-kat

Mikor kell modositani?

* amikor meglesz a RealmCore konkret service identity-je

#### `"Admin"`

Az admin endpointok vedelme.

Mit allits be?

* egy eros admin API kulcsot
* a helyes header nevet
* az engedelyezett IP-ket

Mikor kell modositani?

* mindig production elott

#### `"RequireHttps": false`

Ez csak az **elso boothoz** legyen ideiglenesen `false`.

Mit allits be?

* elso tesztnel `false`
* miutan Nginx HTTPS megy, `true`

#### `"KnownProxies": [ "127.0.0.1" ]`

Melyik reverse proxy megbizhato.

Mit allits be?

* ha az Nginx ugyanazon a gepen fut, jo a `127.0.0.1`
* ha kulon gepen lenne, akkor annak az IP-je

#### `"AllowedOrigins"`

Bongeszos kliensekhez CORS lista.

Mit allits be?

* ha nincs bongeszos frontend, lehet minimalis
* ha van admin frontend vagy launcher web oldal, akkor annak a domainjet

---

## 11. Kezi inditas teszthez

Mivel nalad nem `.dll`, hanem futtathato binaris van, a helyes inditas:

```bash
cd /opt/gnosis/authapi/app
ASPNETCORE_ENVIRONMENT=Production ./GnosisAuthServer
```

Ha ez jo, a masik terminalban tesztelheto:

```bash
curl http://127.0.0.1:5158/health/live
curl http://127.0.0.1:5158/health/ready
```

Ha nem indul, nezd meg:

* megvannak-e a PEM fajlok
* jo-e a DB connection string
* lefutott-e a `bootstrap.sql`

---

## 12. systemd service letrehozasa

Hozz letre kulon rendszerfelhasznalot:

```bash
sudo useradd --system --home /opt/gnosis/authapi --shell /usr/sbin/nologin gnosisauth
sudo chown -R gnosisauth:gnosisauth /opt/gnosis/authapi
```

Service file:

```bash
sudo nano /etc/systemd/system/gnosis-authapi.service
```

Tartalom:

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

Logok:

```bash
journalctl -u gnosis-authapi -f
```

---

## 13. Nginx reverse proxy beallitasa

A javasolt topologia:

* az Auth API csak helyben fusson
* az Nginx fogadja a publikus kero forgalmat
* az Nginx tovabbitson `127.0.0.1:5158`-ra

Site file:

```bash
sudo nano /etc/nginx/sites-available/gnosis-authapi
```

Pelda:

```nginx
server {
    listen 80;
    server_name auth.pelda.hu;

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

Aktivalas:

```bash
sudo ln -s /etc/nginx/sites-available/gnosis-authapi /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

Miutan a HTTPS be van allitva, vissza kell allitani:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ]
}
```

---

## 14. Hibas inditas leggyakoribb okai

### `JWT private key file was not found`

Ok:

* nincs privat PEM fajl
* rossz utvonal van a configban

Megoldas:

* generald le a kulcsokat
* abszolut pathot hasznalj

### `Access denied for user ...`

Ok:

* rossz MySQL user
* rossz host
* nincs `localhost` es `127.0.0.1` kulon kezelve

Megoldas:

* hozd letre mindket user host variaciot
* hasznald a `-h 127.0.0.1` kapcsolot

### `ready` health check hibas

Ok:

* nincs schema
* rossz DB kapcsolat
* nincs futo MySQL

### `502 Bad Gateway`

Ok:

* az app nem fut
* rossz a port
* rossz az Nginx proxy beallitas

### App el sem indul systemd alatt

Ok:

* nincs `chmod +x`
* rossz `ExecStart`
* a binaris nincs az `app` mappaban

---

## 15. Mit nem kell telepiteni?

A kovetkezok **nem kotelezoek** a szerverre:

* teljes source kod
* `/opt/gnosis/authapi/src`
* Visual Studio
* `dotnet publish` szerver oldali futtatasa

Ha Windows gepen publisholsz es feltoltod a publish outputot, akkor a VPS-en eleg:

* `app/`
* `bootstrap.sql`
* `keys/`
* `appsettings.Production.json`

---

## 16. Gyors osszefoglalo

A helyes telepitesi sorrend:

1. letrehozod az `app` mappat
2. feltoltod a publisholt fajlokat az `app` mappaba
3. futtathato jogot adsz a `GnosisAuthServer` binarisnak
4. feltoltod kulon a `bootstrap.sql` fajlt
5. letrehozod a MySQL adatbazist es usereket
6. lefuttatod a bootstrap SQL-t
7. legeneralod az RSA PEM kulcsokat
8. letrehozod az `appsettings.Production.json` fajlt
9. kezileg kiprobalod a binarist
10. systemd service-be rakod
11. Nginx moge teszed
12. HTTPS utan `RequireHttps=true`

Ez a helyes modell a te jelenlegi publish formatumodhoz, ahol az output egy futtathato `GnosisAuthServer` fajlt tartalmaz, es **nem** `.dll` alapu inditas tortenik.
