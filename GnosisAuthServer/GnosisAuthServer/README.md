# GnosisAuthServer README

## 1. Mi ez a projekt?

A `GnosisAuthServer` a Gnosis Online rendszer globalis Auth API-ja. Ez a szolgaltatas a belepesi pont a kliens szamara, es ez tarolja a canonical, fejlesztoi oldal altal karbantartott globalis adatokat is. A projekt celja, hogy egy biztonsagosabb, production-kozelibb Auth API legyen, amely kulon valasztja a jatekos-auth, a realm status, az admin muveletek es a globalis GameData kezeleset. Az ASP.NET Core production uzemhez javasolt mintaja Linuxon az, hogy az alkalmazas Kestrel mogott fut, kivulrol pedig egy reverse proxy, tipikusan Nginx fogadja a forgalmat. ([Microsoft Learn][1])

## 2. Magas szintu mukodesi elv

A rendszer a kovetkezo fo szerepkoroket kuloniti el:

* **Kliens -> Auth API**

  * Steam login
  * access token kiadas
  * realm lista lekerese
  * sajat account adatok lekerese

* **Official RealmCore -> Auth API**

  * hivatalos realm heartbeat kuldese
  * globalis GameData lekerese service-to-service hitelesitessel

* **Admin -> Auth API**

  * realm nyilvantartas kezelese
  * globalis GameData cserelese
  * fejlesztoi admin muveletek

Az API erzekeny adatokat kezel, ezert productionben nem ajanlott nyers HTTP-n kitenni az internetre. A Microsoft API projektekhez azt javasolja, hogy ne HTTP->HTTPS redirectre epitsek a vedelmet, hanem az API vagy egyaltalan ne hallgasson nyilvanos HTTP-n, vagy utasitsa el az ilyen kerest. Ez a projekt ugy lett tervezve, hogy Kestrel helyben, loopback cimen fusson, es Nginx legyen a publikus HTTPS reteg. ([Microsoft Learn][2])

## 3. Az Auth API fobb felelossegi korei

### 3.1 Auth

Az Auth API vegzi a jatekos bejelentkeztetest, a tokenkiadast es a realm lista kiszolgalasat.

### 3.2 Realm status

Az Auth API nyilvantartja, hogy mely official realmek latszanak online-nak. Ezt hitelesitett heartbeat alapjan frissiti.

### 3.3 Global GameData

Az Auth API tarolja a globalis, canonical GameData snapshotot:

* Items
* Entities
* Quests
* Spells
* Auras

Ez a globalis adatforras. A kesobbi RealmCore ezt lehuzza, memoriaban cache-eli, es erre engedi ra a sajat realm override-okat.

### 3.4 Admin

Az admin vegpontok realm managementre es globalis GameData frissitesre valok.

---

## 4. Fajl- es mappaszerkezet

Az uj Auth API logikailag a kovetkezo reszekre bomlik.

### `Program.cs`

Az alkalmazas inditopontja.

Feladatai:

* konfiguracio betoltese
* DI kontener felepitese
* adatbazis kapcsolat ellenorzese
* JWT, service auth, CORS, rate limit, health check pipeline konfiguracio
* Kestrel / middleware inditas

### `Controllers/AuthController.cs`

A kliens oldali auth folyamatot kezeli.

Tipikus feladatai:

* Steam login
* access token generalas
* `me` endpoint
* szerverlista endpoint

### `Controllers/RealmStatusController.cs`

A realm heartbeat endpointokat kezeli.

Tipikus feladatai:

* official realm heartbeat fogadas
* realm status frissites

### `Controllers/AdminRealmsController.cs`

Admin vegpontok a realm registry kezelesere.

Tipikus feladatai:

* realm letrehozas
* realm modositasa
* realm tiltasa / engedelyezese
* heartbeat secret vagy service identity kezelese

### `Controllers/GameDataController.cs`

A globalis GameData kiszolgalasat es admin oldali cserjet kezeli.

Tipikus feladatai:

* version endpoint
* snapshot endpoint
* admin replace endpoint

### `Controllers/HealthController.cs`

