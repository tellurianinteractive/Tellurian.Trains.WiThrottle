# wiFRED-protokollreferens

Detta dokument beskriver den del av WiThrottle-protokollet som används av den
trådlösa körkontrollen [wiFRED](https://github.com/newHeiko/wiFred),
i den mån det är relevant för denna serverimplementation.

För enhetsspecifika detaljer (hårdvara, konfiguration, firmware-beteende),
se den [officiella wiFRED-dokumentationen](https://newheiko.github.io/wiFred/documentation/docu_en.html).

## Enhetsupptäckt

### mDNS

wiFRED kan automatiskt upptäcka en WiThrottle-server genom att söka efter
`_withrottle._tcp` via mDNS. Servern annonserar denna tjänst så att
wiFRED-enheter på samma nätverk kan hitta den automatiskt.

### UDP-broadcast

Efter anslutning till WiFi skickar wiFRED strängen `"wiFred"` som broadcast
på UDP-port **51289**. Serverns `WiFredDiscoveryService` lyssnar på denna
port och hämtar vid mottagning enhetens konfiguration via
`GET http://{enhetsIP}/api/getConfigXML` för att upptäcka lokadresskonflikter
mellan anslutna enheter.

## Protokollmeddelanden

Alla multi-throttle-kommandon använder formatet `MT{action}{target}<;>{kommando}`,
där `<;>` är den bokstavliga avgränsaren. Meddelanden avslutas med nyrad.

### Anslutningshandskaning

| Riktning | Meddelande | Syfte |
|----------|------------|-------|
| Server → Klient | `VN2.0` | Protokollversion |
| Server → Klient | `*{sekunder}` | Hjärtslagstimeout |
| Klient → Server | `N{namn}` | Körkontrollens namn |
| Klient → Server | `HU{macHex}` | Hårdvaruidentifierare |
| Klient → Server | `*+` | Anmäl intresse för hjärtslagsövervakning |

### Lokförvärv / Frigöring

| Meddelande | Beskrivning |
|------------|-------------|
| `MT+{lokId}<;>{lokId}` | Förvärva ett lok. Servern svarar med aktuella funktionstillstånd, riktning och hastighetsstegsläge. |
| `MT-{lokId}<;>r` | Frigör ett lok. Servern nödstoppar det. |

Lok-ID använder formatet `L{nummer}` för långa (utökade) DCC-adresser
och `S{nummer}` för korta adresser.

### Hastighet, riktning och nödstopp

| Meddelande | Beskrivning |
|------------|-------------|
| `MTA{target}<;>V{hastighet}` | Sätt hastighet (0–126) |
| `MTA{target}<;>R{0\|1}` | Sätt riktning (0 = bakåt, 1 = framåt) |
| `MTA{target}<;>X` | Nödstopp |

`{target}` är antingen ett specifikt lok-ID eller `*` för att adressera alla
förvärvade lok.

### Funktioner

| Meddelande | Beskrivning |
|------------|-------------|
| `MTA{target}<;>F{0\|1}{n}` | Knapptryckning (1) / släpp (0) för funktion *n* |
| `MTA{target}<;>f{0\|1}{n}` | Tvinga funktion *n* på (1) eller av (0) |
| `MTA{target}<;>m{0\|1}{n}` | Sätt funktionsläge för *n*: 0 = låsande, 1 = momentan |

För låsande funktioner växlar servern funktionstillståndet vid
knapptryckning (`F1`) och ignorerar släpp (`F0`).
För momentana funktioner skickar servern knapptillståndet direkt.

### Hastighetsstegsläge

| Meddelande | Beskrivning |
|------------|-------------|
| `MTA{target}<;>s{läge}` | Deklarera hastighetsstegsläge |

wiFRED kan skicka en hastighetsstegsmodssträng som `128`, `28`, `14`
eller andra. Servern tar emot men agerar för närvarande inte på detta meddelande.

### Sessionskontroll

| Meddelande | Beskrivning |
|------------|-------------|
| `*+` | Aktivera hjärtslagsövervakning |
| `*` | Hjärtslag (keepalive) |
| `Q` | Avsluta — servern nödstoppar och frigör alla lok |

## Hjärtslag

Under anslutningsuppbyggnad annonserar servern sin hjärtslagstimeout
(i sekunder). wiFRED anmäler intresse genom att skicka `*+` och använder
sedan **40 % av serverns annonserade timeout** som sitt eget keepalive-intervall.
Om servern inte tar emot någon trafik inom timeout-perioden nödstoppar den
sessionens lok.

## Hastighetsbegränsning av hastighetskommandon

wiFRED hastighetsbegränsar hastighetskommandon med en spärrtid på **150 ms**
(`SPEED_HOLDOFF_PERIOD`). Servern tillämpar sin egen hastighetsbegränsning
(konfigurerbar via `ThrottlingSettings.SpeedTimeThresholdMs`, standard
150 ms) för att jämna ut skurar från alla klienter.
