# wiFRED-dokumentation

Referensdokumentation för den trådlösa körkontrollen 
[wiFRED](https://github.com/newHeiko/wiFred).

## Lokomkopplare och strömhantering

De fyra lokomkopplarna (LOCO1–LOCO4) fungerar som både **strömbrytare** och
lokaktiveringsreglage. Det finns ingen separat av/på-knapp.

### Uppstart (boot)

Att flytta **valfri** lokomkopplare till ON-läge ansluter fysiskt batteriet genom en
hårdvarulåskrets som strömsätter ESP32-S2. Firmwaren kallstartar från början varje
gång — det finns ingen mjukvarukonfigurerad GPIO-uppväckning från dvala.

Vid uppstart gör wiFRED:
1. Initierar GPIO, avvisartimrar och ADC
2. Ansluter till ett konfigurerat WiFi-nätverk
3. Upptäcker eller ansluter till den konfigurerade WiThrottle-servern
4. Förvärvar varje lok vars omkopplare är i ON-läge

### Lokaktivering och -avaktivering

Under körning aktiverar eller avaktiverar växling av individuella lokomkopplare lok
utan omstart:

- **Omkopplare ON**: Loket begärs från WiThrottle-servern,
  funktionslägen skickas och tvångsaktiverade/-avaktiverade funktioner tillämpas.
- **Omkopplare OFF**: Loket frigörs tillbaka till servern.
- **Alla omkopplarändringar** utlöser ett nödstopp för samtliga anslutna lok som en säkerhetsåtgärd.

### Avstängning (nedstängningssekvens)

När **alla fyra** lokomkopplarna ställs till OFF går wiFRED in i en flerstegsavstängning:

| Steg | Tillstånd | Varaktighet | Vad som händer |
|------|-----------|-------------|----------------|
| 1 | `STATE_LOCOS_OFF` | 6 sekunder | Respitperiod — om en omkopplare slås på igen återgår wiFRED till onlineläge utan omstart |
| 2 | `STATE_LOWPOWER_WAITING` | 100 ms | Kopplar från WiThrottle-servern och stänger av WiFi. Röd LED blinkar mycket långsamt (1 ms på / 250 ms cykel) |
| 3 | `STATE_LOWPOWER` | upp till 60 sekunder | Väntar på att hårdvarans keep-alive-krets ska laddas ur, anropar sedan `ESP.deepSleep(0)` (oändlig sömn) |

Om en lokomkopplare slås på igen under `STATE_LOWPOWER` utför firmwaren
en fullständig omstart (`ESP.restart()`).

### Andra orsaker till avstängning

- **Inaktivitet**: Efter **3 timmar** utan hastighetsändringar, funktionstryckningar eller
  lokregistreringar nödstoppar wiFRED alla lok och går in i nedstängningssekvensen.
- **Tomt batteri**: Om batterispänningen sjunker för lågt går wiFRED direkt in i
  `STATE_LOWPOWER_WAITING` (efter att ha frigjort eventuella aktiva lok på ett kontrollerat sätt).

### LED-indikatorer

| Tillstånd | LED-mönster |
|-----------|-------------|
| Uppstart med en lokomkopplare ON | Röd LED: 100 ms på / 200 ms cykel |
| Uppstart med alla omkopplare OFF | Batterinivå visas på alla 3 LED:er (grön/gul/röd) |
| Avstängning | Röd LED: 1 ms på / 250 ms cykel (knappt synlig) |


## Konfiguration

###  Funktionsknappslägen

Var och en av de 17 funktionerna (F0–F16) per lok kan konfigureras individuellt
via wiFRED:s webbgränssnitt. De tillgängliga lägena är:

| Värde | Läge | Beskrivning |
|-------|------|-------------|
| 0 | THROTTLE | Standardbeteende, styrs av körkontrollen |
| 1 | THROTTLE_MOMENTARY | Styrs av körkontrollen, tvingad momentan (aktiv medan knappen hålls) |
| 2 | THROTTLE_LOCKING | Styrs av körkontrollen, tvingad låsande (växlar vid knapptryckning) |
| 3 | THROTTLE_SINGLE | Styrs av körkontrollen bara om detta är det enda loket i multikopplingen |
| 4 | ALWAYS_ON | Funktionen tvångsaktiveras vid lokförvärv |
| 5 | ALWAYS_OFF | Funktionen tvångsavaktiveras vid lokförvärv |
| 6 | IGNORE | Funktionsknappen ignoreras |

Vid loktilldelning skickar wiFRED momentan eller låsande för varje funktion
baserat på konfigurationen enligt ovan. Under körning skickar fysiska knapptryckningar `F1<n>` (tryck)
och `F0<n>` (släpp). Tvångsaktivering/-avaktivering använder `f1<n>` respektive `f0<n>`.

### Webbserver

wiFRED kör en webbserver på **port 80**. Alla konfigurationsändringar görs via
de inbyggda formuläre eller med 
HTTP GET-anrop med frågeparametrar (samma som formulären använder).
Ingen autentisering krävs.

### Åtkomst till webbservern

- **Config AP-läge**: Håll SHIFT-knappen (gul) i 5 sekunder vid uppstart. wiFRED skapar
  en egen WiFi-accesspunkt med namnet `wiFred-configXXXXXX`. En captive DNS-portal omdirigerar
  till webbservern. mDNS-namn: `config.local`.
- **Config Station-läge**: Håll F0-knappen i 5 sekunder medan enheten är ansluten till WiFi.
  Webbservern nås via `<throttleName>.local` med mDNS.

### Ändpunkter

| Slutpunkt | Syfte |
|-----------|-------|
| `GET /index.html?<parametrar>` | Alla konfigurationsskrivningar |
| `GET /api/getConfigXML` | Läs hela konfigurationen som XML |
| `GET /funcmap.html?loco=<1-4>&f0=<val>&...&f16=<val>` | Konfiguration av funktionsmappning |
| `GET /scanWifi.html` | Skanna tillgängliga WiFi-nätverk |
| `GET /restart.html` | Starta om enheten |
| `GET /resetConfig.html?reallyReset=on` | Fabriksåterställning |
| `GET /flashred.html?count=N` | Blinka röd LED för identifiering |
| `GET /update` | OTA-firmwareuppdatering |

### Enhetsupptäckt

- **mDNS**: Annonserar sig som `<throttleName>.local` med HTTP-tjänst på TCP-port 80.
- **UDP-broadcast**: Skickar strängen `"wiFred"` på UDP-port **51289** efter anslutning till WiFi.

### Konfigurationsparametrar

Alla parametrar skickas som frågesträngar till `GET /index.html`.

#### WiThrottle-server

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `loco.serverName` | sträng | WiThrottle-serverns värdnamn eller IP |
| `loco.serverPort` | heltal | WiThrottle-serverns port (standard: 12090) |
| `loco.automatic` | närvaro | Om närvarande aktiveras Zeroconf/Bonjour-automatupptäckt |

#### Lokkonfiguration

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `loco` | heltal (1–4) | Vilken lokplats som ska konfigureras |
| `loco.address` | heltal | DCC-adress (-1 för att inaktivera, 1–10239 lång, 1–127 kort) |
| `loco.longAddress` | närvaro | Om närvarande används lång (utökad) DCC-adress |
| `loco.direction` | heltal | 0 = Normal, 1 = Omvänd, 2 = Ändra inte |
| `loco.mode` | sträng | Hastighetsstegsläge: `"128"`, `"28"`, `"27"`, `"14"`, `"motorola_28"`, `"tmcc_32"`, `"incremental"`, `"1"`, `"2"`, `"4"`, `"8"`, `"16"`, eller `""` (ange ej) |

#### Funktionsmappning

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `loco` | heltal (1–4) | Vilken lokplats |
| `f0` – `f16` | heltal (0–6) | Funktionsläge enligt tabellen ovan |

#### WiFi-nätverkshantering

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `wifiSSID` + `wifiKEY` | sträng | Lägg till ett WiFi-nätverk |
| `remove=<ssid>` | sträng | Ta bort ett WiFi-nätverk |
| `disable=<ssid>` | sträng | Inaktivera ett WiFi-nätverk |
| `enable=<ssid>` | sträng | Aktivera ett WiFi-nätverk |

#### Allmänt

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `throttleName` | sträng | Enhetsnamn (används även för mDNS) |
| `centerSwitch` | heltal | Beteende för mittenläget: -2 = ignorera, -1 = nollhastighet, 0–16 = aktivera funktion |

#### Kalibrering

| Parameter | Typ | Beskrivning |
|-----------|-----|-------------|
| `resetPoti=true` | | Återställ kalibrering av hastighetspotentiometern |
| `newVoltage=<millivolt>` | heltal | Korrigera batterispänningskalibrering |

### XML-konfigurations-API

`GET /api/getConfigXML` returnerar enhetens fullständiga konfiguration som XML, inklusive:

- `<throttleName>` – enhetsnamn
- `<localIP>` – aktuell IP-adress
- `<firmwareRevision>` – firmwareversion
- `<batteryVoltage>` och `<batteryLow>` – batteristatus
- `<WiFi>` – anslutningsstatus, SSID, signalstyrka, MAC-adress
- `<LOCOS>` – alla 4 lokplatser med adress, läge, riktning, lång adress-flagga och F0–F16-mappningar
- `<NETWORKS>` – alla konfigurerade WiFi-nätverk med SSID, nycklar och aktiverad/inaktiverad-status
- `<LOCOSERVER>` – servernamn, port och automatisk upptäckt-flagga
- `<centerSwitch>` – konfiguration av mittenläget


## Kommunikationstiming och hastighetsbegränsning

### Hastighetsbegränsning av hastighetskommandon

Hastighetskommandon hastighetsbegränsas med en spärrtid på **150 ms** (`SPEED_HOLDOFF_PERIOD`).
Ett hastighetskommando skickas bara om:
- Hastighetsvärdet faktiskt har ändrats, OCH
- Minst 150 ms har gått sedan senaste hastighetsuppdatering.

Att skicka ett hastighetskommando nollställer även hjärtslagstimern, vilket förhindrar
redundanta keepalive-meddelanden direkt efter en hastighetsuppdatering.

### Filtrering av hastighetspotentiometern

Potentiometeravläsningen genomgår flera filtreringssteg:

1. **ADC-sampling**: En timer startar var **2:a ms** och växlar mellan hastighets- och batterimätningar.
   **16 sampel** medelvärdesbildas per avläsning, vilket ger ett effektivt medelvärdesfönster på ~64 ms.
2. **Dödbandsspärr**: En ny hastighet vidarebefordras bara om det medelvärdesbildade värdet ändrats med mer
   än 1 steg (av ~126 användbara steg, dvs. ~0,8 % av fullt utslag).
3. **Autokalibreringshysteres**: Kalibreringens min/max-värden kräver **16 på varandra följande**
   avläsningar utanför gränsen innan uppdatering, vilket förhindrar att tillfälliga spikar påverkar kalibreringen.
4. **Gradvis maxreducering**: En separat timer minskar maxkalibreringen med 1 var **10:e sekund**,
   vilket säkerställer att körkontrollen alltid till slut når nollhastighet.

### Knappavvisning (debounce)

Alla knappar avvisas av en timer som kontrollerar var **10:e ms**. En knapp måste avläsas
konsekvent i det nya tillståndet under **4 på varandra följande kontroller** (= **40 ms**) innan
tillståndsändringen accepteras. Detta gäller både tryck- och släpptransitioner.

### Hjärtslag / Keepalive

- Under anslutningsuppbyggnad läser wiFRED serverns annonserade timeout och skickar `*+\n`
  för att aktivera hjärtslagsläge.
- wiFRED använder **40 % av serverns timeout** som sitt eget hjärtslagsintervall
  (den multiplicerar serverns sekundvärde med 400, inte 1000).
- Standard keepalive-timeout (innan serverförhandling): **5000 ms**.
- Hjärtslagsmeddelandet (`*\n`) skickas bara när det inte har förekommit annan trafik
  (hastighet, funktion osv.) under hela keepalive-intervallet.

### TCP-inställningar

- `client.setNoDelay(true)` — inaktiverar Nagles algoritm så att kommandon skickas omedelbart.
- `client.setTimeout(10)` — 10 ms socket-timeout.
- Ingen meddelandeköning eller batchning; varje kommando skickas som ett individuellt `client.print()`-anrop.

### Återanslutning

- Om TCP-anslutningen tappar kopplingen flaggas alla aktiva lok för återförvärv och
  enheten försöker igen efter **60 sekunder**.
- WiFi-anslutningstimeout: **20 sekunder** per individuellt nätverksförsök,
  **60 sekunder** totalt innan enheten ger upp.
- Om automatisk upptäckt är aktiverad använder wiFRED mDNS för att hitta en `_withrottle._tcp`-tjänst.
  Om inget mDNS-resultat hittas faller den tillbaka till gateway-IP + 1 (för LNWI/DCC-EX-enheter).

### Inaktivitetstimeout

Efter **3 timmar** (10 800 000 ms) utan användaraktivitet går wiFRED automatiskt in i
sömnläge.

### Sammanfattning av tidskonstanter

| Konstant | Värde | Syfte |
|----------|-------|-------|
| Hastighetsspärr | 150 ms | Minsta tid mellan hastighetskommandon |
| ADC-samplingsintervall | 2 ms | Hur ofta potentiometern samplas |
| ADC-sampel medelvärde | 16 | Sampel per hastighetsavläsning (~64 ms fönster) |
| Knappavvisningsintervall | 10 ms | Hur ofta knappstatus kontrolleras |
| Knappavvisningsräknare | 4 | Konsekutiva avläsningar som krävs (= 40 ms) |
| Standard keepalive | 5000 ms | Hjärtslagsintervall innan serverförhandling |
| Keepalive-multiplikator | 40 % av serverns timeout | Säkerhetsmarginal för hjärtslag |
| Kalibreringsöverskridningsräknare | 16 | Avläsningar som krävs för att uppdatera kalibrering |
| Kalibreringsmaxreducering | var 10:e s | Gradvis potiMax-reducering |
| Mittenfunktion nödstopp | 500 ms | Tid i mittenläge för att tillåta riktningsbyte |
| Återställ alla funktioner | 5000 ms | Håll ESTOP för att återställa alla funktioner |
| Inaktivitetstimeout | 3 timmar | Automatisk sömn vid inaktivitet |
| Återanslutningsfördröjning | 60 s | Väntetid innan återanslutning efter avbrott |
| Timeout enskilt nätverk | 20 s | Timeout för ett WiFi-anslutningsförsök |
| Timeout totalt nätverk | 60 s | Total WiFi-anslutningstimeout |

### Hårdvaruknappar

- **ESTOP (röd) hållen 5 s vid uppstart**: Fabriksåterställning (raderar all konfiguration)
- **SHIFT (gul) hållen 5 s vid uppstart**: Gå in i Config AP-läge
- **F0 hållen 5 s medan ansluten**: Gå in i Config Station-läge

### Lagring

Konfigurationen lagras på ESP32:s SPIFFS-filsystem som JSON-filer:

| Fil | Innehåll |
|-----|----------|
| `/server.txt` | WiThrottle-servernamn, port, automatisk-flagga |
| `/config.txt` | Körkonrollens namn, mittenomkopplarens inställning |
| `/wifi0.txt`, `/wifi1.txt`, ... | WiFi-nätverk (SSID, PSK, inaktiverad-flagga) |
| `/loco1.txt` – `/loco4.txt` | Lokkonfigurationer med funktionsmappningar |
| `/calibration.txt` | Potentiometerns min/max, batterifaktor |