Egeszsegugyi vegpontok.

Tipikus feladatai:

* `live`
* `ready`

### `Data/`

Az EF Core adatbazis modelljei.

Tipikus fajlok:

* `Account.cs`
* `Realm.cs`
* `RealmHeartbeatNonce.cs`
* `MasterDbContext.cs`

Feladatuk:

* tablaleirasok
* DbSet-ek
* adatbazis mapping

### `Models/`

A request/response DTO-k es a GameData contractok.

Tipikus fajlok:

* `Requests.cs`
* `Responses.cs`
* `GameDataContracts.cs`

Feladatuk:

* API request modellek
* API response modellek
* global snapshot modellek

### `Options/`

A konfiguracios szekciokhoz tartozo strongly typed classok.

Tipikus fajlok:

* `DatabaseOptions.cs`
* `JwtOptions.cs`
* `SteamOptions.cs`
* `ServiceAuthOptions.cs`
* `AdminOptions.cs`
* `SecurityOptions.cs`
* `CorsOptions.cs`
* `RealmRegistryOptions.cs`

Feladatuk:

* `appsettings.json` beallitasok tipusos lekepezese

### `Security/`

A hitelesitesi es alairasi logika.

Tipikus feladatai:

* JWT kulcsok betoltese PEM fajlbol
* JWT token generalas / ellenorzes
* HMAC service-to-service auth
* nonce / replay vedelem

### `Services/`

Az uzleti logika.

Tipikus fajlok:

* Steam validator
* token service
* realm registry service
* GameData service
* nonce service

### `Sql/bootstrap.sql`

Az elso inditashoz szukseges adatbazis schema.

Ez a fajl hozza letre a kezdo tablakat.

### `keys/README.txt`

Rovid leiras a JWT RSA kulcsokrol.

---

## 5. Fuggosegek

A projekt .NET alapu ASP.NET Core Web API. Ubuntu alatt a .NET telepitese csomagkezelo segitsegevel tamogatott. A hivatalos dokumentacio szerint, ha futtatni szeretnenk az alkalmazast, eleg az ASP.NET Core Runtime, ha pedig szerveren akarunk `restore`, `build` vagy `publish` muveleteket vegezni, akkor a `dotnet-sdk-10.0` csomag kell. Ubuntu 22.04+ rendszereken a .NET a hivatalos Ubuntu feedben vagy backports feedben erheto el, es a Microsoft kulon kiemeli, hogy Ubuntu esetben a disztribucio feedjeit erdemes elonyben reszesiteni. ([Microsoft Learn][3])

A projekt hasznal:

* ASP.NET Core
* EF Core
* MySQL provider
* RSA alapu JWT
* HMAC service auth
* rate limiting middleware

Az ASP.NET Core rate limiting middleware be van epitve a platformba, es endpoint szinten vagy globalisan is hasznalhato. ([Microsoft Learn][4])

---

## 6. Security modell roviden

A projekt security modellje az alabbi alapelvekre epul:

* nincs nyitott `AllowAnyOrigin` CORS policy productionben
* nincs auto realm regisztracio heartbeat alapjan
* nincs plaintext JWT symmetric signing key szetszorva minden komponenshez
* a JWT RSA kulcspaarbol generalodik
* a service-to-service auth HMAC es nonce alapu replay vedelemmel mukodik
* az API publikus oldala reverse proxy mogott fut
* az alkalmazas maga lehetoseg szerint csak loopback interface-re kot
* a MySQL kapcsolat productionben titkositott legyen

A Microsoft kulon kiemeli, hogy API projektekhez nem szerencses a sima HTTP->HTTPS redirectre hagyatkozni, mert az API kliens az elso kerest mar elkuldhette HTTP-n. Biztonsagosabb, ha az API eleve nem hallgat nyilvanos HTTP-n. A proxy mogotti uzemnel a `X-Forwarded-*` headerek csak trusted proxyktol fogadhatok el, es ujabb ASP.NET Core verziokban az ismeretlen proxytol jovo ilyen headereket a middleware mar figyelmen kivul hagyja. ([Microsoft Learn][2])

