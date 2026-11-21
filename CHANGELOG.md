# Changelog – Fairground Live (Twitch & TikTok Crowd)

## v1.4.3 (2026-11-21)

### Neu: Großnachrichten für Subs, Gifts, Raids & Bits (ohne OAuth)
- **Große In-Game-Alert-Box** (`SubAlertBanner`, mittig oben im Spiel, 9 Sekunden sichtbar) für alle Twitch-Events via `USERNOTICE` & `PRIVMSG`:
  - **Neuer Sub & Resub (`sub` / `resub`):** Zeigt Name, Abo-Monate und persönliche Sub-Nachricht groß an.
  - **Sub-Geschenke & Sub-Bomben (`subgift`, `anonsubgift`, `submysterygift`):** Zeigt Schenker, Beschenkten bzw. verschenkte Anzahl groß an.
  - **Geschenk-Verlängerung (`giftpaidupgrade`):** Würdigt Verlängerungen geschenkter Abos.
  - **Raids (`raid`):** Großer `🚨 RAID ALARM!` mit Raider-Name und Zuschauerzahl.
  - **Bits / Cheers (`bits=`):** Großer `💎 CHEER!`-Alert inkl. Bit-Anzahl und Cheer-Nachricht.
- **Test-Button im Dashboard:** Neuer Button **`🎁 Sub-Alert testen`** direkt neben den Durchsagen.

### Neu: Offline-Schalter unter TikTok & neue Rollen-Verteilung
- **Schalter „🙈 Keine Zuschauer anzeigen (Offline-Modus)“** direkt unter dem TikTok-Feld im Dashboard: Blendet mit einem Klick sofort alle 3D-Namen über den Köpfen, Badges und das Top-Banner aus (oder wieder ein).
- **VIPs & Langzeit-Subs sind jetzt Ehrengäste:**
  - **VIPs & Founder** werden nie mehr an Kassen oder Buden eingeteilt, sondern laufen immer als Ehrengäste über die Kirmes.
  - **Subscriber ab 3 Monaten (`SubMonths >= 3`)** bleiben ebenfalls als Gäste auf dem Platz.
  - **Mods** arbeiten weiterhin bevorzugt; **normale Zuschauer & Subs unter 3 Monaten** helfen an freien Kassen, Buden und Fahrgeschäften mit.

### Verbesserungen & Fehlerbehebungen
- **Sofortige Ban- & Timeout-Bereinigung (`CLEARCHAT` / `CLEARMSG`):** Gebannte oder getimeoutete Zuschauer werden sofort vom Peep und aus Mitarbeiter-Posten entfernt; einzeln gelöschte Nachrichten entfernen sofort die Sprechblase über dem Kopf.
- **Kein doppelter Browser-Tab:** Wartet beim Spielstart bis zu 5 Sekunden auf ein laufendes Dashboard-Polling, bevor ein neuer Tab geöffnet wird.

---

## v1.4.2 (2026-11-18)
- Update für Fairground Online v1.1.3 (TikTok-Live-Integration, In-Game-Durchsagen, Custom Crowd).