---

## 7. appsettings.json leiras

A projekt konfiguracioja tobb forrasbol allhat:

* `appsettings.json`
* `appsettings.Production.json`
* environment valtozok
* opcionálisan secret file vagy systemd environment

A legfontosabb beallitasok:

### `Urls`

Egyszeru modja annak, hogy az alkalmazas hol hallgasson.

Pelda:

```json
"Urls": "http://127.0.0.1:5158"
```

Ez azt jelenti, hogy az alkalmazas csak a sajat gepen erheto el ezen a porton.

### `Kestrel`

Reszletesebb endpoint konfiguracio.

A Kestrel endpointokat JSON-bol, code-bol vagy URL listabol lehet konfigurálni. A Microsoft dokumentacio szerint a `Kestrel:Endpoints` szekcio productionre alkalmas mod. ([Microsoft Learn][5])

Pelda:

```json
"Kestrel": {
  "Endpoints": {
    "HttpLocal": {
      "Url": "http://127.0.0.1:5158"
    }
  }
}
```

### `Database`

Az adatbazis kapcsolat.

Pelda:

```json
"Database": {
  "ConnectionString": "Server=127.0.0.1;Port=3306;Database=gnosis_auth;User=gnosis_auth;Password=JELSZO;SslMode=Preferred;"
}
```

Mit jelent:

* `Server`: a MySQL szerver cime
* `Port`: a MySQL port, altalaban `3306`
* `Database`: az adatbazis neve
* `User`: a DB user
* `Password`: a DB user jelszava
* `SslMode`: SSL/TLS viselkedes

A MySQL tamogat encrypted kapcsolatot, es `require_secure_transport` beallitassal ki is kenyszeritheto a titkositott kapcsolat. ([Microsoft Learn][6])

### `Jwt`

A jatekos access tokenek kiadasahoz hasznalt RSA kulcsok es JWT metadata.

Pelda:

```json
"Jwt": {
  "PrivateKeyPemPath": "/opt/gnosis/authapi/app/keys/auth_private.pem",
  "PublicKeyPemPath": "/opt/gnosis/authapi/app/keys/auth_public.pem",
  "Issuer": "Gnosis.Auth",
  "Audience": "Gnosis.Clients",
  "AccessTokenMinutes": 20,
  "KeyId": "gnosis-auth-main"
}
```

Mit jelent:

* `PrivateKeyPemPath`: az RSA privat kulcs PEM fajlja
* `PublicKeyPemPath`: az RSA publikus kulcs PEM fajlja
* `Issuer`: a token kibocsatoja
* `Audience`: a token vart kozonsege
* `AccessTokenMinutes`: token elettartam
* `KeyId`: kulcsazonosito

**Fontos:** ha a privat kulcs fajl nem letezik, az alkalmazas indulaskor exceptiont dob. Ez vart viselkedes.

### `Steam`

A Steam alapju loginhoz tartozo beallitasok.

Pelda:

```json
"Steam": {
  "Enabled": true,
  "AppId": 123456,
  "PublisherKey": "STEAM_PUBLISHER_KEY",
  "AllowMockTicketsInDevelopment": false
}
```

Mit jelent:

* `Enabled`: be van-e kapcsolva a Steam validacio
* `AppId`: a jatek Steam AppId-ja
* `PublisherKey`: szerver oldali Steam key
* `AllowMockTicketsInDevelopment`: csak fejlesztoi uzemhez

### `RealmRegistry`

A realm status lathatosaggal kapcsolatos szabalyok.

Pelda:

```json
"RealmRegistry": {
  "HeartbeatTimeoutSeconds": 90,
  "HideUnhealthyRealms": true
}
```

Mit jelent:

* `HeartbeatTimeoutSeconds`: mennyi ideig ervenyes egy heartbeat
* `HideUnhealthyRealms`: a lejart vagy rossz allapotu realmek el legyenek-e rejtve

### `ServiceAuth`

A belso szolgaltatasok hitelesitese.

Pelda:

```json
"ServiceAuth": {
  "Enabled": true,
  "AllowedClockSkewSeconds": 30,
  "NonceTtlSeconds": 90,
  "Clients": [
    {
      "ServiceId": "official-eu-realm-core",
      "Secret": "EROS_SECRET",
      "Roles": [ "official-realm-heartbeat.write", "realm-gamedata.read" ],
      "AllowedRealmIds": [ "official-eu-1" ]
    }
  ]
}
```

Mit jelent:

* `Enabled`: aktiv-e a service auth
* `AllowedClockSkewSeconds`: mennyi idoeltolodas engedett a signed kerelmeknel
* `NonceTtlSeconds`: nonce elettartam replay vedelemhez
* `Clients`: engedelyezett belso kliensek listaja

### `Admin`

Az admin API beallitasai.

Pelda:

```json
"Admin": {
  "Enabled": true,
  "HeaderName": "X-Gnosis-Admin-Key",
  "ApiKey": "NAGYON_EROS_ADMIN_KULCS",
  "AllowedIpAddresses": [ "127.0.0.1" ]
}
```

Mit jelent:

* `Enabled`: aktivak-e az admin endpointok
* `HeaderName`: melyik fejlecben varja az admin kulcsot
* `ApiKey`: admin kulcs
* `AllowedIpAddresses`: mely IP-krol erhetok el az admin muveletek

### `Security`

A reverse proxy es HTTPS policy.

Pelda:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ]
}
```

Mit jelent:

* `RequireHttps`: elvarja-e a HTTPS eredetet
* `KnownProxies`: megbizhato proxyk listaja

### `Cors`

Mely origin-ek erhetoek el bongeszobol.

Pelda:

```json
"Cors": {
  "AllowedOrigins": [ "https://auth.pelda.hu" ]
}
```

Productionben csak konkret origin-ek szerepeljenek.

---

## 8. Miert kell JWT PEM kulcs?

Ez a projekt nem egyszeru shared symmetric stringgel ir ala JWT-t, hanem RSA kulcspaaral. Ennek az az elonye, hogy kesobb mas szolgaltatasok, peldaul a RealmCore, eleg csak a publikus kulcsot ismerjek a token ellenorzeshez, a privat kulcs pedig csak az Auth API szerveren marad.

A hiba:

```text
JWT private key file was not found
```

azt jelenti, hogy a `Jwt:PrivateKeyPemPath` altal megadott fajl nem talalhato.

A kulcsokat kulon kell generalni a szerveren.

Pelda:

```bash
mkdir -p /opt/gnosis/authapi/app/keys
openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

---

## 9. Elso inditas - teljes, lepesrol lepesre

Ez a szakasz Ubuntu szerverhez irodott.

## 9.1 Elokeszites

Frissitsuk a csomaglistat es telepitsuk a szukseges csomagokat:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0 nginx openssl unzip mysql-server
```

A .NET 10 telepitesere Ubuntu alatt a hivatalos csomagkezelos ut tamogatott, az ASP.NET Core Runtime kulon is telepitheto, de szerver oldali `publish`-hoz a `dotnet-sdk-10.0` a kenyelmesebb. ([Microsoft Learn][3])

Ellenorzes:

```bash
dotnet --list-sdks
dotnet --list-runtimes
```

## 9.2 Mappaszerkezet letrehozasa

```bash
sudo mkdir -p /opt/gnosis/authapi/src
sudo mkdir -p /opt/gnosis/authapi/app
sudo chown -R $USER:$USER /opt/gnosis/authapi
```

Javasolt szerkezet:

```text
/opt/gnosis/authapi/
  src/
  app/
  app/keys/
  app/logs/
```

## 9.3 Projekt kibontasa

```bash
cd /opt/gnosis/authapi/src
unzip /path/to/GnosisAuthServer-Rewrite-v4.zip
cd GnosisAuthServer-Rewrite-v4
```

## 9.4 Build es publish

```bash
dotnet restore
dotnet publish -c Release -o /opt/gnosis/authapi/app
```

## 9.5 JWT RSA kulcsok letrehozasa

```bash
mkdir -p /opt/gnosis/authapi/app/keys
openssl genrsa -out /opt/gnosis/authapi/app/keys/auth_private.pem 4096
openssl rsa -in /opt/gnosis/authapi/app/keys/auth_private.pem -pubout -out /opt/gnosis/authapi/app/keys/auth_public.pem
chmod 600 /opt/gnosis/authapi/app/keys/auth_private.pem
chmod 644 /opt/gnosis/authapi/app/keys/auth_public.pem
```

## 9.6 MySQL adatbazis letrehozasa

Lepjunk be MySQL-be:

```bash
sudo mysql
```

Majd:

```sql
CREATE DATABASE gnosis_auth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gnosis_auth'@'127.0.0.1' IDENTIFIED BY 'EROS_DB_JELSZO';
GRANT ALL PRIVILEGES ON gnosis_auth.* TO 'gnosis_auth'@'127.0.0.1';
FLUSH PRIVILEGES;
EXIT;
```

Futtassuk le az alap schema-t:

```bash
mysql -u gnosis_auth -p gnosis_auth < /opt/gnosis/authapi/src/GnosisAuthServer-Rewrite-v4/Sql/bootstrap.sql
```

A MySQL encrypted kapcsolatok tamogatottak, es productionben ajanlott secure transportot hasznalni. ([Microsoft Learn][6])

## 9.7 appsettings.Production.json letrehozasa

Az alkalmazas melle hozzunk letre egy production configot:

`/opt/gnosis/authapi/app/appsettings.Production.json`

Peldatartalom:

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

**Megjegyzes:** elso boothoz ideiglenesen lehet `RequireHttps=false`, hogy latszodjon, tenyleg elindul-e. Miutan az Nginx HTTPS mar kesz, ezt vissza kell allitani `true`-ra.

## 9.8 Kezi inditas teszthez

```bash
cd /opt/gnosis/authapi/app
ASPNETCORE_ENVIRONMENT=Production dotnet GnosisAuthServer.dll
```

Masik terminalban teszt:

```bash
curl http://127.0.0.1:5158/health/live
curl http://127.0.0.1:5158/health/ready
```

Ha ez mukodik, akkor az alkalmazas alapvetoen rendben indul.

---

## 10. systemd szolgaltatas letrehozasa

A Linuxos production futtatasnal a javasolt minta, hogy a .NET app daemonkent fusson, es `systemd` felugyelje. A Microsoft Linux + Nginx utmutatoja is ezt a modellt hasznalja. ([Microsoft Learn][1])

Hozzunk letre kulon rendszerfelhasznalot:

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
ExecStart=/usr/bin/dotnet /opt/gnosis/authapi/app/GnosisAuthServer.dll
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

Logok megtekintese:

```bash
journalctl -u gnosis-authapi -f
```

---

## 11. Nginx reverse proxy beallitasa

A javasolt topologia:

* Kestrel csak `127.0.0.1:5158`
* Nginx fogadja a publikus 80/443 forgalmat
* Nginx tovabbitja a kerest a Kestrel fele

Az ASP.NET Core Linuxos hostolashoz a hivatalos minta Nginx reverse proxy + systemd menedzselt Kestrel. A forwarded headers helyes beallitasa kulcsfontossagu. ([Microsoft Learn][1])

Nginx site file:

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

Ezutan allitsunk be TLS-t a domainra Nginx oldalon. Miutan a HTTPS mukodik, az Auth API configban:

```json
"Security": {
  "RequireHttps": true,
  "KnownProxies": [ "127.0.0.1" ]
}
```

A forwarded headerek feldolgozasat csak ismert proxytol szabad engedni. Ez kulonosen fontos, mert ezek a headerek befolyasolhatjak az alkalmazas altal latott hostot, kliens IP-t es scheme-et. ([Microsoft Learn][7])

---

## 12. CORS

A CORS csak akkor relevans, ha bongeszo alapu frontend is hivja az Auth API-t. Ha csak Unity kliens vagy belso szolgaltatasok beszelnek vele, akkor a CORS inkabb szukitett legyen, ne nyitott.

Javaslat:

* csak konkret origin-ek
* ne `*`
* ne `AllowAnyOrigin`

Pelda:

```json
"Cors": {
  "AllowedOrigins": [
    "https://auth.pelda.hu"
  ]
}
```

---

## 13. Rate limiting

Az Auth API publikus vegpontokat nyit a login es szerverlista muveletekhez, ezert ajanlott rate limitinget hasznalni legalabb a kovetkezokre:

* `POST /api/auth/steam`
* `GET /api/auth/servers`
* `POST /api/internal/official-realms/heartbeat`

Az ASP.NET Core beepitett rate limiting middleware-t ad, es kulon policy rendelheto endpointokhoz vagy route-csoportokhoz. ([Microsoft Learn][4])

---

## 14. Future service kapcsolatok

A projekt kesobbi helye a teljes rendszerben:

* **AuthApi**

  * global auth
  * realm lista
  * global GameData source

* **RealmCoreApi**

  * save/load
  * merged GameData cache
  * zone management
  * node orchestration

* **NodeAgent**

  * zone process inditas
  * zone monitorozas

* **GameServer**

  * runtime gameplay

A `RealmCoreApi` kesobb hitelesitett belso klienskent fogja hivni az Auth API GameData endpointjait. A jelenlegi `ServiceAuth` szekcio ezt a celra kesziti elo.

---

## 15. Hibas inditas leggyakoribb okai

### `JWT private key file was not found`

Ok:

* nincs letrehozva a privat PEM fajl
* rossz path van megadva
* relativ path rossz working directorybol nezve

Megoldas:

* generald le a kulcsokat
* hasznalj abszolut utvonalat

### `ready` health check hibas

Ok:

* rossz DB connection string
* MySQL nem fut
* nincs lefuttatva a `bootstrap.sql`

Megoldas:

* ellenorizd a kapcsolatot
* ellenorizd a tablakat

### Nginx 502 Bad Gateway

Ok:

* az Auth API nem fut
* rossz `proxy_pass`
* rossz port
* systemd service el sem indult

Megoldas:

```bash
sudo systemctl status gnosis-authapi
journalctl -u gnosis-authapi -f
sudo nginx -t
```

### Steam login nem mukodik

Ok:

* hibas `AppId`
* hianyzik `PublisherKey`
* a Steam validacio nincs megfeleloen beallitva

Megoldas:

* ellenorizd a Steam configot
* ideiglenesen fejlesztoi uzemben mock ticket flow-t hasznalj, ha van ra kulon fejlesztoi tamogatas

---

## 16. Javasolt production uzemeltetesi szabalyok

### Kotelezo

* ne maradjon placeholder secret a configban
* a JWT privat kulcs soha ne keruljon repo-ba
* a DB user csak a szukseges jogosultsagokat kapja
* az alkalmazas ne publikus HTTP-n hallgasson
* legyen reverse proxy HTTPS-sel
* legyen rendszeres backup
* a `KnownProxies` legyen megfeleloen beallitva

### Erosen ajanlott

* kulon Linux user az apphoz
* kulon adatbazis user csak ehhez a projekthez
* admin endpoint csak localhost vagy fix admin IP
* naplok rendszeres ellenorzese
* service auth secret-ek rotacioja

### Kesesobbre

* client certificate auth belso service-k kozott
* kulon secret management megoldas
* centralizalt logolas
* kulon monitoring

---

## 17. Gyors osszefoglalo

A `GnosisAuthServer` egy production-kozelibb ASP.NET Core Auth API, amely:

* Steam loginhoz tokeneket ad ki
* kezeli a realm listat
* fogadja a hivatalos realm heartbeatet
* tarolja a globalis canonical GameData-t
* admin vegpontokat ad a realm es GameData kezeleshez

A helyes telepitesi modell Ubuntu VPS-en:

1. .NET telepitese
2. MySQL telepitese es schema letrehozasa
3. projekt publisholasa
4. JWT RSA kulcsok generalasa
5. `appsettings.Production.json` letrehozasa
6. kezi health check
7. systemd service
8. Nginx reverse proxy
9. HTTPS bekapcsolasa
10. `RequireHttps=true`