using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Steamworks;

namespace FairgroundTwitch
{
    [BepInPlugin("com.blackmautz.fairgroundtwitch", "Fairground Twitch Integration", "1.3.0")]
    public class TwitchPlugin : BasePlugin
    {
        public new static ManualLogSource Log;
        public static TwitchPlugin Instance;

        public static ConfigEntry<string> ChannelName;
        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> ShowNametags;
        public static ConfigEntry<bool> ShowBadges;
        public static ConfigEntry<bool> ShowTopHeader;
        public static ConfigEntry<bool> AssignModsToStaff;
        public static ConfigEntry<bool> AllowAllViewersAsStaff;
        public static ConfigEntry<bool> ExcludeBroadcaster;
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<string> DebugStreamerName;
        public static ConfigEntry<int> NametagFontSize;
        public static ConfigEntry<string> GuestColorHex;
        public static ConfigEntry<string> ModColorHex;
        public static ConfigEntry<string> VipColorHex;
        public static ConfigEntry<string> SubColorHex;
        public static ConfigEntry<bool> EnableJubel;
        public static ConfigEntry<bool> EnableKlatschen;
        public static ConfigEntry<bool> EnableZugabe;
        public static ConfigEntry<float> ActionDuration;
        public static ConfigEntry<float> SpeechBubbleDuration;
        public static ConfigEntry<int> MaxVisibleNametags;
        public static ConfigEntry<float> ViewerInactivityMinutes;
        public static ConfigEntry<bool> AutoOpenDashboard;

        public override void Load()
        {
            Log = base.Log;
            Instance = this;

            ChannelName = Config.Bind("Twitch", "Channel", "nooreax", "Dein Twitch-Kanalname (Kleinbuchstaben, ohne Leerzeichen)");
            ModEnabled = Config.Bind("Twitch", "Enabled", true, "Schaltet die Twitch-Mod ein oder aus.");

            ShowNametags = Config.Bind("Namensschilder", "ShowNametags", true, "Zeige schwebende 3D-Namen ueber den Koepfen der Peeps und Mitarbeiter.");
            ShowBadges = Config.Bind("Namensschilder", "ShowBadges", true, "Zeige echte Twitch-Badge-Icons (Mod-Schwert, VIP-Diamant, Sub-Stern, Twitch-Icon) ueber dem Namen.");
            ShowTopHeader = Config.Bind("Namensschilder", "ShowTopHeader", true, "Zeige oben im Spiel den Twitch-Info-Header mit allen Befehlen (!jubel, !klatschen, !zugabe) und Live-Zuschauerzaehler.");
            AssignModsToStaff = Config.Bind("Namensschilder", "ModsAnFahrgeschaeften", true, "Weise Moderatoren und VIPs bevorzugt den Mitarbeitern (Kasse, Chip-Einsammler, Buden, Rekommandeur) an den Fahrgeschaeften zu!");
            AllowAllViewersAsStaff = Config.Bind("Namensschilder", "AlleZuschauerAnFahrgeschaeften", true, "Erlaubt auch Subscribern und normalen Zuschauern, freie Mitarbeiter-Plaetze an Kassen, Buden und Fahrgeschaeften zu besetzen, wenn keine Mods mehr warten.");
            ExcludeBroadcaster = Config.Bind("Namensschilder", "StreamerAusschliessen", true, "Verhindert, dass du selbst (der Streamer / Kanalbesitzer) als NPC-Peep oder KI-Mitarbeiter herumlaeufst, da du ja selbst spielst.");
            DebugMode = Config.Bind("Debug", "DebugStreamerModus", false, "Wenn aktiv, wirst du selbst (und der Kanal-Streamer) beim Schreiben im Chat als [STREAMER] erkannt, spawnt im Spiel und kann alle Befehle live testen!");
            DebugStreamerName = Config.Bind("Debug", "EigenerTwitchName", "", "Dein eigener Twitch-Benutzername fuer den Debug-Modus (damit du auch in fremden Streams als [STREAMER] erkannt wirst, wenn der Debug-Haken an ist).");
            NametagFontSize = Config.Bind("Namensschilder", "FontSize", 32, "Groesse der Schrift ueber den Peeps.");
            GuestColorHex = Config.Bind("Namensschilder", "GastFarbe", "#FFFFFF", "Standardfarbe fuer normale Gaeste ohne eigene Twitch-Farbe.");
            ModColorHex = Config.Bind("Namensschilder", "ModFarbe", "#00FF7F", "Farbe fuer Twitch-Moderatoren (Hex-Code, z. B. #00FF7F).");
            VipColorHex = Config.Bind("Namensschilder", "VipFarbe", "#FF1493", "Farbe fuer Twitch-VIPs (Hex-Code, z. B. #FF1493).");
            SubColorHex = Config.Bind("Namensschilder", "SubFarbe", "#FFD700", "Farbe fuer Twitch-Subscriber (Hex-Code, z. B. #FFD700).");

            EnableJubel = Config.Bind("ChatBefehle", "EnableJubel", true, "Reagiere auf !jubel, !handsup, !tanz");
            EnableKlatschen = Config.Bind("ChatBefehle", "EnableKlatschen", true, "Reagiere auf !klatschen und !clap");
            EnableZugabe = Config.Bind("ChatBefehle", "EnableZugabe", true, "Reagiere auf !zugabe und !again");
            ActionDuration = Config.Bind("ChatBefehle", "AktionsDauerSekunden", 15f, "Wie lange die Aktion des einzelnen Zuschauers nach seinem Chat-Befehl aktiv bleibt.");
            SpeechBubbleDuration = Config.Bind("Namensschilder", "SprechblasenDauerSekunden", 30f, "Wie lange normale Chat-Nachrichten als Sprechblase ueber dem Kopf der Zuschauer stehen bleiben (Standard: 30 Sekunden).");
            MaxVisibleNametags = Config.Bind("Namensschilder", "MaxVisibleNametags", 200, "Maximale Anzahl aktiver Zuschauer-Peeps gleichzeitig (Standard: 200). Da entfernte Schilder automatisch ausgeblendet werden, bleiben FPS absolut stabil!");
            ViewerInactivityMinutes = Config.Bind("Namensschilder", "ViewerInactivityMinutes", 10f, "Inaktivitaets-Timeout in Minuten: Wer x Minuten nichts im Chat schreibt, gibt seinen Peep/Namensschild fuer neue aktive Chatter frei (0 = Deaktiviert).");
            AutoOpenDashboard = Config.Bind("Twitch", "AutoOpenDashboard", true, "Oeffnet beim Spielstart automatisch das Twitch Live-Dashboard im Standard-Webbrowser.");

            WriteConfigInfoTxt();
            WriteLiveDashboardHtml();

            ClassInjector.RegisterTypeInIl2Cpp<TwitchRunner>();

            GameObject go = new GameObject("FairgroundTwitchRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<TwitchRunner>();

            Log.LogInfo("[FairgroundTwitch] v1.3.0 erfolgreich geladen! (Einzel-User-Aktionen + Mitarbeiter-System + Badges aktiv)");
        }

        private static void WriteConfigInfoTxt()
        {
            try
            {
                string infoPath = Path.Combine(Paths.ConfigPath, "FairgroundTwitch_ANLEITUNG_UND_BEFEHLE.txt");
                string content =
@"================================================================================
   FAIRGROUND ONLINE - TWITCH LIVE-KIRMES INTEGRATION (v1.3.0)
   Alle Informationen, Chat-Befehle, Funktionen & Config-Erklärungen
================================================================================

1. WIE FUNKTIONIERT DIE MOD?
----------------------------
Sobald du das Spiel startest, verbindet sich die Mod im Hintergrund (ohne Login/Passwort)
direkt mit dem in der Config eingestellten Twitch-Chat (Standard: com.blackmautz.fairgroundtwitch.cfg).
Jeder Zuschauer, der eine Nachricht in deinen Twitch-Chat schreibt, wird live auf deiner
Kirmes zum Leben erweckt – inklusive seiner echten Twitch-Namensfarbe, seinem Status
(Moderator, VIP, Founder, Subscriber mit exakter Monatsanzahl, Verifizierter Partner)
und den originalen Twitch-Badge-Icons über dem Kopf!

Du selbst (als Streamer / Kanalbesitzer) wirst standardmäßig ausgeschlossen, damit du
dir nicht selbst als KI-Peep begegnest.


2. ALLE CHAT-BEFEHLE FÜR DEINE ZUSCHAUER IM STREAM
--------------------------------------------------
WICHTIG: Jeder Befehl steuert immer NUR den einzelnen Zuschauer, der ihn im Chat eingibt!

  A) Animationen & Stimmung (im Fahrgeschäft & auf dem Platz):
  ------------------------------------------------------------
  • !jubel      (oder !handsup, !tanz)  -> Der Zuschauer reißt die Arme hoch / tanzt!
  • !klatschen  (oder !clap, !applaus)  -> Der Zuschauer applaudiert und klatscht!
  • !zugabe     (oder !again, !schrei)  -> Der Zuschauer schreit laut und fordert Zugabe!

  B) Wunsch-Jobs & Gezieltes Mitfahren auf der Kirmes:
  ----------------------------------------------------
  • !mitfahren [Name]                   -> Fährt direkt beim Wunsch-Fahrgeschäft mit!
                                           Beispiele:
                                           !mitfahren mega dance
                                           !mitfahren soundexpress
                                           !mitfahren teufelstanz
                                           !mitfahren riesenrad
                                           (Ohne Namen hinter !mitfahren / !gast steigt der
                                           Zuschauer direkt bei dem Fahrgeschäft ein, das
                                           du gerade selbst steuerst, oder läuft frei herum!)
  • !kasse                              -> Wechselt gezielt als Kassierer in ein Kassenhäuschen!
  • !chip       (oder !sammler)         -> Wechselt gezielt als Fahrchip-Sammler auf die Platte!
  • !bude       (oder !stand)           -> Wechselt gezielt als Verkäufer in eine Spiel-/Essensbude!
  • !reko       (oder !operator)        -> Wechselt an ein freies Steuerpult eines Fahrgeschäfts!
  • !arbeit     (oder !job)             -> Nimmt den nächsten freien Mitarbeiter-Platz an!

  C) Live-Sprechblasen (Normale Chat-Nachrichten):
  ------------------------------------------------
  • Schreibt ein Zuschauer einen normalen Satz in den Chat (ohne ""!""), erscheint seine
    Nachricht für 30 Sekunden (im Dashboard einstellbar!) als Sprechblase direkt über seinem Kopf!


3. AUTOMATISCHE MITARBEITER-REIHENFOLGE AN DEN GESCHÄFTEN
---------------------------------------------------------
Moderatoren, VIPs und Founder haben immer 100 % Vorrang an den Arbeitsplätzen.
Wenn keine Mods mehr warten, dürfen auch Subscriber und normale Zuschauer freie Geschäfte
besetzen, damit nach und nach die komplette Kirmes belebt wird.

Die automatische Vergabe erfolgt in dieser Reihenfolge:
  1. [KASSE • ...]        -> Kassenhäuschen an den Fahrgeschäften
  2. [CHIP-SAMMLER • ...] -> Fahrchip-Einsammler auf der Platte
  3. [STAND • ...]        -> Spielbuden, Losbuden & Essensstände
  4. [REKOMMANDEUR • ...] -> Steuerpult / Fahrerhäuschen (Wird als ALLERLETZTES besetzt,
                             damit die Plätze für dich frei bleiben! Übernimmst du ein
                             Fahrgeschäft selbst, wechselt der Zuschauer automatisch.)


4. LIVE-EINBLENDUNGEN UNTER DEM TOP-HEADER IM SPIEL
---------------------------------------------------
Direkt unter der oberen Leiste (""Zuschauer auf dem Platz"") blendet sich ein Live-Feed ein:
  • ► NEU AUF DEM PLATZ: Wenn ein neuer Zuschauer auf der Kirmes ankommt.
  • ★ NEU IM TEAM:       Wenn jemand einen Job an Kasse, Bude oder Fahrgeschäft antritt.
  • 🎢 BEI DIR EINGESTIEGEN: Wenn sich ein Twitch-Zuschauer direkt in das Fahrgeschäft setzt,
                             das DU gerade selbst steuerst (perfekt zum Begrüßen übers Mikro!).
  • 🎠 FAHRT GESTARTET:  Wenn ein Zuschauer in ein anderes Karussell auf dem Platz einsteigt.


5. ERKLÄRUNG DER CONFIG-DATEI (com.blackmautz.fairgroundtwitch.cfg)
-------------------------------------------------------------------
Die eigentlichen Einstellungen findest du im selben Ordner (BepInEx\config) in der Datei:
""com.blackmautz.fairgroundtwitch.cfg""

  [Twitch]
  • Channel                        = Dein Twitch-Kanalname (in Kleinbuchstaben, z.B. dhalucard)
  • Enabled                        = true / false (Schaltet die gesamte Mod ein oder aus)

  [Namensschilder]
  • ShowNametags                   = true / false (3D-Namen über den Köpfen anzeigen)
  • ShowBadges                     = true / false (Echte Twitch-Icons über den Namen anzeigen)
  • ShowTopHeader                  = true / false (Obere Stream-Infoleiste im Spiel anzeigen)
  • ModsAnFahrgeschaeften          = true / false (Mitarbeiter-System an Geschäften aktivieren)
  • AlleZuschauerAnFahrgeschaeften = true / false (Auch Subs/Viewer an freien Kassen/Buden erlauben)
  • StreamerAusschliessen          = true / false (Dich selbst nicht als NPC-Peep spawnen)
  • FontSize                       = Schriftgröße der Namen (Standard: 32)
  • GastFarbe / ModFarbe / VipFarbe / SubFarbe = Fallback-Farben als Hex-Code (#RRGGBB)

  [ChatBefehle]
  • EnableJubel / EnableKlatschen / EnableZugabe = Einzelne Befehle ein-/ausschalten
  • AktionsDauerSekunden           = Wie lange ein Zuschauer nach !jubel/!klatschen animiert bleibt (Standard: 15)
================================================================================
";
                File.WriteAllText(infoPath, content, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Twitch] Konnte Info-TXT nicht erstellen: {ex.Message}");
            }
        }

        private static void WriteLiveDashboardHtml()
        {
            try
            {
                string htmlPath = Path.Combine(Paths.ConfigPath, "FairgroundTwitch_Dashboard.html");
                string html =
@"<!DOCTYPE html>
<html lang=""de"">
<head>
<meta charset=""UTF-8"">
<title>Fairground Online • Twitch Live-Kirmes Control Center</title>
<style>
  :root {
    --bg: #0e0e14;
    --panel: #161621;
    --panel2: #1f1f2e;
    --twitch: #9146ff;
    --twitch-glow: #a970ff;
    --green: #00e676;
    --cyan: #00e5ff;
    --gold: #ffb400;
    --pink: #ff4081;
    --text: #efeff1;
    --muted: #adadb8;
    --border: #2c2c3f;
  }
  * { box-sizing: border-box; font-family: 'Segoe UI', Inter, Roboto, sans-serif; }
  body { margin: 0; padding: 24px; background: var(--bg); color: var(--text); }
  .container { max-width: 1180px; margin: 0 auto; }
  .header {
    background: linear-gradient(135deg, #1b1233 0%, #141422 100%);
    border: 1px solid var(--twitch);
    border-bottom: 4px solid var(--twitch);
    border-radius: 14px;
    padding: 20px 26px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    box-shadow: 0 8px 30px rgba(145,70,255,0.18);
    margin-bottom: 22px;
  }
  .header h1 { margin: 0; font-size: 24px; display: flex; align-items: center; gap: 12px; }
  .badge-status {
    padding: 7px 14px;
    border-radius: 999px;
    font-weight: 700;
    font-size: 13px;
    display: inline-flex;
    align-items: center;
    gap: 8px;
  }
  .status-online { background: rgba(0,230,118,0.15); color: var(--green); border: 1px solid var(--green); }
  .status-offline { background: rgba(255,64,129,0.15); color: var(--pink); border: 1px solid var(--pink); }
  .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 22px; }
  @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
  .card {
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 12px;
    padding: 20px;
    box-shadow: 0 4px 16px rgba(0,0,0,0.25);
  }
  .card h2 {
    margin: 0 0 14px 0;
    font-size: 17px;
    color: var(--twitch-glow);
    border-bottom: 1px solid var(--border);
    padding-bottom: 10px;
    display: flex;
    align-items: center;
    justify-content: space-between;
  }
  .stat-row { display: flex; gap: 12px; margin-bottom: 16px; }
  .stat-box {
    flex: 1;
    background: var(--panel2);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 12px;
    text-align: center;
  }
  .stat-box .num { font-size: 26px; font-weight: 800; color: var(--green); }
  .stat-box .lbl { font-size: 12px; color: var(--muted); margin-top: 4px; }
  .input-row { display: flex; gap: 10px; margin-bottom: 14px; }
  input[type=text] {
    flex: 1;
    background: var(--panel2);
    border: 1px solid var(--border);
    color: white;
    padding: 10px 14px;
    border-radius: 8px;
    font-size: 15px;
    outline: none;
  }
  input[type=text]:focus { border-color: var(--twitch); }
  button {
    background: var(--twitch);
    color: white;
    border: none;
    padding: 10px 16px;
    border-radius: 8px;
    font-weight: 700;
    cursor: pointer;
    transition: 0.15s;
    font-size: 13px;
  }
  button:hover { background: var(--twitch-glow); transform: translateY(-1px); }
  button.btn-sm { padding: 5px 10px; font-size: 12px; border-radius: 6px; }
  button.btn-cyan { background: #008ba3; }
  button.btn-cyan:hover { background: #00acc1; }
  button.btn-green { background: #008e46; }
  button.btn-green:hover { background: #00b85c; }
  button.btn-gold { background: #c68400; }
  button.btn-gold:hover { background: #ffab00; color: #000; }
  .toggle-list { display: flex; flex-direction: column; gap: 10px; }
  .toggle-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: var(--panel2);
    padding: 10px 14px;
    border-radius: 8px;
    border: 1px solid var(--border);
  }
  .toggle-item span { font-size: 14px; font-weight: 600; }
  .toggle-item small { display: block; color: var(--muted); font-size: 11px; font-weight: 400; margin-top: 2px; }
  input[type=checkbox] { width: 20px; height: 20px; accent-color: var(--twitch); cursor: pointer; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td { text-align: left; padding: 9px 10px; border-bottom: 1px solid var(--border); }
  th { color: var(--muted); font-weight: 600; font-size: 12px; text-transform: uppercase; }
  .tag {
    display: inline-block;
    padding: 2px 7px;
    border-radius: 5px;
    font-size: 11px;
    font-weight: 700;
    margin-right: 4px;
  }
  .tag-mod { background: rgba(0,230,118,0.18); color: var(--green); }
  .tag-vip { background: rgba(255,20,147,0.18); color: #ff5cb8; }
  .tag-sub { background: rgba(255,180,0,0.18); color: var(--gold); }
  .tag-guest { background: rgba(169,112,255,0.18); color: var(--twitch-glow); }
  .tag-job { background: rgba(0,229,255,0.15); color: var(--cyan); border: 1px solid rgba(0,229,255,0.3); }
  .cmd-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 10px; }
  .cmd-box {
    background: var(--panel2);
    border: 1px solid var(--border);
    border-left: 3px solid var(--twitch);
    border-radius: 8px;
    padding: 10px 12px;
  }
  .cmd-box code { color: var(--gold); font-weight: 800; font-size: 14px; }
  .cmd-box p { margin: 4px 0 0 0; font-size: 12px; color: var(--muted); }
  .toast {
    position: fixed; bottom: 20px; right: 20px;
    background: var(--green); color: #000; font-weight: 800;
    padding: 12px 20px; border-radius: 8px; display: none;
    box-shadow: 0 6px 20px rgba(0,0,0,0.4);
  }
</style>
</head>
<body>
<div class=""container"">
  <div class=""header"">
    <div>
      <h1>🎡 Fairground Online • Twitch Live-Kirmes Control Center</h1>
      <div style=""color:var(--muted); font-size:13px; margin-top:4px;"">
        Steuere deine Kirmes-Mod live im Browser ohne Spiel-Neustart! Alle Änderungen werden sofort im Spiel & in der Config gespeichert.
      </div>
    </div>
    <div id=""connBadge"" class=""badge-status status-offline"">● Spiel nicht verbunden (Starte Fairground Online)</div>
  </div>

  <div class=""grid"">
    <!-- LINKE SPALTE: LIVE-STEUERUNG -->
    <div class=""card"">
      <h2>⚡ Live-Kanal & Spiel-Statistik</h2>
      <div class=""stat-row"">
        <div class=""stat-box"">
          <div id=""statTotal"" class=""num"">0</div>
          <div class=""lbl"">Zuschauer auf der Kirmes</div>
        </div>
        <div class=""stat-box"">
          <div id=""statPeeps"" class=""num"" style=""color:var(--gold)"">0</div>
          <div class=""lbl"">Besucher-Peeps (Fahrgeschäfte)</div>
        </div>
        <div class=""stat-box"">
          <div id=""statStaff"" class=""num"" style=""color:var(--cyan)"">0</div>
          <div class=""lbl"">Mitarbeiter (Kasse / Bude / Chip)</div>
        </div>
      </div>

      <label style=""font-size:12px; color:var(--muted); font-weight:700; display:block; margin-bottom:6px;"">AKTIVER TWITCH-KANAL (LIVE WECHSELN OHNE NEUSTART):</label>
      <div class=""input-row"">
        <input type=""text"" id=""channelInput"" placeholder=""z.B. dhalucard oder nooreax"" />
        <button onclick=""changeChannel()"">🔄 Kanal live übernehmen</button>
      </div>

      <h2 style=""margin-top:20px;"">🎛️ Live-Schalter (Sofort im Spiel aktiv)</h2>
      <div class=""toggle-list"">
        <div class=""toggle-item"">
          <div>
            <span>Oberes Stream-Banner (Top-Header) anzeigen</span>
            <small>Zeigt oben im Spiel Kanalname, Zuschauerzähler, Befehle & Live-Join-Feed</small>
          </div>
          <input type=""checkbox"" id=""chkTopHeader"" onchange=""toggleSetting('ShowTopHeader', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>3D-Namensschilder über Köpfen anzeigen</span>
            <small>Zeigt Namen, Rollen, Sub-Monate und Sprechblasen über Peeps & Mitarbeitern</small>
          </div>
          <input type=""checkbox"" id=""chkNametags"" onchange=""toggleSetting('ShowNametags', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>Echte Twitch-Badge-Icons über dem Namen</span>
            <small>Zeigt Mod-Schwert, VIP-Diamant, Sub-Stern und Twitch-Logo als 3D-Icon</small>
          </div>
          <input type=""checkbox"" id=""chkBadges"" onchange=""toggleSetting('ShowBadges', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>Mitarbeiter-System an Geschäften & Buden</span>
            <small>Teilt Zuschauer an Kassen, Chip-Sammlern, Spielbuden & Steuerpulten ein</small>
          </div>
          <input type=""checkbox"" id=""chkModsStaff"" onchange=""toggleSetting('AssignModsToStaff', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>Auch normale Zuschauer & Subs an Geschäften arbeiten lassen</span>
            <small>Mods haben 100 % Vorrang, aber freie Kassen/Buden werden auch von Viewern besetzt</small>
          </div>
          <input type=""checkbox"" id=""chkAllStaff"" onchange=""toggleSetting('AllowAllViewersAsStaff', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>Streamer selbst als NPC ausschließen</span>
            <small>Verhindert, dass du dir selbst als KI-Peep auf dem Platz begegnest</small>
          </div>
          <input type=""checkbox"" id=""chkExcludeSelf"" onchange=""toggleSetting('ExcludeBroadcaster', this.checked)"" checked />
        </div>
        <div class=""toggle-item"">
          <div>
            <span>💬 Sprechblasen-Dauer über den Köpfen</span>
            <small>Wie lange Chat-Nachrichten der Zuschauer über ihrem Kopf sichtbar bleiben</small>
          </div>
          <select id=""selBubbleDuration"" onchange=""setNumericSetting('SpeechBubbleDuration', this.value)"" style=""background:#111118; border:1px solid var(--border); color:#fff; padding:6px 10px; border-radius:6px; font-weight:700; font-size:12px;"">
            <option value=""15"">15 Sek.</option>
            <option value=""30"" selected>30 Sek. (Standard)</option>
            <option value=""45"">45 Sek.</option>
            <option value=""60"">60 Sek.</option>
            <option value=""120"">2 Min.</option>
          </select>
        </div>
        <div class=""toggle-item"">
          <div>
            <span>🏷️ Max. sichtbare 3D-Namensschilder (FPS-Bremsschutz)</span>
            <small>Wie viele Peeps gleichzeitig Namen tragen (schuetzt FPS bei 500-2000 Peeps)</small>
          </div>
          <select id=""selMaxNametags"" onchange=""setNumericSetting('MaxVisibleNametags', this.value)"" style=""background:#111118; border:1px solid var(--green); color:var(--green); padding:6px 10px; border-radius:6px; font-weight:700; font-size:12px;"">
            <option value=""50"">50 Zuschauer-Peeps (Sehr schwache PCs)</option>
            <option value=""100"">100 Zuschauer-Peeps</option>
            <option value=""200"" selected>200 Zuschauer-Peeps (Standard & Empfohlen)</option>
            <option value=""500"">500 Zuschauer-Peeps</option>
            <option value=""1000"">1.000 Zuschauer-Peeps</option>
            <option value=""1500"">1.500 Zuschauer-Peeps</option>
            <option value=""2000"">2.000 Zuschauer-Peeps (Voller Park / 100% aller Peeps)</option>
          </select>
        </div>
        <div class=""toggle-item"">
          <div>
            <span>⏳ Inaktivitaets-Timeout (Namen nach X Min. freigeben)</span>
            <small>Wer X Min. nicht im Chat schreibt, gibt seinen Peep fuer neue aktive Chatter frei</small>
          </div>
          <select id=""selInactivityMin"" onchange=""setNumericSetting('ViewerInactivityMinutes', this.value)"" style=""background:#111118; border:1px solid var(--twitch); color:var(--gold); padding:6px 10px; border-radius:6px; font-weight:700; font-size:12px;"">
            <option value=""3"">3 Minuten (Schneller Durchlauf)</option>
            <option value=""5"">5 Minuten</option>
            <option value=""10"" selected>10 Minuten (Standard)</option>
            <option value=""15"">15 Minuten</option>
            <option value=""30"">30 Minuten</option>
            <option value=""0"">Nie freigeben (Deaktiviert)</option>
          </select>
        </div>
        <div class=""toggle-item"" style=""border-color:#ff2a2a; background:rgba(255,42,42,0.08);"">
          <div>
            <span style=""color:#ff6b6b;"">🧪 Debug-Modus: Mich selbst als [STREAMER] im Spiel spawnen & testen</span>
            <small>Wenn der Haken an ist, wirst du beim Schreiben im Chat als <b>[STREAMER]</b> erkannt und spawnst sofort!</small>
          </div>
          <input type=""checkbox"" id=""chkDebugMode"" onchange=""toggleSetting('DebugMode', this.checked)"" />
        </div>
      </div>

      <!-- NEU: VOD-CHAT REPLAY DEMO (FREIZEITGESTOERT) -->
      <div id=""vodDemoBox"" style=""margin-top:14px; background:linear-gradient(135deg, rgba(145,70,255,0.18) 0%, rgba(20,20,35,0.9) 100%); border:1px solid var(--twitch); border-radius:12px; padding:14px; box-shadow:0 4px 18px rgba(145,70,255,0.25);"">
        <div style=""display:flex; align-items:center; justify-content:space-between; margin-bottom:8px;"">
          <div style=""font-size:13px; font-weight:800; color:var(--twitch-glow); display:flex; align-items:center; gap:8px;"">
            <span>🎬 TWITCH VOD-CHAT DEMO (FREIZEITGESTOERT)</span>
            <span id=""demoBadge"" class=""tag"" style=""background:rgba(255,64,129,0.2); color:var(--pink);"">INAKTIV</span>
          </div>
          <a href=""https://www.twitch.tv/videos/2919532326"" target=""_blank"" style=""color:var(--muted); font-size:11px; text-decoration:none;"">🔗 VOD #2919532326 öffnen ↗</a>
        </div>
        <div style=""font-size:12px; color:var(--text); margin-bottom:10px;"">
          Spielt den echten Chat aus dem Kirmes-Stream von <b>Freizeitgestoert</b> mit <b>885 echten Chat-Nachrichten, Sprechblasen & Kassen-Zuweisungen</b> ab!
        </div>
        <div style=""display:flex; gap:10px; align-items:center; flex-wrap:wrap;"">
          <button id=""btnStartDemo"" class=""btn-green"" onclick=""startVodDemo()"" style=""display:flex; align-items:center; gap:6px; font-size:13px; padding:8px 16px;"">
            <span>▶️ Demo-Chat abspielen</span>
          </button>
          <button id=""btnStopDemo"" class=""btn-sm"" style=""background:#b91c1c; display:none;"" onclick=""stopVodDemo()"">
            <span>⏹️ Demo stoppen</span>
          </button>
          <span id=""demoProgressText"" style=""font-size:12px; color:var(--gold); font-weight:700;"">Bereit zum Starten</span>
        </div>
      </div>

      <div id=""debugPanelBox"" style=""margin-top:12px; background:rgba(255,42,42,0.08); border:1px solid #ff2a2a; border-radius:10px; padding:12px;"">
        <div style=""font-size:12px; font-weight:800; color:#ff6b6b; margin-bottom:8px;"">🧪 DEBUG- & STREAMER-TESTBEREICH (OHNE TWITCH-STREAM LIVE SEIN ZU MÜSSEN)</div>
        <div class=""input-row"" style=""margin-bottom:8px;"">
          <input type=""text"" id=""debugStreamerInput"" placeholder=""Dein eigener Twitch-Name (z.B. dein_nick)"" />
          <button class=""btn-sm"" style=""background:#e11d48;"" onclick=""saveDebugName()"">💾 Name speichern</button>
        </div>
        <div class=""input-row"" style=""margin-bottom:0;"">
          <input type=""text"" id=""debugSimMsgInput"" placeholder=""Test-Nachricht oder Befehl (z.B. Hallo Kirmes! oder !jubel oder !kasse)"" />
          <button class=""btn-sm btn-gold"" onclick=""simulateDebugChat()"">🚀 Direkt als [STREAMER] im Spiel spawnen / senden!</button>
        </div>
      </div>
    </div>

    <!-- RECHTE SPALTE: BEFEHLE & BOT-VORLAGE -->
    <div class=""card"">
      <h2>
        <span>💬 Alle Chat-Befehle für deine Zuschauer</span>
        <button class=""btn-sm btn-green"" onclick=""copyBotText()"">📋 Chat-Erklärung kopieren</button>
      </h2>
      <div class=""cmd-grid"">
        <div class=""cmd-box"">
          <code>!jubel</code> / <code>!tanz</code>
          <p>Dein eigener Charakter reißt im Fahrgeschäft die Arme hoch oder tanzt auf dem Platz!</p>
        </div>
        <div class=""cmd-box"">
          <code>!klatschen</code> / <code>!clap</code>
          <p>Dein eigener Charakter klatscht begeistert Beifall!</p>
        </div>
        <div class=""cmd-box"">
          <code>!zugabe</code> / <code>!schrei</code>
          <p>Dein Charakter jubelt lautstark mit Schrei-Soundeffekt und fordert Zugabe!</p>
        </div>
        <div class=""cmd-box"">
          <code>!mitfahren [Name]</code>
          <p>Fährt direkt beim Streamer oder beim Wunsch-Geschäft mit (z.B. <b>!mitfahren mega dance</b>)!</p>
        </div>
        <div class=""cmd-box"">
          <code>!kasse</code>
          <p>Wechselt gezielt als Kassierer in ein Kassenhäuschen (z.B. [KASSE • SOUNDEXPRESS]).</p>
        </div>
        <div class=""cmd-box"">
          <code>!chip</code> / <code>!sammler</code>
          <p>Wechselt gezielt als Fahrchip-Sammler auf die Platte vor dem Fahrgeschäft.</p>
        </div>
        <div class=""cmd-box"">
          <code>!bude</code> / <code>!stand</code>
          <p>Wechselt gezielt als Verkäufer in eine Spielbude oder einen Essensstand.</p>
        </div>
        <div class=""cmd-box"">
          <code>!reko</code> / <code>!arbeit</code>
          <p>Bewirbt sich für ein freies Steuerpult (!reko) oder den nächsten freien Job (!arbeit).</p>
        </div>
      </div>

      <h2 style=""margin-top:18px;"">📢 Fertige Vorlagen für deinen Twitch-Chat & Bot (1-Klick Kopieren)</h2>
      <div style=""background:var(--panel2); border:1px solid var(--border); border-radius:8px; padding:10px 12px; margin-bottom:12px;"">
        <div style=""font-size:11px; color:var(--muted); margin-bottom:6px; font-weight:700;"">VORSCHAU (ZUM EINFÜGEN IN DEINEN TWITCH-CHAT ODER NIGHTBOT):</div>
        <input type=""text"" id=""botTemplateBox"" readonly value=""🎡 LIVE AUF DER KIRMES! Schreibt in den Chat, um im Spiel zu spawnen! Befehle: !jubel | !klatschen | !zugabe | !mitfahren [Name] (z.B. !mitfahren mega dance) | !kasse (Kassierer) | !chip (Fahrchips sammeln) | !bude (Buden-Verkäufer)!"" style=""width:100%; font-size:12px; padding:8px; margin-bottom:8px; background:#101018; color:var(--gold);"" />
        <div style=""display:flex; gap:8px; flex-wrap:wrap; align-items:center;"">
          <button class=""btn-sm btn-green"" onclick=""copyBotText()"">📋 Für Twitch-Chat kopieren</button>
          <button class=""btn-sm btn-cyan"" onclick=""copyNightbotCmd()"">🤖 Als !addcom für Nightbot kopieren</button>
          <button class=""btn-sm"" onclick=""sendInGameBanner()"">📢 Als eigenes Banner im Spiel einblenden!</button>
        </div>
      </div>

      <h2 style=""margin-top:14px;"">📢 Eigene Durchsage im Spiel-Banner (Eigene 3. Zeile unter Neu-Anzeigen!)</h2>
      <div class=""input-row"" style=""margin-bottom:8px;"">
        <input type=""text"" id=""customBannerInput"" placeholder=""z.B. Steigt alle in den SoundExpress ein! Nächste Fahrt startet!"" />
        <select id=""announceDuration"" style=""background:#111118; border:1px solid var(--border); color:#fff; padding:9px 10px; border-radius:8px; font-weight:700; font-size:12px;"">
          <option value=""60"">⏱️ 60 Sek.</option>
          <option value=""120"">⏱️ 2 Min.</option>
          <option value=""300"">⏱️ 5 Min.</option>
          <option value=""3600"">📌 Dauerhaft (1 Std.)</option>
        </select>
        <button class=""btn-gold"" onclick=""sendCustomBanner()"">📢 Im Spiel anzeigen</button>
        <button class=""btn-sm"" style=""background:#b91c1c; padding:9px 12px;"" onclick=""clearBanner()"">❌ Ausblenden</button>
      </div>
      <div style=""font-size:11px; color:var(--muted);"">💡 Deine Durchsage hat jetzt eine <b>eigene goldene Leiste unterhalb</b> der Join-Meldungen – wird also selbst bei 1.000 Zuschauern niemals überschrieben!</div>
    </div>
  </div>

  <!-- UNTERE TABELLE: LIVE ZUSCHAUER AUF DER KIRMES & DIREKT-ZUWEISUNG -->
  <div class=""card"">
    <h2>
      <span>👥 Live-Zuschauer auf der Kirmes (Direkt im Browser umteilen!)</span>
      <span id=""sceneLabel"" style=""font-size:12px; color:var(--muted); font-weight:400;"">Szene: -</span>
    </h2>
    <div style=""display:flex; gap:10px; flex-wrap:wrap; align-items:center; margin-bottom:12px; background:var(--panel2); padding:10px 12px; border-radius:10px; border:1px solid var(--border);"">
      <input type=""text"" id=""viewerSearchInput"" oninput=""applyFilterAndRender()"" placeholder=""🔍 Zuschauer oder Geschäft suchen (z.B. SteelBuggi, SoundExpress, Kasse)..."" style=""flex:1; min-width:240px; padding:8px 12px; font-size:13px;"" />
      <div style=""display:flex; gap:6px; flex-wrap:wrap;"">
        <button id=""flt-all"" class=""btn-sm"" style=""background:var(--twitch);"" onclick=""setViewerFilter('all')"">Alle</button>
        <button id=""flt-staff"" class=""btn-sm"" style=""background:#272736;"" onclick=""setViewerFilter('staff')"">🛠️ Nur Mitarbeiter</button>
        <button id=""flt-visitor"" class=""btn-sm"" style=""background:#272736;"" onclick=""setViewerFilter('visitor')"">🚶 Nur Besucher</button>
        <button id=""flt-vipmod"" class=""btn-sm"" style=""background:#272736;"" onclick=""setViewerFilter('vipmod')"">⭐ Mods / VIPs / Subs</button>
      </div>
      <select id=""viewerSortSelect"" onchange=""applyFilterAndRender()"" style=""background:#111118; border:1px solid var(--border); color:#fff; padding:7px 10px; border-radius:8px; font-weight:700; font-size:12px;"">
        <option value=""az"">🔤 Sortierung: Name (A–Z)</option>
        <option value=""role"">👑 Sortierung: Rolle (Mods/VIPs zuerst)</option>
        <option value=""job"">🛠️ Sortierung: Mitarbeiter zuerst</option>
      </select>
    </div>
    <div style=""overflow-x:auto; max-height:460px; overflow-y:auto;"">
      <table>
        <thead>
          <tr>
            <th>Zuschauer</th>
            <th>Twitch-Status</th>
            <th>Aktuelle Position auf der Kirmes</th>
            <th>Live im Spiel umteilen (Ohne Neustart)</th>
          </tr>
        </thead>
        <tbody id=""viewerTableBody"">
          <tr><td colspan=""4"" style=""color:var(--muted); text-align:center; padding:20px;"">Warte auf Verbindung zum laufenden Spiel...</td></tr>
        </tbody>
      </table>
    </div>
  </div>
</div>

<div id=""toast"" class=""toast"">Gespeichert!</div>

<script>
const API = 'http://127.0.0.1:48888';
let isEditingChannel = false;
let isEditingDebugName = false;
let lastViewersList = [];
let lastTrackedUser = '';
let currentViewerFilter = 'all';

document.getElementById('channelInput').addEventListener('focus', () => isEditingChannel = true);
document.getElementById('channelInput').addEventListener('blur', () => isEditingChannel = false);
document.getElementById('debugStreamerInput').addEventListener('focus', () => isEditingDebugName = true);
document.getElementById('debugStreamerInput').addEventListener('blur', () => isEditingDebugName = false);

function showToast(msg) {
  const t = document.getElementById('toast');
  t.innerText = msg;
  t.style.display = 'block';
  setTimeout(() => t.style.display = 'none', 2500);
}

function setViewerFilter(flt) {
  currentViewerFilter = flt;
  const ids = ['all', 'staff', 'visitor', 'vipmod'];
  for (const id of ids) {
    const btn = document.getElementById('flt-' + id);
    if (btn) btn.style.background = (id === flt) ? 'var(--twitch)' : '#272736';
  }
  applyFilterAndRender();
}

function applyFilterAndRender() {
  renderViewers(lastViewersList, lastTrackedUser);
}

async function fetchStatus() {
  try {
    const res = await fetch(API + '/status');
    if (!res.ok) throw new Error();
    const data = await res.json();

    const badge = document.getElementById('connBadge');
    badge.className = 'badge-status status-online';
    badge.innerText = '● LIVE MIT SPIEL VERBUNDEN (#' + data.channel + ')';

    document.getElementById('statTotal').innerText = data.totalActive;
    document.getElementById('statPeeps').innerText = data.peepCount;
    document.getElementById('statStaff').innerText = data.staffCount;
    document.getElementById('sceneLabel').innerText = 'Aktuelle Kirmes: ' + (data.scene || '-');

    if (!isEditingChannel) {
      document.getElementById('channelInput').value = data.channel || '';
    }

    document.getElementById('chkTopHeader').checked = data.showTopHeader;
    document.getElementById('chkNametags').checked = data.showNametags;
    document.getElementById('chkBadges').checked = data.showBadges;
    document.getElementById('chkModsStaff').checked = data.assignModsToStaff;
    document.getElementById('chkAllStaff').checked = data.allowAllViewersAsStaff;
    document.getElementById('chkExcludeSelf').checked = data.excludeBroadcaster;
    if (document.getElementById('chkDebugMode')) {
      document.getElementById('chkDebugMode').checked = !!data.debugMode;
    }
    if (!isEditingDebugName && document.getElementById('debugStreamerInput')) {
      document.getElementById('debugStreamerInput').value = data.debugStreamerName || '';
    }
    if (data.speechBubbleDuration && document.getElementById('selBubbleDuration')) {
      document.getElementById('selBubbleDuration').value = String(data.speechBubbleDuration);
    }

    // Demo Status
    const dBadge = document.getElementById('demoBadge');
    const btnStart = document.getElementById('btnStartDemo');
    const btnStop = document.getElementById('btnStopDemo');
    const progText = document.getElementById('demoProgressText');
    if (dBadge && btnStart && btnStop) {
      if (data.vodDemoActive) {
        dBadge.className = 'tag tag-mod';
        dBadge.innerText = '● LIVE AKTIV';
        btnStart.style.display = 'none';
        btnStop.style.display = 'inline-block';
        progText.innerText = `Läuft: ${data.vodDemoPlayed || 0} von ${data.vodDemoCount || 885} Nachrichten abgespielt`;
      } else {
        dBadge.className = 'tag';
        dBadge.style.background = 'rgba(255,64,129,0.2)';
        dBadge.style.color = 'var(--pink)';
        dBadge.innerText = 'INAKTIV';
        btnStart.style.display = 'inline-block';
        btnStop.style.display = 'none';
        if (data.vodDemoPlayed > 0) {
          progText.innerText = `Pausiert bei ${data.vodDemoPlayed} Nachrichten`;
        } else {
          progText.innerText = `Bereit (${data.vodDemoCount || 885} echte VOD-Nachrichten geladen)`;
        }
      }
    }
    if (data.maxVisibleNametags && document.getElementById('selMaxNametags')) {
      document.getElementById('selMaxNametags').value = String(data.maxVisibleNametags);
    }
    if (data.viewerInactivityMinutes !== undefined && document.getElementById('selInactivityMin')) {
      document.getElementById('selInactivityMin').value = String(data.viewerInactivityMinutes);
    }

    lastViewersList = data.viewers || [];
    lastTrackedUser = data.trackedUser || '';
    applyFilterAndRender();
  } catch (e) {
    const badge = document.getElementById('connBadge');
    badge.className = 'badge-status status-offline';
    badge.innerText = '● Spiel nicht gestartet (Starte Fairground Online)';
  }
}

function getRoleRank(role) {
  if (role === 'Broadcaster') return 6;
  if (role === 'Moderator') return 5;
  if (role === 'VIP') return 4;
  if (role === 'Founder') return 3;
  if (role === 'Subscriber') return 2;
  return 1;
}

function renderViewers(list, trackedUser) {
  const tbody = document.getElementById('viewerTableBody');
  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan=""4"" style=""color:var(--muted); text-align:center; padding:18px;"">Noch keine Zuschauer auf dem Platz. Schreib etwas in den Twitch-Chat!</td></tr>';
    return;
  }

  const searchVal = (document.getElementById('viewerSearchInput') ? document.getElementById('viewerSearchInput').value : '').trim().toLowerCase();
  const sortMode = document.getElementById('viewerSortSelect') ? document.getElementById('viewerSortSelect').value : 'az';

  let filtered = list.filter(v => {
    if (currentViewerFilter === 'staff' && !v.job) return false;
    if (currentViewerFilter === 'visitor' && v.job) return false;
    if (currentViewerFilter === 'vipmod' && v.role === 'Guest') return false;
    if (searchVal) {
      const hay = ((v.name || '') + ' ' + (v.user || '') + ' ' + (v.job || '') + ' ' + (v.ride || '') + ' ' + (v.role || '')).toLowerCase();
      if (!hay.includes(searchVal)) return false;
    }
    return true;
  });

  filtered.sort((a, b) => {
    const aTracked = trackedUser && a.user.toLowerCase() === trackedUser.toLowerCase() ? 1 : 0;
    const bTracked = trackedUser && b.user.toLowerCase() === trackedUser.toLowerCase() ? 1 : 0;
    if (aTracked !== bTracked) return bTracked - aTracked;

    if (sortMode === 'role') {
      const rDiff = getRoleRank(b.role) - getRoleRank(a.role);
      if (rDiff !== 0) return rDiff;
    } else if (sortMode === 'job') {
      const aJob = a.job ? 1 : 0;
      const bJob = b.job ? 1 : 0;
      if (aJob !== bJob) return bJob - aJob;
    }
    return (a.name || a.user).localeCompare(b.name || b.user, 'de', { sensitivity: 'base' });
  });

  if (!filtered.length) {
    tbody.innerHTML = '<tr><td colspan=""4"" style=""color:var(--muted); text-align:center; padding:18px;"">Keine Zuschauer für diesen Filter / Suchbegriff gefunden.</td></tr>';
    return;
  }

  let html = '';
  for (const v of filtered) {
    let roleHtml = '';
    if (v.role === 'Broadcaster') roleHtml += '<span class=""tag"" style=""background:rgba(255,42,42,0.25); color:#ff4d4d; border:1px solid #ff2a2a;"">🎥 STREAMER</span>';
    else if (v.role === 'Moderator') roleHtml += '<span class=""tag tag-mod"">MOD</span>';
    else if (v.role === 'VIP') roleHtml += '<span class=""tag tag-vip"">VIP</span>';
    else if (v.role === 'Founder') roleHtml += '<span class=""tag tag-sub"">FOUNDER</span>';
    else if (v.role === 'Subscriber') roleHtml += '<span class=""tag tag-sub"">SUB</span>';
    else roleHtml += '<span class=""tag tag-guest"">VIEWER</span>';

    if (v.subMonths > 0) roleHtml += `<span class=""tag tag-sub"">${v.subMonths}M</span>`;
    if (v.verified) roleHtml += '<span class=""tag tag-guest"">✓ PARTNER</span>';

    let locHtml = v.job
      ? `<span class=""tag tag-job"">🛠️ ${v.job}</span>`
      : `<span style=""color:var(--green); font-weight:600;"">🚶 Besucher auf dem Platz ${v.ride ? '• 🎢 ' + v.ride : ''}</span>`;

    const isTracked = trackedUser && trackedUser.toLowerCase() === v.user.toLowerCase();
    const trackBtn = isTracked
      ? `<button class=""btn-sm"" style=""background:#e11d48; color:#fff;"" onclick=""trackViewer('clear')"">❌ Radar aus</button>`
      : `<button class=""btn-sm"" style=""background:#0284c7; color:#fff;"" onclick=""trackViewer('${v.user}')"">📍 Radar-Markierung</button>`;

    html += `<tr style=""${isTracked ? 'background:rgba(0,229,255,0.12);' : ''}"">
      <td><b style=""color:${v.color}; font-size:14px;"">${v.name}</b> ${isTracked ? '<span class=""tag tag-job"">📍 MARKIERT</span>' : ''}</td>
      <td>${roleHtml}</td>
      <td>${locHtml}</td>
      <td>
        ${trackBtn}
        <select onchange=""if(this.value){sendJobCmd('${v.user}', this.value); this.value='';}"" style=""background:#111118; border:1px solid var(--green); color:var(--green); padding:5px 8px; border-radius:6px; font-weight:700; font-size:11px; cursor:pointer;"">
          <option value="""">🎢 Mitfahren in...</option>
          <option value=""!mitfahren"">🚶 Frei rumlaufen / Bei mir</option>
          <option value=""!mitfahren megadance"">🎢 MegaDance</option>
          <option value=""!mitfahren soundexpress"">🎢 SoundExpress</option>
          <option value=""!mitfahren teufelstanz"">🎢 Teufelstanz</option>
          <option value=""!mitfahren starlight"">🎢 Starlight</option>
          <option value=""!mitfahren rotator"">🎢 Rotator</option>
          <option value=""!mitfahren xplosion"">🎢 Xplosion</option>
          <option value=""!mitfahren turaka"">🎢 Turaka</option>
          <option value=""!mitfahren pinball"">🎢 Pinball</option>
          <option value=""!mitfahren funhouse"">🎢 FunHouse</option>
          <option value=""!mitfahren ferriswheel"">🎡 Riesenrad</option>
          <option value=""!mitfahren hallofmirrors"">🪞 Spiegelkabinett</option>
        </select>
        <button class=""btn-sm btn-cyan"" onclick=""sendJobCmd('${v.user}', '!kasse')"">🎟️ Kasse</button>
        <button class=""btn-sm btn-cyan"" onclick=""sendJobCmd('${v.user}', '!chip')"">🪙 Chip-Sammler</button>
        <button class=""btn-sm btn-gold"" onclick=""sendJobCmd('${v.user}', '!bude')"">🍿 Bude</button>
        <button class=""btn-sm"" onclick=""sendJobCmd('${v.user}', '!jubel')"">🙌 Jubeln</button>
        <button class=""btn-sm btn-gold"" onclick=""sendJobCmd('${v.user}', '!feuerwerk')"">🎆 Feuerwerk</button>
      </td>
    </tr>`;
  }
  tbody.innerHTML = html;
}

async function changeChannel() {
  const ch = document.getElementById('channelInput').value.trim().toLowerCase();
  if (!ch) return;
  await fetch(`${API}/set?key=Channel&val=${encodeURIComponent(ch)}`);
  showToast('Kanal live gewechselt auf #' + ch + '!');
  fetchStatus();
}

async function toggleSetting(key, val) {
  await fetch(`${API}/set?key=${encodeURIComponent(key)}&val=${val ? 'true' : 'false'}`);
  showToast('Einstellung live im Spiel übernommen!');
  fetchStatus();
}

async function setNumericSetting(key, val) {
  await fetch(`${API}/set?key=${encodeURIComponent(key)}&val=${encodeURIComponent(val)}`);
  showToast('Sprechblasen-Dauer auf ' + val + ' Sek. gesetzt!');
  fetchStatus();
}

async function sendJobCmd(user, cmd) {
  await fetch(`${API}/action?user=${encodeURIComponent(user)}&cmd=${encodeURIComponent(cmd)}`);
  showToast(user + ' -> ' + cmd + ' ausgeführt!');
  setTimeout(fetchStatus, 300);
}

async function trackViewer(user) {
  await fetch(`${API}/track?user=${encodeURIComponent(user)}`);
  if (user === 'clear') showToast('Radar-Markierung im Spiel ausgeschaltet!');
  else showToast('📍 ' + user + ' wird jetzt im Spiel mit Durch-Wände-Radar & Entfernung markiert!');
  setTimeout(fetchStatus, 200);
}

async function saveDebugName() {
  const val = document.getElementById('debugStreamerInput').value.trim();
  await fetch(`${API}/set?key=DebugStreamerName&val=${encodeURIComponent(val)}`);
  await fetch(`${API}/set?key=DebugMode&val=true`);
  showToast('🧪 Debug-Name gespeichert & Debug-Modus aktiviert!');
  fetchStatus();
}

async function startVodDemo() {
  await fetch(`${API}/demo?act=start`);
  showToast('🎬 Freizeitgestoert VOD-Chat Demo gestartet! Zuschauer spawnen jetzt im Spiel.');
  setTimeout(fetchStatus, 300);
}

async function stopVodDemo() {
  await fetch(`${API}/demo?act=stop`);
  showToast('⏹️ Demo-Chat gestoppt.');
  setTimeout(fetchStatus, 300);
}

async function simulateDebugChat() {
  const nameVal = document.getElementById('debugStreamerInput').value.trim();
  const msgVal = document.getElementById('debugSimMsgInput').value.trim() || 'Hallo von der Kirmes!';
  if (nameVal) {
    await fetch(`${API}/set?key=DebugStreamerName&val=${encodeURIComponent(nameVal)}`);
  }
  await fetch(`${API}/simulate?user=${encodeURIComponent(nameVal)}&msg=${encodeURIComponent(msgVal)}`);
  document.getElementById('debugSimMsgInput').value = '';
  showToast('🚀 Als [STREAMER] im Spiel gespawnt / Nachricht gesendet!');
  setTimeout(fetchStatus, 300);
}

function copyTextFallback(txt) {
  if (navigator.clipboard && window.isSecureContext) {
    navigator.clipboard.writeText(txt);
  } else {
    const ta = document.createElement('textarea');
    ta.value = txt;
    document.body.appendChild(ta);
    ta.select();
    document.execCommand('copy');
    document.body.removeChild(ta);
  }
}

function copyBotText() {
  const txt = '🎡 LIVE AUF DER KIRMES! Schreibt in den Chat, um im Spiel zu spawnen! Befehle: !jubel | !klatschen | !zugabe | !mitfahren [Name] (z.B. !mitfahren mega dance) | !kasse (Kassierer) | !chip (Fahrchips sammeln) | !bude (Buden-Verkäufer)!';
  document.getElementById('botTemplateBox').value = txt;
  copyTextFallback(txt);
  showToast('Chat-Erklärung in die Zwischenablage kopiert!');
}

function copyNightbotCmd() {
  const txt = '!addcom !kirmes 🎡 LIVE AUF DER KIRMES! Schreibt in den Chat, um im Spiel zu spawnen! Befehle: !jubel | !klatschen | !zugabe | !mitfahren [Name] (z.B. !mitfahren mega dance) | !kasse (Kassierer) | !chip (Fahrchips sammeln) | !bude (Buden-Verkäufer)!';
  document.getElementById('botTemplateBox').value = txt;
  copyTextFallback(txt);
  showToast('Nightbot !addcom Befehl kopiert! Einfach in deinen Twitch-Chat einfügen!');
}

async function sendInGameBanner() {
  const sec = document.getElementById('announceDuration') ? document.getElementById('announceDuration').value : '60';
  const msg = 'CHAT-BEFEHLE: !jubel • !klatschen • !zugabe • !mitfahren [Name] • !kasse • !chip • !bude';
  await fetch(`${API}/announce?msg=${encodeURIComponent(msg)}&sec=${sec}`);
  showToast('Befehls-Banner wird jetzt im eigenen Durchsage-Banner eingeblendet!');
}

async function sendCustomBanner() {
  const inp = document.getElementById('customBannerInput');
  const sec = document.getElementById('announceDuration') ? document.getElementById('announceDuration').value : '60';
  const msg = inp.value.trim();
  if (!msg) return;
  await fetch(`${API}/announce?msg=${encodeURIComponent(msg)}&sec=${sec}`);
  inp.value = '';
  showToast('Deine Durchsage wird jetzt im eigenen Banner unterhalb der Join-Anzeige eingeblendet!');
}

async function clearBanner() {
  await fetch(`${API}/announce?msg=clear`);
  showToast('Durchsage-Banner im Spiel ausgeblendet!');
}

setInterval(fetchStatus, 1500);
fetchStatus();
</script>
</body>
</html>";
                File.WriteAllText(htmlPath, html, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Twitch] Konnte Live-Dashboard HTML nicht erstellen: {ex.Message}");
            }
        }
    }

    public enum ViewerRole
    {
        Guest = 0,
        Subscriber = 1,
        Founder = 2,
        VIP = 3,
        Moderator = 4,
        Broadcaster = 5
    }

    public struct UserChatCommand
    {
        public string Username;
        public string CommandText;
        public bool IsCommand;
    }

    public enum ViewerJobPreference
    {
        Auto = 0,
        VisitorOnly = 1,
        StaffKasse = 2,
        StaffChip = 3,
        StaffBude = 4,
        StaffReko = 5,
        StaffAny = 6
    }

    public class ActiveUserActionState
    {
        public string Username;
        public int PeepId;
        public bdt ActionType;
        public bool IsAgainOrScream;
        public string StatusBubbleText; // z.B. "🙌 JUBELT!", "👏 KLATSCHT!", "🔊 ZUGABE!"
        public float RemainingSeconds;
        public Seat ActiveSeat;
        public Seat_Peep_Animation_Controller SeatAnimCtrl;
        public Peep_Interaction_Controller PeepInteractCtrl;
        public Animator WalkAnimator;
        public Peep_Moving TargetMoving;
        public Peep_Animation_Mode OriginalAnimMode;
        public bool HadOriginalAnimMode;
        public float NextSeatRefreshTime;
        public HandIKController HandIK;
        public Basic_Seating_Pos SeatingPos;
    }

    public class ViewerInfo
    {
        public string Username;
        public string DisplayName;
        public string TwitchColorHex;
        public ViewerRole Role;
        public bool IsSub;
        public bool IsFounder;
        public bool IsVip;
        public bool IsMod;
        public bool IsVerified;
        public bool IsBot;
        public int SubMonths = 0;
        public int SubTier = 1;
        public DateTime LastSeen;
        public string AssignedStaffRoleTitle = null;
        public string ActiveActionBadge = null; // Wird eingeblendet, während dieser User gerade !jubel / !klatschen / !zugabe macht
        public string ActiveSpeechBubble = null; // Live-Sprechblase über dem Kopf für normale Chat-Nachrichten
        public float SpeechBubbleExpireTime = 0f;
        public ViewerJobPreference JobPreference = ViewerJobPreference.Auto;

        private static readonly string[] DefaultTwitchColors = new string[]
        {
            "#FF4500", // OrangeRed
            "#2E8B57", // SeaGreen
            "#00FF7F", // SpringGreen
            "#D2691E", // Chocolate
            "#5F9EA0", // CadetBlue
            "#1E90FF", // DodgerBlue
            "#FF69B4", // HotPink
            "#8A2BE2", // BlueViolet
            "#FF7F50", // Coral
            "#9ACD32", // YellowGreen
            "#00CED1", // DarkTurquoise
            "#DAA520", // GoldenRod
            "#FF1493", // DeepPink
            "#00BFFF", // DeepSkyBlue
            "#FFD700"  // Gold
        };

        public bool IsStaffEligible => Role == ViewerRole.Broadcaster || Role == ViewerRole.Moderator || Role == ViewerRole.VIP || Role == ViewerRole.Founder;

        public string GetEffectiveNameColorHex()
        {
            if (!string.IsNullOrEmpty(TwitchColorHex) && TwitchColorHex.StartsWith("#") && TwitchColorHex.Length == 7)
            {
                return TwitchColorHex;
            }

            switch (Role)
            {
                case ViewerRole.Broadcaster:
                    return "#FF4500";
                case ViewerRole.Moderator:
                    return TwitchPlugin.ModColorHex.Value;
                case ViewerRole.VIP:
                    return TwitchPlugin.VipColorHex.Value;
                case ViewerRole.Founder:
                case ViewerRole.Subscriber:
                    return TwitchPlugin.SubColorHex.Value;
                default:
                    int hash = Math.Abs((Username ?? "guest").ToLowerInvariant().GetHashCode());
                    return DefaultTwitchColors[hash % DefaultTwitchColors.Length];
            }
        }

        public Color GetColor()
        {
            string hex = GetEffectiveNameColorHex();
            if (ColorUtility.TryParseHtmlString(hex, out Color col))
            {
                return col;
            }
            return Color.white;
        }

        public string GetTag()
        {
            string shownName = !string.IsNullOrEmpty(DisplayName) ? DisplayName : Username;
            string nameColor = GetEffectiveNameColorHex();
            string coloredName = $"<color={nameColor}><b>{shownName}</b></color>";

            string subBadgeText = "";
            if (IsFounder)
            {
                subBadgeText = SubMonths > 0
                    ? $" <color=#C962FF><b>[FOUNDER • {SubMonths}M]</b></color>"
                    : " <color=#C962FF><b>[FOUNDER]</b></color>";
            }
            else if (IsSub || SubMonths > 0)
            {
                string tierStr = SubTier > 1 ? $" T{SubTier}" : "";
                subBadgeText = SubMonths > 0
                    ? $" <color=#FFB400><b>[SUB{tierStr} • {SubMonths}M]</b></color>"
                    : $" <color=#FFB400><b>[SUB{tierStr}]</b></color>";
            }

            string verifiedText = IsVerified ? " <color=#BF94FF><b>[VERIFIZIERT]</b></color>" : "";
            string botText = IsBot ? " <color=#80DEEA><b>[BOT]</b></color>" : "";

            string jobBadge = !string.IsNullOrEmpty(AssignedStaffRoleTitle)
                ? $"<color=#00E5FF><b>[{AssignedStaffRoleTitle}]</b></color> "
                : "";

            string actionLine = !string.IsNullOrEmpty(ActiveActionBadge)
                ? $"\n<color=#FFFF00><b>{ActiveActionBadge}</b></color>"
                : "";

            string speechLine = !string.IsNullOrEmpty(ActiveSpeechBubble)
                ? $"\n<color=#FFF8DC>\"{ActiveSpeechBubble}\"</color>"
                : "";

            switch (Role)
            {
                case ViewerRole.Broadcaster:
                    return $"{jobBadge}<color=#FF2A2A><b>[STREAMER]</b></color>{verifiedText}{subBadgeText}{botText}\n{coloredName}{actionLine}{speechLine}";

                case ViewerRole.Moderator:
                    return $"{jobBadge}<color=#00E676><b>[MOD]</b></color>{verifiedText}{subBadgeText}{botText}\n{coloredName}{actionLine}{speechLine}";

                case ViewerRole.VIP:
                    return $"{jobBadge}<color=#FF1493><b>[VIP]</b></color>{verifiedText}{subBadgeText}{botText}\n{coloredName}{actionLine}{speechLine}";

                case ViewerRole.Founder:
                    return $"{jobBadge}<color=#C962FF><b>[FOUNDER{(SubMonths > 0 ? $" • {SubMonths}M" : "")}]</b></color>{verifiedText}{botText}\n{coloredName}{actionLine}{speechLine}";

                case ViewerRole.Subscriber:
                    string tierStr = SubTier > 1 ? $" T{SubTier}" : "";
                    if (SubMonths > 0)
                        return $"{jobBadge}<color=#FFB400><b>[SUB{tierStr} • {SubMonths}M]</b></color>{verifiedText}{botText}\n{coloredName}{actionLine}{speechLine}";
                    return $"{jobBadge}<color=#FFB400><b>[SUB{tierStr}]</b></color>{verifiedText}{botText}\n{coloredName}{actionLine}{speechLine}";

                default:
                    if (IsVerified)
                        return $"{jobBadge}<color=#BF94FF><b>[STREAMER / PARTNER]</b></color>{botText}\n{coloredName}{actionLine}{speechLine}";
                    return $"{jobBadge}<color=#A970FF>[VIEWER]</color>{botText}\n{coloredName}{actionLine}{speechLine}";
            }
        }
    }

    public class StaffSlot
    {
        public int InstanceId;
        public Transform TargetTransform;
        public string JobTitle;
        public float HeightOffset;
    }

    public class TwitchRunner : MonoBehaviour
    {
        public TwitchRunner(IntPtr ptr) : base(ptr) { }

        // Thread-safe queues
        private static readonly ConcurrentQueue<ViewerInfo> _newViewersQueue = new ConcurrentQueue<ViewerInfo>();
        private static readonly ConcurrentQueue<UserChatCommand> _commandQueue = new ConcurrentQueue<UserChatCommand>();

        // Cache of known viewers
        private static readonly Dictionary<string, ViewerInfo> _knownViewers = new Dictionary<string, ViewerInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<ViewerInfo> _availableViewers = new Queue<ViewerInfo>();
        private static readonly Queue<ViewerInfo> _availablePriorityViewers = new Queue<ViewerInfo>();
        private static readonly Queue<ViewerInfo> _availableStaffQueue = new Queue<ViewerInfo>();

        // Active Guest Peep assignments: Peep Key -> ViewerInfo
        private readonly Dictionary<int, ViewerInfo> _peepAssignments = new Dictionary<int, ViewerInfo>();
        private readonly Dictionary<int, TMP_Text> _peepNametags = new Dictionary<int, TMP_Text>();
        private readonly Dictionary<string, int> _usernameToPeepId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _assignedUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Active Staff assignments: Transform InstanceID -> ViewerInfo
        private readonly List<StaffSlot> _discoveredStaffSlots = new List<StaffSlot>();
        private readonly List<Ride_Controller> _cachedRideControllers = new List<Ride_Controller>();
        private readonly List<Seat_Peep_Animation_Controller> _cachedSeatAnimControllers = new List<Seat_Peep_Animation_Controller>();
        private readonly List<Peep_Interaction_Controller> _cachedPeepInteractionControllers = new List<Peep_Interaction_Controller>();
        private readonly Dictionary<int, string> _rideInstanceNameCache = new Dictionary<int, string>();
        private readonly Dictionary<string, int> _viewerCurrentRideId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ViewerInfo> _staffAssignments = new Dictionary<int, ViewerInfo>();
        private readonly Dictionary<int, TMP_Text> _staffNametags = new Dictionary<int, TMP_Text>();
        private readonly Dictionary<string, int> _usernameToStaffId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Cached Staff Discovery (Once per scene load to prevent lag spikes!)
        private bool _staffDiscoveredForScene = false;
        private int _lastHeaderCount = -1;
        private string _lastHeaderChannel = "";

        // Per-User active animations (Username -> ActiveUserActionState)
        private readonly Dictionary<string, ActiveUserActionState> _activeUserActions = new Dictionary<string, ActiveUserActionState>(StringComparer.OrdinalIgnoreCase);

        // Native Multiplayer Nametag Template Prefab
        private static GameObject _nativeNametagTemplate = null;

        // VOD Chat Replay Demo Mode (Freizeitgestoert Kirmes VOD)
        private static bool _vodDemoActive = false;
        private static float _vodDemoTimer = 0f;
        private static int _vodDemoIdx = 0;
        private static readonly List<VodDemoItem> _vodDemoItems = new List<VodDemoItem>();

        private struct VodDemoItem
        {
            public float Offset;
            public string User;
            public string Name;
            public string Msg;
            public string Color;
            public bool IsSub;
            public bool IsMod;
            public bool IsVip;
            public bool IsBroadcaster;
        }

        // Cached Badge Sprites (mod.png, vip.png, sub.png, twitch.png)
        private static readonly Dictionary<string, Sprite> _badgeSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static bool _badgesLoaded = false;

        // Top Header UI Overlay
        private GameObject _headerCanvasGo = null;
        private Text _headerTextComp = null;
        private GameObject _joinBannerBorderGo = null;
        private Text _joinBannerTextComp = null;
        private string _latestJoinFeedText = "";
        private string _lastRenderedJoinFeedText = "";
        private float _joinFeedExpireTime = 0f;

        // Separates Durchsage-Banner unter dem Join-Feed (wird NIEMALS von Joins überschrieben!)
        private GameObject _announceBannerBorderGo = null;
        private Text _announceBannerTextComp = null;
        private string _latestAnnounceText = "";
        private string _lastRenderedAnnounceText = "";
        private float _announceExpireTime = 0f;

        // 2D Durch-Wände-Radar & Entfernungs-Marker für gesuchten Zuschauer (Option 2)
        private string _trackedUsername = "";
        private GameObject _radarMarkerBorderGo = null;
        private RectTransform _radarMarkerRt = null;
        private Text _radarMarkerTextComp = null;

        // Scene tracking
        private string _lastSceneName;

        // Dynamic Channel & Lobby Tracking
        private string _currentChannel = "";
        private string _desiredChannel = "";
        private float _lobbyCheckTimer = 0f;
        private TcpClient _currentTcp = null;

        // IRC Client thread & Local Live-Dashboard Server
        private Thread _ircThread;
        private Thread _httpThread;
        private TcpListener _httpListener;
        private volatile string _cachedStatusJson = "{}";
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private bool _isRunning;
        private float _scanTimer;
        private float _fadeTimer;
        private float _dashboardRefreshTimer;

        private void Start()
        {
            if (!TwitchPlugin.ModEnabled.Value)
            {
                TwitchPlugin.Log?.LogInfo("[TwitchRunner] Mod ist in der Config deaktiviert.");
                return;
            }

            LoadBadgeSprites();

            _isRunning = true;
            _ircThread = new Thread(IrcWorkerLoop)
            {
                IsBackground = true,
                Name = "TwitchIRCThread"
            };
            _ircThread.Start();

            _httpThread = new Thread(HttpDashboardWorkerLoop)
            {
                IsBackground = true,
                Name = "TwitchDashboardHttpThread"
            };
            _httpThread.Start();
        }

        private static void LoadBadgeSprites()
        {
            if (_badgesLoaded) return;
            _badgesLoaded = true;

            try
            {
                string iconsDir = Path.Combine(Paths.PluginPath, "FairgroundTwitch", "icons");
                if (!Directory.Exists(iconsDir))
                {
                    TwitchPlugin.Log?.LogWarning($"[Twitch] Icon-Ordner nicht gefunden: {iconsDir}");
                    return;
                }

                string[] iconNames = new[] { "mod", "vip", "sub", "twitch", "guest" };
                foreach (string name in iconNames)
                {
                    string filePath = Path.Combine(iconsDir, name + ".png");
                    if (File.Exists(filePath))
                    {
                        byte[] bytes = File.ReadAllBytes(filePath);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (ImageConversion.LoadImage(tex, bytes))
                        {
                            tex.wrapMode = TextureWrapMode.Clamp;
                            tex.filterMode = FilterMode.Bilinear;
                            tex.hideFlags = HideFlags.HideAndDontSave;
                            UnityEngine.Object.DontDestroyOnLoad(tex);

                            Sprite spr = Sprite.Create(
                                tex,
                                new Rect(0, 0, tex.width, tex.height),
                                new Vector2(0.5f, 0.5f),
                                100f
                            );
                            spr.hideFlags = HideFlags.HideAndDontSave;
                            UnityEngine.Object.DontDestroyOnLoad(spr);

                            _badgeSprites[name] = spr;
                            TwitchPlugin.Log?.LogInfo($"[Twitch] Badge-Icon geladen: {name}.png ({tex.width}x{tex.height})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning($"[Twitch] Fehler beim Laden der Badge-Icons: {ex.Message}");
            }
        }

        private static Sprite GetPrimaryBadgeSprite(ViewerInfo v)
        {
            if (!_badgesLoaded) LoadBadgeSprites();

            switch (v.Role)
            {
                case ViewerRole.Moderator:
                    if (_badgeSprites.TryGetValue("mod", out Sprite modSpr)) return modSpr;
                    break;
                case ViewerRole.VIP:
                    if (_badgeSprites.TryGetValue("vip", out Sprite vipSpr)) return vipSpr;
                    break;
                case ViewerRole.Founder:
                case ViewerRole.Subscriber:
                    if (_badgeSprites.TryGetValue("sub", out Sprite subSpr)) return subSpr;
                    break;
                case ViewerRole.Broadcaster:
                    if (_badgeSprites.TryGetValue("twitch", out Sprite bcSpr)) return bcSpr;
                    break;
                default:
                    if (_badgeSprites.TryGetValue("twitch", out Sprite twSpr)) return twSpr;
                    break;
            }
            return null;
        }

        private static Sprite GetSecondaryBadgeSprite(ViewerInfo v)
        {
            if (!_badgesLoaded) LoadBadgeSprites();

            if ((v.Role == ViewerRole.Moderator || v.Role == ViewerRole.VIP || v.Role == ViewerRole.Broadcaster) && (v.IsSub || v.IsFounder || v.SubMonths > 0))
            {
                if (_badgeSprites.TryGetValue("sub", out Sprite subSpr)) return subSpr;
            }
            return null;
        }

        private void Update()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (_lastSceneName != null && _lastSceneName != currentScene)
            {
                TwitchPlugin.Log?.LogInfo($"[Twitch] Szenenwechsel erkannt ({_lastSceneName} -> {currentScene}), setze Zuweisungen zurueck.");
                ResetAllAssignments();
            }
            _lastSceneName = currentScene;

            // Throttled Queue Processing (Hardcore-Chat-Schutz vor Main-Thread Lag Spikes)
            int drainedViewers = 0;
            while (drainedViewers < 12 && _newViewersQueue.TryDequeue(out ViewerInfo viewer))
            {
                drainedViewers++;
                RegisterViewer(viewer);
            }

            int drainedCmds = 0;
            while (drainedCmds < 8 && _commandQueue.TryDequeue(out UserChatCommand userCmd))
            {
                drainedCmds++;
                HandleUserChatCommand(userCmd);
            }

            int drainedActions = 0;
            while (drainedActions < 10 && _mainThreadActions.TryDequeue(out Action act))
            {
                drainedActions++;
                try { act?.Invoke(); } catch { }
            }

            UpdateActiveUserActions();
            UpdateTrackedViewerRadar();
            UpdateVodDemoReplay();

            _fadeTimer += Time.deltaTime;
            if (_fadeTimer >= 0.25f)
            {
                _fadeTimer = 0f;
                UpdateNametagDistanceFading();
            }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 1.5f)
            {
                _scanTimer = 0f;
                UpdateNametagsAndAssignments();
                CheckSpeechBubblesAndRideBoarding();
                UpdateTopHeaderOverlay();
            }

            _dashboardRefreshTimer += Time.deltaTime;
            if (_dashboardRefreshTimer >= 3.0f)
            {
                _dashboardRefreshTimer = 0f;
                RefreshDashboardStatusJson();
            }

            _lobbyCheckTimer += Time.deltaTime;
            if (_lobbyCheckTimer >= 2.0f)
            {
                _lobbyCheckTimer = 0f;
                UpdateLobbyTwitchChannel();
            }
        }

        private void UpdateNametagDistanceFading()
        {
            if (!TwitchPlugin.ShowNametags.Value) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;

            void ProcessTagDict(Dictionary<int, TMP_Text> dict)
            {
                foreach (var kvp in dict)
                {
                    TMP_Text tm = kvp.Value;
                    if (tm == null || tm.gameObject == null) continue;

                    Vector3 pos = tm.transform.position;
                    Vector3 diff = pos - camPos;

                    // 1. Frustum Dot Check: Schilder HINTER der Kamera sofort deaktivieren (verhindert Ruckeln beim Umschauen!)
                    float dot = Vector3.Dot(diff, camFwd);
                    if (dot <= 0.2f)
                    {
                        if (tm.gameObject.activeSelf) tm.gameObject.SetActive(false);
                        continue;
                    }

                    float sqrDist = diff.sqrMagnitude;
                    bool hasActiveAction = false;
                    try
                    {
                        if (dict == _peepNametags && _peepAssignments.TryGetValue(kvp.Key, out ViewerInfo pv) && pv != null && !string.IsNullOrEmpty(pv.ActiveActionBadge))
                        {
                            hasActiveAction = true;
                        }
                        else if (dict == _staffNametags && _staffAssignments.TryGetValue(kvp.Key, out ViewerInfo sv) && sv != null && !string.IsNullOrEmpty(sv.ActiveActionBadge))
                        {
                            hasActiveAction = true;
                        }
                    }
                    catch { }

                    float targetAlpha;
                    if (hasActiveAction)
                    {
                        targetAlpha = sqrDist <= 14400f ? 1f : 0f; // 120m
                    }
                    else if (sqrDist <= 484f) // bis 22m volle Sichtbarkeit
                    {
                        targetAlpha = 1f;
                    }
                    else if (sqrDist >= 1600f) // ab 40m komplett unsichtbar (spart 100% GPU/CPU-Last)
                    {
                        targetAlpha = 0f;
                    }
                    else
                    {
                        float dist = Mathf.Sqrt(sqrDist);
                        targetAlpha = 1f - ((dist - 22f) / 18f);
                    }

                    if (targetAlpha <= 0.02f)
                    {
                        if (tm.gameObject.activeSelf) tm.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (!tm.gameObject.activeSelf) tm.gameObject.SetActive(true);
                        Color c = tm.color;
                        if (Math.Abs(c.a - targetAlpha) > 0.05f)
                        {
                            c.a = targetAlpha;
                            tm.color = c;
                        }
                    }
                }
            }

            try
            {
                ProcessTagDict(_peepNametags);
                ProcessTagDict(_staffNametags);
            }
            catch { }
        }

        private void UpdateLobbyTwitchChannel()
        {
            try
            {
                string configChannel = TwitchPlugin.ChannelName.Value.ToLowerInvariant().Trim();

                if (Steam_Manager.Is_Connected && Steam_Manager.Lobby != null)
                {
                    bool isOwner = Steam_Manager.Is_Owner;

                    if (!isOwner)
                    {
                        string hostStreamer = null;

                        string lobbyName = Steam_Manager.Lobby.lobby_Name;
                        if (!string.IsNullOrEmpty(lobbyName))
                        {
                            int tagIdx = lobbyName.IndexOf("[twitch:", StringComparison.OrdinalIgnoreCase);
                            if (tagIdx != -1)
                            {
                                int endIdx = lobbyName.IndexOf(']', tagIdx);
                                if (endIdx != -1)
                                {
                                    hostStreamer = lobbyName.Substring(tagIdx + 8, endIdx - (tagIdx + 8)).Trim();
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(hostStreamer))
                        {
                            var hostPlayer = Steam_Manager.Lobby.Lobby_Owner;
                            if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.Name))
                            {
                                hostStreamer = hostPlayer.Name.Trim();
                            }
                        }

                        if (!string.IsNullOrEmpty(hostStreamer))
                        {
                            SetDesiredChannel(hostStreamer.ToLowerInvariant());
                            return;
                        }
                    }
                }

                SetDesiredChannel(configChannel);
            }
            catch { }
        }

        private void SetDesiredChannel(string newChannel)
        {
            if (string.IsNullOrEmpty(newChannel)) return;

            if (!string.Equals(_desiredChannel, newChannel, StringComparison.OrdinalIgnoreCase))
            {
                _desiredChannel = newChannel;
                TwitchPlugin.Log?.LogInfo($"[Twitch] Aktiver Zielkanal geändert auf: #{_desiredChannel}");

                if (!string.Equals(_currentChannel, _desiredChannel, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        _currentTcp?.Close();
                    }
                    catch { }
                }
            }
        }

        private void ResetAllAssignments()
        {
            foreach (var kvp in _peepNametags)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
                }
            }
            _peepNametags.Clear();
            _peepAssignments.Clear();
            _usernameToPeepId.Clear();

            foreach (var kvp in _staffNametags)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
                }
            }
            _staffNametags.Clear();
            _staffAssignments.Clear();
            _usernameToStaffId.Clear();
            _discoveredStaffSlots.Clear();
            _cachedRideControllers.Clear();
            _cachedSeatAnimControllers.Clear();
            _cachedPeepInteractionControllers.Clear();
            _rideInstanceNameCache.Clear();
            _viewerCurrentRideId.Clear();
            _activeUserActions.Clear();
            _staffDiscoveredForScene = false;
            _lastHeaderCount = -1;

            _assignedUsernames.Clear();

            _availableViewers.Clear();
            _availablePriorityViewers.Clear();
            _availableStaffQueue.Clear();
            foreach (var kvp in _knownViewers)
            {
                ViewerInfo v = kvp.Value;
                v.AssignedStaffRoleTitle = null;
                v.ActiveActionBadge = null;
                v.ActiveSpeechBubble = null;
                EnqueueViewerByRole(v);
            }
        }

        private static void EnqueueViewerByRole(ViewerInfo v)
        {
            if (TwitchPlugin.ExcludeBroadcaster.Value && !TwitchPlugin.DebugMode.Value && v.Role == ViewerRole.Broadcaster)
            {
                return;
            }

            if (v.JobPreference == ViewerJobPreference.VisitorOnly)
            {
                if (v.Role != ViewerRole.Guest) _availablePriorityViewers.Enqueue(v);
                else _availableViewers.Enqueue(v);
                return;
            }

            if (v.IsStaffEligible || v.JobPreference != ViewerJobPreference.Auto)
            {
                _availableStaffQueue.Enqueue(v);
            }
            else if (v.Role != ViewerRole.Guest)
            {
                _availablePriorityViewers.Enqueue(v);
            }
            else
            {
                _availableViewers.Enqueue(v);
            }
        }

        private void RegisterViewer(ViewerInfo v)
        {
            if (TwitchPlugin.ExcludeBroadcaster.Value && !TwitchPlugin.DebugMode.Value && v.Role == ViewerRole.Broadcaster)
            {
                return;
            }

            if (!_knownViewers.TryGetValue(v.Username, out ViewerInfo existing))
            {
                _knownViewers[v.Username] = v;
                EnqueueViewerByRole(v);
                TwitchPlugin.Log?.LogInfo($"[Twitch] Neuer Zuschauer erfasst: {v.DisplayName ?? v.Username} (Rolle: {v.Role}, Sub: {v.SubMonths}M, Farbe: {v.GetEffectiveNameColorHex()})");
            }
            else
            {
                bool changed = existing.Role != v.Role
                    || existing.SubMonths != v.SubMonths
                    || existing.IsSub != v.IsSub
                    || existing.IsFounder != v.IsFounder
                    || existing.IsVerified != v.IsVerified
                    || existing.IsBot != v.IsBot
                    || (!string.IsNullOrEmpty(v.TwitchColorHex) && existing.TwitchColorHex != v.TwitchColorHex)
                    || (!string.IsNullOrEmpty(v.DisplayName) && existing.DisplayName != v.DisplayName);

                existing.LastSeen = DateTime.UtcNow;
                existing.Role = v.Role;
                existing.IsSub = v.IsSub;
                existing.IsFounder = v.IsFounder;
                existing.IsVip = v.IsVip;
                existing.IsMod = v.IsMod;
                existing.IsVerified = v.IsVerified;
                existing.IsBot = v.IsBot;
                if (v.SubMonths > 0) existing.SubMonths = v.SubMonths;
                if (v.SubTier > 1) existing.SubTier = v.SubTier;
                if (!string.IsNullOrEmpty(v.TwitchColorHex)) existing.TwitchColorHex = v.TwitchColorHex;
                if (!string.IsNullOrEmpty(v.DisplayName)) existing.DisplayName = v.DisplayName;

                if (changed)
                {
                    RefreshViewerNametag(existing);
                }
            }
        }

        private void RefreshViewerNametag(ViewerInfo v)
        {
            if (v == null) return;
            if (_usernameToStaffId.TryGetValue(v.Username, out int staffId))
            {
                if (_staffNametags.TryGetValue(staffId, out TMP_Text staffTag) && staffTag != null)
                {
                    CreateOrUpdateNametag(staffTag.transform.parent, staffId, v, true, 2.05f);
                }
            }
            else if (_usernameToPeepId.TryGetValue(v.Username, out int peepId))
            {
                if (_peepNametags.TryGetValue(peepId, out TMP_Text tagText) && tagText != null)
                {
                    CreateOrUpdateNametag(tagText.transform.parent, peepId, v, false, 2.15f);
                }
            }
        }

                private static string DetermineJobRoleFromHierarchy(Transform t, string fallbackRole)
        {
            if (t == null) return fallbackRole;

            Transform curr = t;
            int depth = 0;
            while (curr != null && depth < 6)
            {
                string n = (curr.name ?? "").ToLowerInvariant();

                // 1. Kasse
                if (n.Contains("cashier") || n.Contains("checkout") || n.Contains("kasse") || n.Contains("ticket"))
                {
                    return "KASSE";
                }

                // 2. Rekommandeur / Operator am Steuerpult
                if (n.Contains("operator") || n.Contains("rekommandeur") || n.Contains("control") || n.Contains("cab") || n.Contains("pult") || n.Contains("driver"))
                {
                    return "REKOMMANDEUR";
                }

                // 3. Chip-Sammler / Assistent (auch Pinball, MegaDance, Rotator, Group etc.)
                if (n.Contains("assistent") || n.Contains("assistant") || n.Contains("collector") || n.Contains("chip")
                    || n.Contains("gondola") || n.Contains("female") || n.Contains("male") || n.Contains("group"))
                {
                    return "CHIP-SAMMLER";
                }

                // 4. Stand / Bude
                if (n.Contains("store") || n.Contains("booth") || n.Contains("vendor") || n.Contains("shop"))
                {
                    return "STAND";
                }
                curr = curr.parent;
                depth++;
            }
            return fallbackRole;
        }

        private static string ExtractReadableRideOrBoothName(Transform t, string defaultRole)
        {
            // Gibt ausschliesslich die saubere Berufsbezeichnung zurueck (z.B. KASSE, CHIP-SAMMLER, STAND, REKOMMANDEUR)
            // Verhindert falsche Zusaetze wie POI, PINBALL, LOSBUDE etc.
            return DetermineJobRoleFromHierarchy(t, defaultRole);
        }

        private static bool IsGenericHierarchyName(string cleaned)
        {
            if (string.IsNullOrEmpty(cleaned)) return true;
            return cleaned.StartsWith("Employee", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Human", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Character", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Standing", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Seating", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Checkout", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("GameObject", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Controller", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Operator", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Rides", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Ride", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Booths", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Booth", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Stores", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Store", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Stalls", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Stall", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Shops", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Shop", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Props", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Environment", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Root", StringComparison.OrdinalIgnoreCase)
                || cleaned.StartsWith("Scene", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("GELDKASSETTE", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindNearbyBoothOrRideName(Transform staffTransform, string actualRole)
        {
            try
            {
                if (staffTransform == null) return null;

                // A) Prüfe Kinder und Geschwister (Siblings) des Eltern-Containers (z.B. "Store" -> hat oft ein Mesh-Kind wie "Popcorn_Stand", "Churros", "Losbude" etc.)
                Transform p = staffTransform.parent;
                for (int up = 0; up < 3 && p != null; up++)
                {
                    int childCount = p.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        Transform sibling = p.GetChild(i);
                        if (sibling == null || sibling == staffTransform) continue;
                        string cName = CleanUnityObjectName(sibling.name);
                        if (!string.IsNullOrEmpty(cName) && !IsGenericHierarchyName(cName))
                        {
                            return cName;
                        }
                    }

                    // Prüfe auch MeshRenderer / SkinnedMeshRenderer im selben Buden-Root
                    var renderers = p.GetComponentsInChildren<Renderer>(true);
                    if (renderers != null)
                    {
                        foreach (var r in renderers)
                        {
                            if (r == null || r.gameObject == null) continue;
                            if (r.transform.IsChildOf(staffTransform)) continue; // Nicht die Kleidung des Verkäufers selbst
                            string rObjName = CleanUnityObjectName(r.gameObject.name);
                            if (!string.IsNullOrEmpty(rObjName) && !IsGenericHierarchyName(rObjName)
                                && !rObjName.StartsWith("Cube", StringComparison.OrdinalIgnoreCase)
                                && !rObjName.StartsWith("Plane", StringComparison.OrdinalIgnoreCase)
                                && !rObjName.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase)
                                && !rObjName.StartsWith("Light", StringComparison.OrdinalIgnoreCase)
                                && !rObjName.StartsWith("Glass", StringComparison.OrdinalIgnoreCase)
                                && !rObjName.StartsWith("Window", StringComparison.OrdinalIgnoreCase))
                            {
                                return rObjName;
                            }
                        }
                    }
                    p = p.parent;
                }

                // B) Falls immer noch nichts gefunden (oder für REKOMMANDEUR • CONTROLLER): Suche das nächstgelegene Fahrgeschäft im Umkreis!
                var allRides = UnityEngine.Object.FindObjectsOfType<Ride_Controller>(true);
                if (allRides != null)
                {
                    float bestDist = actualRole == "STAND" ? 10f : 35f;
                    string nearestRide = null;
                    Vector3 pos = staffTransform.position;
                    foreach (var rc in allRides)
                    {
                        if (rc == null || rc.transform == null) continue;
                        float d = Vector3.Distance(pos, rc.transform.position);
                        if (d < bestDist)
                        {
                            string rName = CleanUnityObjectName(rc.gameObject.name);
                            if (IsGenericHierarchyName(rName) && rc.transform.parent != null)
                            {
                                rName = CleanUnityObjectName(rc.transform.parent.name);
                            }
                            if (!string.IsNullOrEmpty(rName) && !IsGenericHierarchyName(rName))
                            {
                                bestDist = d;
                                nearestRide = rName;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(nearestRide))
                    {
                        return actualRole == "STAND" ? $"BUDE BEI {nearestRide}" : nearestRide;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string TranslateFairgroundObjectName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string u = raw.Trim();
            string low = u.ToLowerInvariant();

            if (low.Contains("popcorn")) return "POPCORN-BUDE";
            if (low.Contains("cotton") || low.Contains("candy") || low.Contains("zuckerwatte") || low.Contains("sweets")) return "SÜSSWAREN & ZUCKERWATTE";
            if (low.Contains("churro")) return "CHURROS-STAND";
            if (low.Contains("crepe") || low.Contains("crêpe") || low.Contains("waffle") || low.Contains("waffel")) return "CRÊPES & WAFFELN";
            if (low.Contains("sausage") || low.Contains("bratwurst") || low.Contains("grill") || low.Contains("bbq") || low.Contains("imbiss") || low.Contains("burger") || low.Contains("hotdog") || low.Contains("fries") || low.Contains("pommes")) return "IMBISS & GRILL";
            if (low.Contains("drink") || low.Contains("slush") || low.Contains("beer") || low.Contains("bier") || low.Contains("soda") || low.Contains("cola") || low.Contains("beverage")) return "GETRÄNKE-STAND";
            if (low.Contains("ice") || low.Contains("eis")) return "EIS-STAND";
            if (low.Contains("almond") || low.Contains("mandel") || low.Contains("nut")) return "GEBRANNTE MANDELN";
            if (low.Contains("claw") || low.Contains("greifer") || low.Contains("crane")) return "GREIFER-AUTOMAT";
            if (low.Contains("duck") || low.Contains("enten") || low.Contains("fishing")) return "ENTENANGELN";
            if (low.Contains("can") || low.Contains("dosen") || low.Contains("throw") || low.Contains("toss")) return "DOSENWERFEN";
            if (low.Contains("shoot") || low.Contains("schiess") || low.Contains("schieß") || low.Contains("rifle") || low.Contains("target")) return "SCHIESSBUDE";
            if (low.Contains("lotter") || low.Contains("los") || low.Contains("tombola") || low.Contains("raffle")) return "LOSBUDE";
            if (low.Contains("dart") || low.Contains("balloon") || low.Contains("ballon")) return "BALLON-DARTS";
            if (low.Contains("wheel") && !low.Contains("ferris")) return "GLÜCKSRAD";
            if (low.Contains("ferris")) return "RIESENRAD";
            if (low.Contains("hallofmirrors") || low.Contains("mirror")) return "SPIEGELKABINETT";

            return u.ToUpperInvariant();
        }

        private static string CleanUnityObjectName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string s = rawName.Replace("(Clone)", "").Replace("_Prefab", "").Replace("Prefab", "");
            int parenIdx = s.IndexOf('(');
            if (parenIdx != -1) s = s.Substring(0, parenIdx);
            s = s.Replace('_', ' ').Trim();
            return s;
        }

        private void DiscoverRideStaffSlots()
        {
            try
            {
                HashSet<int> existingIds = new HashSet<int>();
                for (int i = _discoveredStaffSlots.Count - 1; i >= 0; i--)
                {
                    if (_discoveredStaffSlots[i].TargetTransform == null)
                    {
                        _discoveredStaffSlots.RemoveAt(i);
                    }
                    else
                    {
                        existingIds.Add(_discoveredStaffSlots[i].InstanceId);
                    }
                }

                var seatingEmps = UnityEngine.Object.FindObjectsOfType<Seating_Employee>(true);

                // ==========================================
                // 1. KASSIERER ("KASSE") ZUERST!
                // ==========================================
                if (seatingEmps != null)
                {
                    foreach (var sEmp in seatingEmps)
                    {
                        if (sEmp == null || sEmp.transform == null) continue;
                        string roleCheck = DetermineJobRoleFromHierarchy(sEmp.transform, "REKOMMANDEUR");
                        if (roleCheck != "KASSE") continue; // Nur echte Kassierer im 1. Schritt!

                        int id = sEmp.transform.GetInstanceID();
                        if (!existingIds.Contains(id))
                        {
                            existingIds.Add(id);
                            _discoveredStaffSlots.Add(new StaffSlot
                            {
                                InstanceId = id,
                                TargetTransform = sEmp.transform,
                                JobTitle = ExtractReadableRideOrBoothName(sEmp.transform, "KASSE"),
                                HeightOffset = 1.85f
                            });
                        }
                    }
                }

                var checkouts = UnityEngine.Object.FindObjectsOfType<Checkout>(true);
                if (checkouts != null)
                {
                    foreach (var chk in checkouts)
                    {
                        if (chk == null || chk.KI_Employee == null || chk.KI_Employee.transform == null) continue;
                        int id = chk.KI_Employee.transform.GetInstanceID();
                        if (!existingIds.Contains(id))
                        {
                            existingIds.Add(id);
                            _discoveredStaffSlots.Add(new StaffSlot
                            {
                                InstanceId = id,
                                TargetTransform = chk.KI_Employee.transform,
                                JobTitle = ExtractReadableRideOrBoothName(chk.transform, "KASSE"),
                                HeightOffset = 1.95f
                            });
                        }
                    }
                }

                // ==========================================
                // 2. FAHRCHIP-SAMMLER ("CHIP-SAMMLER") ALS ZWEITES!
                // ==========================================
                var employees = UnityEngine.Object.FindObjectsOfType<Employee>(true);
                if (employees != null)
                {
                    foreach (var emp in employees)
                    {
                        if (emp == null || emp.transform == null) continue;
                        int id = emp.transform.GetInstanceID();
                        if (!existingIds.Contains(id))
                        {
                            existingIds.Add(id);
                            _discoveredStaffSlots.Add(new StaffSlot
                            {
                                InstanceId = id,
                                TargetTransform = emp.transform,
                                JobTitle = ExtractReadableRideOrBoothName(emp.transform, "CHIP-SAMMLER"),
                                HeightOffset = 2.15f
                            });
                        }
                    }
                }

                // ==========================================
                // 3. SPIELBUDEN & ESSENSSTÄNDE ("STAND") ALS DRITTES!
                // ==========================================
                var storeEmps = UnityEngine.Object.FindObjectsOfType<Store_Employee>(true);
                if (storeEmps != null)
                {
                    foreach (var st in storeEmps)
                    {
                        if (st == null) continue;
                        Transform targetT = st.characterCustomization != null ? st.characterCustomization.transform : st.transform;
                        if (targetT == null) continue;
                        int id = targetT.GetInstanceID();
                        if (!existingIds.Contains(id))
                        {
                            existingIds.Add(id);
                            _discoveredStaffSlots.Add(new StaffSlot
                            {
                                InstanceId = id,
                                TargetTransform = targetT,
                                JobTitle = ExtractReadableRideOrBoothName(st.transform, "STAND"),
                                HeightOffset = 2.10f
                            });
                        }
                    }
                }

                // ==========================================
                // 4. REKOMMANDEURE / STEUERPULT ("REKOMMANDEUR") GANZ ZUM SCHLUSS!
                // (Falls du das Fahrgeschäft selbst übernimmst, wird derjenige automatisch frei und wechselt den Platz!)
                // ==========================================
                var kiOps = UnityEngine.Object.FindObjectsOfType<KI_Ride_Operator>(true);
                if (kiOps != null)
                {
                    foreach (var op in kiOps)
                    {
                        if (op == null) continue;
                        if (op.Standing_Employee != null && op.Standing_Employee.transform != null)
                        {
                            int id = op.Standing_Employee.transform.GetInstanceID();
                            if (!existingIds.Contains(id))
                            {
                                existingIds.Add(id);
                                _discoveredStaffSlots.Add(new StaffSlot
                                {
                                    InstanceId = id,
                                    TargetTransform = op.Standing_Employee.transform,
                                    JobTitle = ExtractReadableRideOrBoothName(op.Standing_Employee.transform, "REKOMMANDEUR"),
                                    HeightOffset = 2.10f
                                });
                            }
                        }
                    }
                }

                if (seatingEmps != null)
                {
                    foreach (var sEmp in seatingEmps)
                    {
                        if (sEmp == null || sEmp.transform == null) continue;
                        int id = sEmp.transform.GetInstanceID();
                        if (!existingIds.Contains(id))
                        {
                            existingIds.Add(id);
                            _discoveredStaffSlots.Add(new StaffSlot
                            {
                                InstanceId = id,
                                TargetTransform = sEmp.transform,
                                JobTitle = ExtractReadableRideOrBoothName(sEmp.transform, "CHIP-SAMMLER"),
                                HeightOffset = 1.85f
                            });
                        }
                    }
                }

                var rideCtrls = UnityEngine.Object.FindObjectsOfType<Ride_Controller>(true);
                _cachedRideControllers.Clear();
                _rideInstanceNameCache.Clear();
                if (rideCtrls != null)
                {
                    foreach (var rc in rideCtrls)
                    {
                        if (rc == null || rc.transform == null) continue;
                        _cachedRideControllers.Add(rc);
                        string rName = CleanUnityObjectName(rc.gameObject.name);
                        if (string.IsNullOrEmpty(rName)) rName = "FAHRGESCHÄFT";
                        _rideInstanceNameCache[rc.GetInstanceID()] = rName.ToUpperInvariant();

                        var anims = rc.GetComponentsInChildren<Animator>(true);
                        if (anims == null) continue;
                        foreach (var anim in anims)
                        {
                            if (anim == null || anim.transform == null) continue;
                            string anName = (anim.gameObject.name ?? "").ToLowerInvariant();
                            if (anName.Contains("operator") || anName.Contains("rekommandeur") || (anName.Contains("employee") && !anName.Contains("cashier") && !anName.Contains("assistent") && !anName.Contains("assistant")))
                            {
                                int id = anim.transform.GetInstanceID();
                                if (!existingIds.Contains(id))
                                {
                                    existingIds.Add(id);
                                    _discoveredStaffSlots.Add(new StaffSlot
                                    {
                                        InstanceId = id,
                                        TargetTransform = anim.transform,
                                        JobTitle = ExtractReadableRideOrBoothName(anim.transform, "CHIP-SAMMLER"),
                                        HeightOffset = 1.85f
                                    });
                                }
                            }
                        }
                    }
                }

                // Einmaliges Cachen von Seat_Peep_Animation_Controller und Peep_Interaction_Controller
                _cachedSeatAnimControllers.Clear();
                var animCtrls = UnityEngine.Object.FindObjectsOfType<Seat_Peep_Animation_Controller>(true);
                if (animCtrls != null)
                {
                    foreach (var ac in animCtrls)
                    {
                        if (ac != null) _cachedSeatAnimControllers.Add(ac);
                    }
                }

                _cachedPeepInteractionControllers.Clear();
                var picCtrls = UnityEngine.Object.FindObjectsOfType<Peep_Interaction_Controller>(true);
                if (picCtrls != null)
                {
                    foreach (var pic in picCtrls)
                    {
                        if (pic != null) _cachedPeepInteractionControllers.Add(pic);
                    }
                }
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning($"[Twitch] Fehler bei Staff-Suche: {ex.Message}");
            }
        }

        private void UpdateStaffAssignments()
        {
            if (!TwitchPlugin.AssignModsToStaff.Value) return;

            bool inMainMenu = !string.IsNullOrEmpty(_lastSceneName) && _lastSceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
            if (inMainMenu) return;

            // Suche die Mitarbeiter-Referenzen nur EINMALIG nach dem Laden der Kirmes (verhindert jegliches Ruckeln durch FindObjectsOfType!)
            if (!_staffDiscoveredForScene)
            {
                _staffDiscoveredForScene = true;
                DiscoverRideStaffSlots();
            }

            if (_discoveredStaffSlots.Count == 0) return;

            List<int> toRelease = null;
            foreach (var kvp in _staffAssignments)
            {
                int slotId = kvp.Key;
                bool stillActive = false;
                for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                {
                    if (_discoveredStaffSlots[i].InstanceId == slotId)
                    {
                        Transform t = _discoveredStaffSlots[i].TargetTransform;
                        if (t != null && t.gameObject != null && t.gameObject.activeInHierarchy)
                        {
                            stillActive = true;
                        }
                        break;
                    }
                }

                if (!stillActive)
                {
                    if (toRelease == null) toRelease = new List<int>();
                    toRelease.Add(slotId);
                }
            }

            if (toRelease != null)
            {
                for (int i = 0; i < toRelease.Count; i++)
                {
                    int slotId = toRelease[i];
                    if (_staffAssignments.TryGetValue(slotId, out ViewerInfo v))
                    {
                        v.AssignedStaffRoleTitle = null;
                        _assignedUsernames.Remove(v.Username);
                        _usernameToStaffId.Remove(v.Username);
                        EnqueueViewerByRole(v);
                        _staffAssignments.Remove(slotId);
                    }
                    if (_staffNametags.TryGetValue(slotId, out TMP_Text tm))
                    {
                        if (tm != null && tm.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(tm.gameObject);
                        }
                        _staffNametags.Remove(slotId);
                    }
                }
            }

            // 1. Moderatoren, VIPs und Founder bekommen IMMER zu 100% Vorrang an freien Mitarbeiter-Plätzen!
            for (int i = 0; i < _discoveredStaffSlots.Count; i++)
            {
                if (_availableStaffQueue.Count == 0) break;

                StaffSlot slot = _discoveredStaffSlots[i];
                if (slot.TargetTransform == null || !slot.TargetTransform.gameObject.activeInHierarchy) continue;
                if (_staffAssignments.ContainsKey(slot.InstanceId)) continue;

                ViewerInfo assignedStaff = null;
                while (_availableStaffQueue.Count > 0)
                {
                    ViewerInfo candidate = _availableStaffQueue.Dequeue();
                    if (_assignedUsernames.Contains(candidate.Username)) continue;
                    if (candidate.JobPreference == ViewerJobPreference.VisitorOnly)
                    {
                        if (candidate.Role != ViewerRole.Guest) _availablePriorityViewers.Enqueue(candidate);
                        else _availableViewers.Enqueue(candidate);
                        continue;
                    }
                    assignedStaff = candidate;
                    break;
                }

                if (assignedStaff != null)
                {
                    assignedStaff.AssignedStaffRoleTitle = slot.JobTitle;
                    _staffAssignments[slot.InstanceId] = assignedStaff;
                    _assignedUsernames.Add(assignedStaff.Username);
                    _usernameToStaffId[assignedStaff.Username] = slot.InstanceId;
                    CreateOrUpdateNametag(slot.TargetTransform, slot.InstanceId, assignedStaff, true, slot.HeightOffset);
                    ShowJoinNotification(assignedStaff, true, slot.JobTitle);
                    TwitchPlugin.Log?.LogInfo($"[Twitch] {assignedStaff.Role} '{assignedStaff.DisplayName ?? assignedStaff.Username}' arbeitet jetzt als [{slot.JobTitle}] an Fahrgeschäft/Bude ({slot.TargetTransform.name})!");
                }
            }

            // 2. Falls ein neuer Mod/VIP/Founder in der Warteschlange ist, aber alle Mitarbeiter-Plätze schon von normalen Zuschauern/Subs belegt sind -> tausche einen Nicht-Mod aus!
            while (_availableStaffQueue.Count > 0)
            {
                ViewerInfo waitingMod = null;
                while (_availableStaffQueue.Count > 0)
                {
                    ViewerInfo c = _availableStaffQueue.Dequeue();
                    if (_assignedUsernames.Contains(c.Username)) continue;
                    if (c.JobPreference == ViewerJobPreference.VisitorOnly)
                    {
                        if (c.Role != ViewerRole.Guest) _availablePriorityViewers.Enqueue(c);
                        else _availableViewers.Enqueue(c);
                        continue;
                    }
                    waitingMod = c;
                    break;
                }
                if (waitingMod == null) break;

                int replaceSlotId = -1;
                ViewerInfo replacedViewer = null;
                StaffSlot replaceSlot = null;

                for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                {
                    StaffSlot s = _discoveredStaffSlots[i];
                    if (_staffAssignments.TryGetValue(s.InstanceId, out ViewerInfo currentOccupant) && !currentOccupant.IsStaffEligible)
                    {
                        replaceSlotId = s.InstanceId;
                        replacedViewer = currentOccupant;
                        replaceSlot = s;
                        break;
                    }
                }

                if (replaceSlotId != -1 && replacedViewer != null && replaceSlot != null)
                {
                    replacedViewer.AssignedStaffRoleTitle = null;
                    _assignedUsernames.Remove(replacedViewer.Username);
                    _usernameToStaffId.Remove(replacedViewer.Username);
                    EnqueueViewerByRole(replacedViewer); // Geht zurück in die Besucher-Queue und läuft gleich als Peep weiter!

                    waitingMod.AssignedStaffRoleTitle = replaceSlot.JobTitle;
                    _staffAssignments[replaceSlotId] = waitingMod;
                    _assignedUsernames.Add(waitingMod.Username);
                    _usernameToStaffId[waitingMod.Username] = replaceSlotId;
                    CreateOrUpdateNametag(replaceSlot.TargetTransform, replaceSlotId, waitingMod, true, replaceSlot.HeightOffset);
                    ShowJoinNotification(waitingMod, true, replaceSlot.JobTitle);
                    TwitchPlugin.Log?.LogInfo($"[Twitch] {waitingMod.Role} '{waitingMod.DisplayName ?? waitingMod.Username}' hat den Mitarbeiter-Platz [{replaceSlot.JobTitle}] übernommen!");
                }
                else
                {
                    // Alle Mitarbeiter-Plätze sind bereits voll mit Mods/VIPs -> Mod darf als Besucher-Peep rumlaufen
                    _availablePriorityViewers.Enqueue(waitingMod);
                    break;
                }
            }

            // 3. Auch Subscriber und normale Zuschauer dürfen freie Kassen, Chip-Sammler, Spielbuden & Fahrgeschäfte besetzen, damit nach und nach ALLE Plätze belebt werden!
            // (Wir wechseln fair ab: Wenn schon mehr Besucher auf dem Platz laufen als Mitarbeiter arbeiten, wird der nächste freie Mitarbeiter-Slot besetzt!)
            if (TwitchPlugin.AllowAllViewersAsStaff.Value && (_availablePriorityViewers.Count > 0 || _availableViewers.Count > 0))
            {
                // Besetze pro Zyklus bis zu 1 weiteren freien Mitarbeiter-Slot mit einem Sub oder normalen Viewer, solange mindestens genauso viele Besucher laufen
                if (_peepAssignments.Count >= _staffAssignments.Count)
                {
                    for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                    {
                        StaffSlot slot = _discoveredStaffSlots[i];
                        if (slot.TargetTransform == null || !slot.TargetTransform.gameObject.activeInHierarchy) continue;
                        if (_staffAssignments.ContainsKey(slot.InstanceId)) continue;

                        ViewerInfo assignedViewerStaff = null;
                        int maxPriorityChecks = _availablePriorityViewers.Count;
                        for (int p = 0; p < maxPriorityChecks && _availablePriorityViewers.Count > 0; p++)
                        {
                            ViewerInfo candidate = _availablePriorityViewers.Dequeue();
                            if (_assignedUsernames.Contains(candidate.Username)) continue;
                            if (candidate.JobPreference == ViewerJobPreference.VisitorOnly)
                            {
                                // Dieser Zuschauer möchte ausdrücklich NUR als Besucher rumlaufen!
                                _availablePriorityViewers.Enqueue(candidate);
                                continue;
                            }
                            assignedViewerStaff = candidate;
                            break;
                        }

                        if (assignedViewerStaff == null)
                        {
                            int maxViewerChecks = _availableViewers.Count;
                            for (int p = 0; p < maxViewerChecks && _availableViewers.Count > 0; p++)
                            {
                                ViewerInfo candidate = _availableViewers.Dequeue();
                                if (_assignedUsernames.Contains(candidate.Username)) continue;
                                if (candidate.JobPreference == ViewerJobPreference.VisitorOnly)
                                {
                                    _availableViewers.Enqueue(candidate);
                                    continue;
                                }
                                assignedViewerStaff = candidate;
                                break;
                            }
                        }

                        if (assignedViewerStaff != null)
                        {
                            assignedViewerStaff.AssignedStaffRoleTitle = slot.JobTitle;
                            _staffAssignments[slot.InstanceId] = assignedViewerStaff;
                            _assignedUsernames.Add(assignedViewerStaff.Username);
                            _usernameToStaffId[assignedViewerStaff.Username] = slot.InstanceId;
                            CreateOrUpdateNametag(slot.TargetTransform, slot.InstanceId, assignedViewerStaff, true, slot.HeightOffset);
                            ShowJoinNotification(assignedViewerStaff, true, slot.JobTitle);
                            TwitchPlugin.Log?.LogInfo($"[Twitch] Zuschauer '{assignedViewerStaff.DisplayName ?? assignedViewerStaff.Username}' ({assignedViewerStaff.Role}) hilft jetzt aus als [{slot.JobTitle}] ({slot.TargetTransform.name})!");
                        }
                        break; // Maximal 1 Nicht-Mod pro Scan-Tick an Staff zuweisen, damit der Rest auch auf den Wegen als Besucher spawnt!
                    }
                }
            }
        }

        private void UpdateNametagsAndAssignments()
        {
            if (!TwitchPlugin.ShowNametags.Value) return;

            UpdateStaffAssignments();

            var peepsDict = Peep_Controller.Peeps;
            if (peepsDict == null) return;

            List<int> toRemove = null;
            foreach (var kvp in _peepAssignments)
            {
                if (!peepsDict.ContainsKey((ushort)kvp.Key))
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int id = toRemove[i];
                    if (_peepAssignments.TryGetValue(id, out ViewerInfo v))
                    {
                        v.AssignedStaffRoleTitle = null;
                        _assignedUsernames.Remove(v.Username);
                        _usernameToPeepId.Remove(v.Username);
                        EnqueueViewerByRole(v);
                        _peepAssignments.Remove(id);
                    }

                    if (_peepNametags.TryGetValue(id, out TMP_Text tm))
                    {
                        if (tm != null && tm.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(tm.gameObject);
                        }
                        _peepNametags.Remove(id);
                    }
                }
            }

            // Inaktivitaets-Pruefung: Wer laenger als X Minuten nichts im Chat geschrieben hat, gibt seinen Peep frei (sofern Wartende vorhanden oder Limit ueberschritten)
            float timeoutMin = TwitchPlugin.ViewerInactivityMinutes != null ? TwitchPlugin.ViewerInactivityMinutes.Value : 10f;
            int maxAllowedTags = TwitchPlugin.MaxVisibleNametags != null ? Mathf.Max(10, TwitchPlugin.MaxVisibleNametags.Value) : 60;
            int waitingCount = _availablePriorityViewers.Count + _availableViewers.Count + _availableStaffQueue.Count;

            if (timeoutMin > 0f && (waitingCount > 0 || _peepAssignments.Count > maxAllowedTags))
            {
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-timeoutMin);
                List<int> inactivePeepIds = null;

                foreach (var kvp in _peepAssignments)
                {
                    ViewerInfo pv = kvp.Value;
                    if (pv == null) continue;
                    // Broadcaster/DebugStreamer wird bei Chat-Timeout verschont
                    if (pv.Role == ViewerRole.Broadcaster) continue;

                    if (pv.LastSeen < cutoff)
                    {
                        if (inactivePeepIds == null) inactivePeepIds = new List<int>();
                        inactivePeepIds.Add(kvp.Key);
                        // Max 4 Freigaben pro Zyklus fuer sanfte Performance
                        if (inactivePeepIds.Count >= 4) break;
                    }
                }

                if (inactivePeepIds != null)
                {
                    for (int i = 0; i < inactivePeepIds.Count; i++)
                    {
                        int id = inactivePeepIds[i];
                        if (_peepAssignments.TryGetValue(id, out ViewerInfo iv))
                        {
                            TwitchPlugin.Log?.LogInfo($"[Twitch] Inaktivitaets-Timeout ({timeoutMin}m): '{iv.DisplayName ?? iv.Username}' gibt Gast-Peep #{id} fuer aktive Chatter frei.");
                            iv.AssignedStaffRoleTitle = null;
                            _assignedUsernames.Remove(iv.Username);
                            _usernameToPeepId.Remove(iv.Username);
                            EnqueueViewerByRole(iv);
                            _peepAssignments.Remove(id);
                        }

                        if (_peepNametags.TryGetValue(id, out TMP_Text tm))
                        {
                            if (tm != null && tm.gameObject != null)
                            {
                                UnityEngine.Object.Destroy(tm.gameObject);
                            }
                            _peepNametags.Remove(id);
                        }
                    }
                }
            }

            // Falls bereits das eingestellte Namensschilder-Limit erreicht ist, keine neuen Peep-Schilder spawnen!
            if (_peepAssignments.Count >= maxAllowedTags)
            {
                return;
            }

            if (_availablePriorityViewers.Count == 0 && _availableViewers.Count == 0 && _availableStaffQueue.Count == 0)
            {
                return;
            }

            int newNametagsSpawnedThisCycle = 0;
            foreach (var entry in peepsDict)
            {
                if (_peepAssignments.Count >= maxAllowedTags) break; // Schutzgrenze gegen Frame-Drops bei 2000 Peeps!
                if (_availablePriorityViewers.Count == 0 && _availableViewers.Count == 0 && _availableStaffQueue.Count == 0) break;
                if (newNametagsSpawnedThisCycle >= 3) break; // Frame-Budget: Max 3 neue Schilder pro Scan-Zyklus spawnen!

                Peep peep = entry.Value;
                if (peep == null || peep.gameObject == null) continue;

                int peepId = entry.Key;

                if (!_peepAssignments.ContainsKey(peepId))
                {
                    ViewerInfo assignedViewer = null;

                    while (_availablePriorityViewers.Count > 0)
                    {
                        ViewerInfo candidate = _availablePriorityViewers.Dequeue();
                        if (!_assignedUsernames.Contains(candidate.Username))
                        {
                            assignedViewer = candidate;
                            break;
                        }
                    }

                    if (assignedViewer == null && _availableStaffQueue.Count > 0)
                    {
                        while (_availableStaffQueue.Count > 0)
                        {
                            ViewerInfo candidate = _availableStaffQueue.Dequeue();
                            if (!_assignedUsernames.Contains(candidate.Username))
                            {
                                assignedViewer = candidate;
                                break;
                            }
                        }
                    }

                    if (assignedViewer == null && _availableViewers.Count > 0)
                    {
                        while (_availableViewers.Count > 0)
                        {
                            ViewerInfo candidate = _availableViewers.Dequeue();
                            if (!_assignedUsernames.Contains(candidate.Username))
                            {
                                assignedViewer = candidate;
                                break;
                            }
                        }
                    }

                    if (assignedViewer != null)
                    {
                        assignedViewer.AssignedStaffRoleTitle = null;
                        _peepAssignments[peepId] = assignedViewer;
                        _assignedUsernames.Add(assignedViewer.Username);
                        _usernameToPeepId[assignedViewer.Username] = peepId;
                        CreateOrUpdateNametag(peep.transform, peepId, assignedViewer, false, 2.15f);
                        newNametagsSpawnedThisCycle++;
                        ShowJoinNotification(assignedViewer, false, null);
                        TwitchPlugin.Log?.LogInfo($"[Twitch] Zuschauer '{assignedViewer.DisplayName ?? assignedViewer.Username}' ({assignedViewer.Role}) erfolgreich Gast-Peep #{peepId} zugewiesen!");
                    }
                }
            }
        }

        private void CreateOrUpdateNametag(Transform parent, int id, ViewerInfo viewer, bool isStaff, float heightOffset)
        {
            var targetDict = isStaff ? _staffNametags : _peepNametags;

            if (targetDict.TryGetValue(id, out TMP_Text existing) && existing != null)
            {
                existing.text = viewer.GetTag();
                existing.color = Color.white;
                AttachOrUpdateBadgeIcons(existing.transform, viewer);
                return;
            }

            try
            {
                if (_nativeNametagTemplate == null)
                {
                    GameObject netCharPrefab = Resources.Load<GameObject>("Network_Character/Human_Male_Network");
                    if (netCharPrefab == null)
                    {
                        netCharPrefab = Resources.Load<GameObject>("Network_Character/Human_Female_Network");
                    }

                    if (netCharPrefab != null)
                    {
                        Network_Player np = netCharPrefab.GetComponent<Network_Player>();
                        if (np != null && np.Playe_Name_Text != null)
                        {
                            _nativeNametagTemplate = np.Playe_Name_Text.gameObject;
                            TwitchPlugin.Log?.LogInfo("[Twitch] Originales Multiplayer-Namensschild erfolgreich geladen (Network_Player.Playe_Name_Text)!");
                        }
                    }
                }

                TMP_Text tagText = null;

                if (_nativeNametagTemplate != null)
                {
                    GameObject clonedTag = UnityEngine.Object.Instantiate(_nativeNametagTemplate, parent, false);
                    clonedTag.name = isStaff ? "Multiplayer_Nametag_Twitch_Staff" : "Multiplayer_Nametag_Twitch";
                    clonedTag.transform.localPosition = new Vector3(0, heightOffset, 0);
                    clonedTag.transform.localRotation = Quaternion.identity;

                    for (int i = 0; i < clonedTag.transform.childCount; i++)
                    {
                        GameObject child = clonedTag.transform.GetChild(i).gameObject;
                        child.SetActive(false);
                    }

                    clonedTag.SetActive(true);

                    tagText = clonedTag.GetComponent<TMP_Text>();
                    if (tagText != null)
                    {
                        tagText.overflowMode = TextOverflowModes.Overflow;
                        tagText.enableWordWrapping = false;
                        tagText.richText = true;
                        try
                        {
                            tagText.outlineWidth = 0.28f;
                            tagText.outlineColor = new Color32(0, 0, 0, 255);
                        }
                        catch { }
                    }

                    if (clonedTag.GetComponent<Look_To_Cam>() == null)
                    {
                        clonedTag.AddComponent<Look_To_Cam>();
                    }
                }

                if (tagText == null)
                {
                    GameObject textGo = new GameObject("TwitchNametagTMP");
                    textGo.transform.SetParent(parent, false);
                    textGo.transform.localPosition = new Vector3(0, heightOffset, 0);
                    textGo.transform.localScale = Vector3.one * 0.04f;

                    TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
                    tmp.font = TMP_Settings.defaultFontAsset;
                    tmp.fontSize = 24f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = false;
                    tmp.richText = true;
                    tmp.isOverlay = true;
                    tmp.outlineWidth = 0.25f;
                    tmp.outlineColor = new Color32(0, 0, 0, 255);

                    var mr = textGo.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sortingOrder = 5000;

                    if (textGo.GetComponent<Look_To_Cam>() == null)
                    {
                        textGo.AddComponent<Look_To_Cam>();
                    }

                    tagText = tmp;
                }

                tagText.color = Color.white;
                tagText.text = viewer.GetTag();

                AttachOrUpdateBadgeIcons(tagText.transform, viewer);

                targetDict[id] = tagText;
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogError($"[Twitch] Fehler beim Erstellen des 3D-Namensschilds: {ex.Message}");
            }
        }

        private void AttachOrUpdateBadgeIcons(Transform tagTransform, ViewerInfo viewer)
        {
            if (tagTransform == null) return;

            Transform existingContainer = tagTransform.Find("TwitchBadgeContainer");
            if (!TwitchPlugin.ShowBadges.Value)
            {
                if (existingContainer != null) existingContainer.gameObject.SetActive(false);
                return;
            }

            Sprite primarySpr = GetPrimaryBadgeSprite(viewer);
            Sprite secondarySpr = GetSecondaryBadgeSprite(viewer);

            if (primarySpr == null)
            {
                if (existingContainer != null) existingContainer.gameObject.SetActive(false);
                return;
            }

            GameObject containerGo;
            float parentScaleY = Math.Max(0.001f, tagTransform.lossyScale.y);
            // Positioniere das Twitch-Badge-Logo direkt ueber der obersten Textzeile (beruecksichtigt Zusatzzeilen wie Job-Titel & Sprechblase)
            int extraLines = 0;
            if (!string.IsNullOrEmpty(viewer.AssignedStaffRoleTitle)) extraLines++;
            if (!string.IsNullOrEmpty(viewer.ActiveActionBadge)) extraLines++;
            if (!string.IsNullOrEmpty(viewer.ActiveSpeechBubble)) extraLines++;
            float desiredWorldHeightOffset = 0.095f + (extraLines * 0.048f);
            float desiredWorldIconScale = 0.13f;

            if (existingContainer == null)
            {
                containerGo = new GameObject("TwitchBadgeContainer");
                containerGo.transform.SetParent(tagTransform, false);
                containerGo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                containerGo = existingContainer.gameObject;
                containerGo.SetActive(true);
            }

            containerGo.transform.localPosition = new Vector3(0f, desiredWorldHeightOffset / parentScaleY, 0f);
            float relScale = desiredWorldIconScale / parentScaleY;
            containerGo.transform.localScale = new Vector3(relScale, relScale, relScale);

            Transform pTrans = containerGo.transform.Find("PrimaryBadge");
            SpriteRenderer pSr;
            if (pTrans == null)
            {
                GameObject pGo = new GameObject("PrimaryBadge");
                pGo.transform.SetParent(containerGo.transform, false);
                pSr = pGo.AddComponent<SpriteRenderer>();
                pSr.sortingOrder = 5005;
            }
            else
            {
                pSr = pTrans.GetComponent<SpriteRenderer>();
            }
            pSr.sprite = primarySpr;

            Transform sTrans = containerGo.transform.Find("SecondaryBadge");
            if (secondarySpr != null)
            {
                SpriteRenderer sSr;
                if (sTrans == null)
                {
                    GameObject sGo = new GameObject("SecondaryBadge");
                    sGo.transform.SetParent(containerGo.transform, false);
                    sSr = sGo.AddComponent<SpriteRenderer>();
                    sSr.sortingOrder = 5005;
                }
                else
                {
                    sTrans.gameObject.SetActive(true);
                    sSr = sTrans.GetComponent<SpriteRenderer>();
                }
                sSr.sprite = secondarySpr;

                pSr.transform.localPosition = new Vector3(-0.38f, 0f, 0f);
                sSr.transform.localPosition = new Vector3(0.38f, 0f, 0f);
            }
            else
            {
                if (sTrans != null) sTrans.gameObject.SetActive(false);
                pSr.transform.localPosition = Vector3.zero;
            }
        }

        // ==========================================
        // TOP STREAM HEADER HUD OVERLAY & JOIN FEED
        // ==========================================
        private void ShowJoinNotification(ViewerInfo viewer, bool isStaff, string jobTitle)
        {
            if (viewer == null) return;

            string shownName = !string.IsNullOrEmpty(viewer.DisplayName) ? viewer.DisplayName : viewer.Username;
            string nameColor = viewer.GetEffectiveNameColorHex();
            string roleBadge = "";

            switch (viewer.Role)
            {
                case ViewerRole.Broadcaster:
                    roleBadge = " <color=#FF2A2A><b>[STREAMER]</b></color>";
                    break;
                case ViewerRole.Moderator:
                    roleBadge = " <color=#00E676><b>[MOD]</b></color>";
                    break;
                case ViewerRole.VIP:
                    roleBadge = " <color=#FF1493><b>[VIP]</b></color>";
                    break;
                case ViewerRole.Founder:
                    roleBadge = viewer.SubMonths > 0
                        ? $" <color=#C962FF><b>[FOUNDER • {viewer.SubMonths}M]</b></color>"
                        : " <color=#C962FF><b>[FOUNDER]</b></color>";
                    break;
                case ViewerRole.Subscriber:
                    roleBadge = viewer.SubMonths > 0
                        ? $" <color=#FFB400><b>[SUB • {viewer.SubMonths}M]</b></color>"
                        : " <color=#FFB400><b>[SUB]</b></color>";
                    break;
            }

            if ((viewer.Role == ViewerRole.Moderator || viewer.Role == ViewerRole.VIP) && (viewer.IsSub || viewer.SubMonths > 0))
            {
                roleBadge += viewer.SubMonths > 0
                    ? $" <color=#FFB400><b>[SUB • {viewer.SubMonths}M]</b></color>"
                    : " <color=#FFB400><b>[SUB]</b></color>";
            }

            if (viewer.IsVerified)
            {
                roleBadge += " <color=#BF94FF><b>[VERIFIZIERT]</b></color>";
            }

            if (viewer.IsBot)
            {
                roleBadge += " <color=#80DEEA><b>[BOT]</b></color>";
            }

            if (isStaff && !string.IsNullOrEmpty(jobTitle))
            {
                _latestJoinFeedText = $"<color=#00E5FF><b>[NEU IM TEAM]</b></color> <color={nameColor}><b>{shownName}</b></color>{roleBadge} <color=#E0E0E0>arbeitet jetzt an:</color> <color=#00E5FF><b>[{jobTitle}]</b></color>";
            }
            else
            {
                _latestJoinFeedText = $"<color=#00E676><b>[NEU AUF DEM PLATZ]</b></color> <color={nameColor}><b>{shownName}</b></color>{roleBadge} <color=#E0E0E0>ist gerade auf der Kirmes angekommen!</color>";
            }

            _joinFeedExpireTime = Time.time + 15f;
            UpdateTopHeaderOverlay();
        }

        private void UpdateTopHeaderOverlay()
        {
            try
            {
                bool inMainMenu = !string.IsNullOrEmpty(_lastSceneName) && _lastSceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!TwitchPlugin.ShowTopHeader.Value || inMainMenu)
                {
                    if (_headerCanvasGo != null && _headerCanvasGo.activeSelf)
                    {
                        _headerCanvasGo.SetActive(false);
                    }
                    return;
                }

                if (_headerCanvasGo == null)
                {
                    CreateTopHeaderOverlay();
                }

                if (_headerCanvasGo != null && !_headerCanvasGo.activeSelf)
                {
                    _headerCanvasGo.SetActive(true);
                }

                if (_headerTextComp != null)
                {
                    int activePeepsCount = _peepAssignments.Count;
                    int activeStaffCount = _staffAssignments.Count;
                    int totalActive = activePeepsCount + activeStaffCount;
                    string chan = _vodDemoActive ? "DEMO: freizeitgestoert (VOD)" : (!string.IsNullOrEmpty(_currentChannel) ? _currentChannel : TwitchPlugin.ChannelName.Value);

                    if (totalActive != _lastHeaderCount || chan != _lastHeaderChannel)
                    {
                        _lastHeaderCount = totalActive;
                        _lastHeaderChannel = chan;
                        _headerTextComp.text =
                            $"<color=#A970FF><b>TWITCH LIVE-KIRMES (#{chan})</b></color>  <color=#BBBBBB>|</color>  " +
                            $"<color=#00E676><b>{totalActive}</b></color> <color=#E0E0E0>Zuschauer</color>  <color=#BBBBBB>|</color>  " +
                            $"<color=#FFD700><b>!jubel</b></color> • <color=#00E5FF><b>!klatschen</b></color> • <color=#FF4081><b>!zugabe</b></color> • <color=#80DEEA><b>!kasse</b></color> • <color=#00E676><b>!mitfahren [Name]</b></color>";
                    }
                }

                if (_joinBannerBorderGo != null && _joinBannerTextComp != null)
                {
                    bool hasActiveJoinMsg = !string.IsNullOrEmpty(_latestJoinFeedText) && Time.time < _joinFeedExpireTime;
                    if (_joinBannerBorderGo.activeSelf != hasActiveJoinMsg)
                    {
                        _joinBannerBorderGo.SetActive(hasActiveJoinMsg);
                    }

                    if (hasActiveJoinMsg && _lastRenderedJoinFeedText != _latestJoinFeedText)
                    {
                        _lastRenderedJoinFeedText = _latestJoinFeedText;
                        _joinBannerTextComp.text = _latestJoinFeedText;
                    }
                }

                if (_announceBannerBorderGo != null && _announceBannerTextComp != null)
                {
                    bool hasActiveAnnounce = !string.IsNullOrEmpty(_latestAnnounceText) && Time.time < _announceExpireTime;
                    if (_announceBannerBorderGo.activeSelf != hasActiveAnnounce)
                    {
                        _announceBannerBorderGo.SetActive(hasActiveAnnounce);
                    }

                    if (hasActiveAnnounce)
                    {
                        // Positioniere die Durchsage automatisch direkt unter dem Join-Banner (oder auf -54f, falls gerade kein Join-Banner aktiv ist)
                        bool joinVisible = _joinBannerBorderGo != null && _joinBannerBorderGo.activeSelf;
                        RectTransform annRt = _announceBannerBorderGo.GetComponent<RectTransform>();
                        if (annRt != null)
                        {
                            float targetY = joinVisible ? -88f : -54f;
                            if (Math.Abs(annRt.anchoredPosition.y - targetY) > 0.5f)
                            {
                                annRt.anchoredPosition = new Vector2(0f, targetY);
                            }
                        }

                        if (_lastRenderedAnnounceText != _latestAnnounceText)
                        {
                            _lastRenderedAnnounceText = _latestAnnounceText;
                            _announceBannerTextComp.text = _latestAnnounceText;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning($"[Twitch] Fehler beim Header-Update: {ex.Message}");
            }
        }

        private void CreateTopHeaderOverlay()
        {
            try
            {
                _headerCanvasGo = new GameObject("FairgroundTwitchHeaderCanvas");
                UnityEngine.Object.DontDestroyOnLoad(_headerCanvasGo);
                _headerCanvasGo.hideFlags = HideFlags.HideAndDontSave;

                Canvas canvas = _headerCanvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;

                CanvasScaler scaler = _headerCanvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                // Violette Akzentlinie unten am Banner (Border-Effekt)
                GameObject borderGo = new GameObject("HeaderBorder");
                borderGo.transform.SetParent(_headerCanvasGo.transform, false);
                Image borderImg = borderGo.AddComponent<Image>();
                borderImg.color = new Color(0.57f, 0.27f, 1.0f, 0.90f); // Twitch-Lila (#9146FF)
                RectTransform borderRt = borderGo.GetComponent<RectTransform>();
                borderRt.anchorMin = new Vector2(0.5f, 1.0f);
                borderRt.anchorMax = new Vector2(0.5f, 1.0f);
                borderRt.pivot = new Vector2(0.5f, 1.0f);
                borderRt.anchoredPosition = new Vector2(0f, -8f);
                borderRt.sizeDelta = new Vector2(1024f, 44f);

                // Dunkler, leicht transparenter Hintergrund-Kasten
                GameObject bgGo = new GameObject("HeaderBg");
                bgGo.transform.SetParent(borderGo.transform, false);
                Image bgImg = bgGo.AddComponent<Image>();
                bgImg.color = new Color(0.07f, 0.07f, 0.10f, 0.92f); // Edles Fast-Schwarz
                RectTransform bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = new Vector2(2f, 3f); // 3px lila Unterstrich, 2px Seitenrand
                bgRt.offsetMax = new Vector2(-2f, -2f);

                // Twitch-Icon links im Banner
                if (!_badgesLoaded) LoadBadgeSprites();
                if (_badgeSprites.TryGetValue("twitch", out Sprite twSprite) && twSprite != null)
                {
                    GameObject iconGo = new GameObject("TwitchHeaderIcon");
                    iconGo.transform.SetParent(bgGo.transform, false);
                    Image iconImg = iconGo.AddComponent<Image>();
                    iconImg.sprite = twSprite;
                    iconImg.preserveAspect = true;

                    RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0f, 0.5f);
                    iconRt.anchorMax = new Vector2(0f, 0.5f);
                    iconRt.pivot = new Vector2(0f, 0.5f);
                    iconRt.anchoredPosition = new Vector2(14f, 0f);
                    iconRt.sizeDelta = new Vector2(28f, 28f);
                }

                // Text im Header
                GameObject textGo = new GameObject("HeaderText");
                textGo.transform.SetParent(bgGo.transform, false);
                _headerTextComp = textGo.AddComponent<Text>();
                _headerTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _headerTextComp.fontSize = 18;
                _headerTextComp.supportRichText = true;
                _headerTextComp.alignment = TextAnchor.MiddleCenter;
                _headerTextComp.color = Color.white;
                _headerTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
                _headerTextComp.verticalOverflow = VerticalWrapMode.Overflow;

                Outline outline = textGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(46f, 0f);
                textRt.offsetMax = new Vector2(-12f, 0f);

                // ==========================================
                // SUB-BANNER DIREKT UNTER "ZUSCHAUER AUF DEM PLATZ" (JOIN-BENACHRICHTIGUNG)
                // ==========================================
                _joinBannerBorderGo = new GameObject("JoinFeedBorder");
                _joinBannerBorderGo.transform.SetParent(_headerCanvasGo.transform, false);
                Image joinBorderImg = _joinBannerBorderGo.AddComponent<Image>();
                joinBorderImg.color = new Color(0.0f, 0.90f, 0.46f, 0.85f); // Smaragdgrün / Twitch-Akzent
                RectTransform joinBorderRt = _joinBannerBorderGo.GetComponent<RectTransform>();
                joinBorderRt.anchorMin = new Vector2(0.5f, 1.0f);
                joinBorderRt.anchorMax = new Vector2(0.5f, 1.0f);
                joinBorderRt.pivot = new Vector2(0.5f, 1.0f);
                joinBorderRt.anchoredPosition = new Vector2(0f, -54f); // Direkt unter dem 44px Hauptbanner!
                joinBorderRt.sizeDelta = new Vector2(680f, 30f);

                GameObject joinBgGo = new GameObject("JoinFeedBg");
                joinBgGo.transform.SetParent(_joinBannerBorderGo.transform, false);
                Image joinBgImg = joinBgGo.AddComponent<Image>();
                joinBgImg.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);
                RectTransform joinBgRt = joinBgGo.GetComponent<RectTransform>();
                joinBgRt.anchorMin = Vector2.zero;
                joinBgRt.anchorMax = Vector2.one;
                joinBgRt.offsetMin = new Vector2(2f, 2f);
                joinBgRt.offsetMax = new Vector2(-2f, -1f);

                GameObject joinTextGo = new GameObject("JoinFeedText");
                joinTextGo.transform.SetParent(joinBgGo.transform, false);
                _joinBannerTextComp = joinTextGo.AddComponent<Text>();
                _joinBannerTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _joinBannerTextComp.fontSize = 15;
                _joinBannerTextComp.supportRichText = true;
                _joinBannerTextComp.alignment = TextAnchor.MiddleCenter;
                _joinBannerTextComp.color = Color.white;
                _joinBannerTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
                _joinBannerTextComp.verticalOverflow = VerticalWrapMode.Overflow;

                Outline joinOutline = joinTextGo.AddComponent<Outline>();
                joinOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                joinOutline.effectDistance = new Vector2(1.0f, -1.0f);

                RectTransform joinTextRt = joinTextGo.GetComponent<RectTransform>();
                joinTextRt.anchorMin = Vector2.zero;
                joinTextRt.anchorMax = Vector2.one;
                joinTextRt.offsetMin = new Vector2(10f, 0f);
                joinTextRt.offsetMax = new Vector2(-10f, 0f);

                _joinBannerBorderGo.SetActive(false);

                // ==========================================
                // 3. EIGENES DURCHSAGE-BANNER UNTER DEM JOIN-FEED (GOLD-AKZENT, NICHT ÜBERSCHREIBBAR!)
                // ==========================================
                _announceBannerBorderGo = new GameObject("AnnounceFeedBorder");
                _announceBannerBorderGo.transform.SetParent(_headerCanvasGo.transform, false);
                Image annBorderImg = _announceBannerBorderGo.AddComponent<Image>();
                annBorderImg.color = new Color(1.0f, 0.72f, 0.0f, 0.92f); // Leuchtendes Gold (#FFB800)
                RectTransform annBorderRt = _announceBannerBorderGo.GetComponent<RectTransform>();
                annBorderRt.anchorMin = new Vector2(0.5f, 1.0f);
                annBorderRt.anchorMax = new Vector2(0.5f, 1.0f);
                annBorderRt.pivot = new Vector2(0.5f, 1.0f);
                annBorderRt.anchoredPosition = new Vector2(0f, -88f); // Direkt unter dem Join-Banner!
                annBorderRt.sizeDelta = new Vector2(820f, 32f);

                GameObject annBgGo = new GameObject("AnnounceFeedBg");
                annBgGo.transform.SetParent(_announceBannerBorderGo.transform, false);
                Image annBgImg = annBgGo.AddComponent<Image>();
                annBgImg.color = new Color(0.10f, 0.08f, 0.04f, 0.95f);
                RectTransform annBgRt = annBgGo.GetComponent<RectTransform>();
                annBgRt.anchorMin = Vector2.zero;
                annBgRt.anchorMax = Vector2.one;
                annBgRt.offsetMin = new Vector2(2f, 2f);
                annBgRt.offsetMax = new Vector2(-2f, -1f);

                GameObject annTextGo = new GameObject("AnnounceFeedText");
                annTextGo.transform.SetParent(annBgGo.transform, false);
                _announceBannerTextComp = annTextGo.AddComponent<Text>();
                _announceBannerTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _announceBannerTextComp.fontSize = 15;
                _announceBannerTextComp.supportRichText = true;
                _announceBannerTextComp.alignment = TextAnchor.MiddleCenter;
                _announceBannerTextComp.color = Color.white;
                _announceBannerTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
                _announceBannerTextComp.verticalOverflow = VerticalWrapMode.Overflow;

                Outline annOutline = annTextGo.AddComponent<Outline>();
                annOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
                annOutline.effectDistance = new Vector2(1.1f, -1.1f);

                RectTransform annTextRt = annTextGo.GetComponent<RectTransform>();
                annTextRt.anchorMin = Vector2.zero;
                annTextRt.anchorMax = Vector2.one;
                annTextRt.offsetMin = new Vector2(12f, 0f);
                annTextRt.offsetMax = new Vector2(-12f, 0f);

                _announceBannerBorderGo.SetActive(false);

                // ==========================================
                // 4. DURCH-WÄNDE-RADAR & ENTFERNUNGS-MARKER FÜR GESUCHTEN ZUSCHAUER
                // ==========================================
                _radarMarkerBorderGo = new GameObject("RadarMarkerBorder");
                _radarMarkerBorderGo.transform.SetParent(_headerCanvasGo.transform, false);
                Image radarBorderImg = _radarMarkerBorderGo.AddComponent<Image>();
                radarBorderImg.color = new Color(0.0f, 0.90f, 1.0f, 0.96f); // Leuchtendes Cyan (#00E5FF)
                _radarMarkerRt = _radarMarkerBorderGo.GetComponent<RectTransform>();
                _radarMarkerRt.anchorMin = new Vector2(0f, 0f);
                _radarMarkerRt.anchorMax = new Vector2(0f, 0f);
                _radarMarkerRt.pivot = new Vector2(0.5f, 0.5f);
                _radarMarkerRt.sizeDelta = new Vector2(280f, 52f);

                GameObject radarBgGo = new GameObject("RadarMarkerBg");
                radarBgGo.transform.SetParent(_radarMarkerBorderGo.transform, false);
                Image radarBgImg = radarBgGo.AddComponent<Image>();
                radarBgImg.color = new Color(0.04f, 0.08f, 0.14f, 0.94f);
                RectTransform radarBgRt = radarBgGo.GetComponent<RectTransform>();
                radarBgRt.anchorMin = Vector2.zero;
                radarBgRt.anchorMax = Vector2.one;
                radarBgRt.offsetMin = new Vector2(2f, 2f);
                radarBgRt.offsetMax = new Vector2(-2f, -2f);

                GameObject radarTextGo = new GameObject("RadarMarkerText");
                radarTextGo.transform.SetParent(radarBgGo.transform, false);
                _radarMarkerTextComp = radarTextGo.AddComponent<Text>();
                _radarMarkerTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _radarMarkerTextComp.fontSize = 14;
                _radarMarkerTextComp.supportRichText = true;
                _radarMarkerTextComp.alignment = TextAnchor.MiddleCenter;
                _radarMarkerTextComp.color = Color.white;
                _radarMarkerTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
                _radarMarkerTextComp.verticalOverflow = VerticalWrapMode.Overflow;

                Outline radarOutline = radarTextGo.AddComponent<Outline>();
                radarOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                radarOutline.effectDistance = new Vector2(1.1f, -1.1f);

                RectTransform radarTextRt = radarTextGo.GetComponent<RectTransform>();
                radarTextRt.anchorMin = Vector2.zero;
                radarTextRt.anchorMax = Vector2.one;
                radarTextRt.offsetMin = new Vector2(6f, 0f);
                radarTextRt.offsetMax = new Vector2(-6f, 0f);

                _radarMarkerBorderGo.SetActive(false);
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning($"[Twitch] Fehler beim Erstellen des Top-Headers: {ex.Message}");
            }
        }

        private static void LoadVodDemoData()
        {
            if (_vodDemoItems.Count > 0) return;
            try
            {
                string jsonPath = Path.Combine(Paths.PluginPath, "FairgroundTwitch", "vod_demo_chat.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Paths.PluginPath, "vod_demo_chat.json");
                }
                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                string[] entries = json.Split(new string[] { "}," }, StringSplitOptions.RemoveEmptyEntries);
                float firstOffset = -1f;

                foreach (string entry in entries)
                {
                    if (!entry.Contains("\"user\"")) continue;
                    VodDemoItem item = new VodDemoItem();
                    item.User = ExtractJsonStringField(entry, "user");
                    item.Name = ExtractJsonStringField(entry, "name");
                    item.Msg = ExtractJsonStringField(entry, "msg");
                    item.Color = ExtractJsonStringField(entry, "color");
                    item.IsSub = entry.Contains("\"isSub\": true");
                    item.IsMod = entry.Contains("\"isMod\": true");
                    item.IsVip = entry.Contains("\"isVip\": true");
                    item.IsBroadcaster = entry.Contains("\"isBroadcaster\": true");

                    string offsetStr = ExtractJsonNumericField(entry, "offset");
                    if (float.TryParse(offsetStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float rawSec))
                    {
                        if (firstOffset < 0f) firstOffset = rawSec;
                        item.Offset = Mathf.Max(0.5f, (rawSec - firstOffset) * 0.45f);
                    }
                    if (!string.IsNullOrEmpty(item.User))
                    {
                        _vodDemoItems.Add(item);
                    }
                }
                _vodDemoItems.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                TwitchPlugin.Log?.LogInfo($"[TwitchDemo] {_vodDemoItems.Count} VOD-Chat Nachrichten fuer Freizeitgestoert-Demo geladen!");
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning($"[TwitchDemo] Fehler beim Laden: {ex.Message}");
            }
        }

        private static string ExtractJsonStringField(string json, string key)
        {
            string needle = "\"" + key + "\": \"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx == -1) return "";
            int start = idx + needle.Length;
            int end = json.IndexOf('"', start);
            return end == -1 ? "" : json.Substring(start, end - start);
        }

        private static string ExtractJsonNumericField(string json, string key)
        {
            string needle = "\"" + key + "\": ";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx == -1) return "0";
            int start = idx + needle.Length;
            int comma = json.IndexOfAny(new char[] { ',', '\n', '\r' }, start);
            return comma == -1 ? json.Substring(start).Trim() : json.Substring(start, comma - start).Trim();
        }

        private void UpdateVodDemoReplay()
        {
            if (!_vodDemoActive) return;
            if (_vodDemoItems.Count == 0) LoadVodDemoData();
            if (_vodDemoItems.Count == 0) return;

            _vodDemoTimer += Time.deltaTime;

            while (_vodDemoIdx < _vodDemoItems.Count)
            {
                VodDemoItem item = _vodDemoItems[_vodDemoIdx];
                if (item.Offset > _vodDemoTimer) break;

                _vodDemoIdx++;

                ViewerRole role = ViewerRole.Guest;
                if (item.IsBroadcaster) role = ViewerRole.Broadcaster;
                else if (item.IsMod) role = ViewerRole.Moderator;
                else if (item.IsVip) role = ViewerRole.VIP;
                else if (item.IsSub) role = ViewerRole.Subscriber;

                ViewerInfo v = new ViewerInfo
                {
                    Username = item.User,
                    DisplayName = item.Name,
                    TwitchColorHex = item.Color,
                    Role = role,
                    IsSub = item.IsSub,
                    IsMod = item.IsMod,
                    IsVip = item.IsVip,
                    IsVerified = false,
                    IsBot = false,
                    SubMonths = item.IsSub ? 12 : 0,
                    SubTier = 1,
                    LastSeen = DateTime.UtcNow
                };

                _newViewersQueue.Enqueue(v);

                if (!string.IsNullOrEmpty(item.Msg))
                {
                    _commandQueue.Enqueue(new UserChatCommand
                    {
                        Username = item.User,
                        CommandText = item.Msg,
                        IsCommand = item.Msg.StartsWith("!")
                    });
                }
            }

            // Loop wenn Demo zu Ende
            if (_vodDemoIdx >= _vodDemoItems.Count && _vodDemoTimer > 300f)
            {
                _vodDemoTimer = 0f;
                _vodDemoIdx = 0;
            }
        }

        private void UpdateTrackedViewerRadar()
        {
            try
            {
                if (string.IsNullOrEmpty(_trackedUsername))
                {
                    if (_radarMarkerBorderGo != null && _radarMarkerBorderGo.activeSelf)
                    {
                        _radarMarkerBorderGo.SetActive(false);
                    }
                    return;
                }

                if (_headerCanvasGo == null)
                {
                    CreateTopHeaderOverlay();
                }

                if (_radarMarkerBorderGo == null || _radarMarkerRt == null || _radarMarkerTextComp == null)
                {
                    return;
                }

                Transform targetTransform = null;
                float heightOffset = 2.45f;
                string locationDesc = "BESUCHER";

                if (_usernameToStaffId.TryGetValue(_trackedUsername, out int staffId))
                {
                    if (_staffNametags.TryGetValue(staffId, out TMP_Text staffTmp) && staffTmp != null && staffTmp.transform.parent != null)
                    {
                        targetTransform = staffTmp.transform.parent;
                        heightOffset = 2.6f;
                    }
                    if (_staffAssignments.TryGetValue(staffId, out ViewerInfo sv) && sv != null && !string.IsNullOrEmpty(sv.AssignedStaffRoleTitle))
                    {
                        locationDesc = sv.AssignedStaffRoleTitle;
                    }
                }
                else if (_usernameToPeepId.TryGetValue(_trackedUsername, out int peepId))
                {
                    var peepsDict = Peep_Controller.Peeps;
                    if (peepsDict != null && peepsDict.TryGetValue((ushort)peepId, out Peep targetPeep) && targetPeep != null)
                    {
                        targetTransform = targetPeep.transform;
                    }
                    if (_viewerCurrentRideId.TryGetValue(_trackedUsername, out int rId) && _rideInstanceNameCache.TryGetValue(rId, out string rName) && !string.IsNullOrEmpty(rName))
                    {
                        locationDesc = rName.ToUpperInvariant();
                    }
                }

                Camera cam = Camera.main;
                if (targetTransform == null || cam == null)
                {
                    if (_radarMarkerBorderGo.activeSelf) _radarMarkerBorderGo.SetActive(false);
                    return;
                }

                if (!_radarMarkerBorderGo.activeSelf)
                {
                    _radarMarkerBorderGo.SetActive(true);
                }

                Vector3 worldPos = targetTransform.position + new Vector3(0f, heightOffset, 0f);
                float distMeters = Vector3.Distance(cam.transform.position, targetTransform.position);
                int distRounded = Mathf.Max(1, Mathf.RoundToInt(distMeters));

                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                bool isBehind = screenPos.z < 0f;
                if (isBehind)
                {
                    screenPos.x = Screen.width - screenPos.x;
                    screenPos.y = 85f;
                }

                // Rechne Screen-Pixel in die 1920x1080 Referenz-Auflösung des CanvasScalers um
                RectTransform canvasRt = _headerCanvasGo.GetComponent<RectTransform>();
                float canvasW = canvasRt != null && canvasRt.rect.width > 100f ? canvasRt.rect.width : 1920f;
                float canvasH = canvasRt != null && canvasRt.rect.height > 100f ? canvasRt.rect.height : 1080f;

                float normX = Mathf.Clamp(screenPos.x / Mathf.Max(1f, (float)Screen.width), 0.08f, 0.92f);
                float normY = Mathf.Clamp(screenPos.y / Mathf.Max(1f, (float)Screen.height), 0.08f, 0.86f);

                if (isBehind)
                {
                    normY = 0.09f;
                }

                _radarMarkerRt.anchoredPosition = new Vector2(normX * canvasW, normY * canvasH);

                string dispName = _trackedUsername;
                if (_knownViewers.TryGetValue(_trackedUsername, out ViewerInfo kv) && !string.IsNullOrEmpty(kv.DisplayName))
                {
                    dispName = kv.DisplayName;
                }

                string dirArrow = isBehind ? "▼ HINTER DIR ▼" : "▼ GESUCHT ▼";
                _radarMarkerTextComp.text =
                    $"<color=#00E5FF><b>[ {dirArrow} • {distRounded}m ]</b></color>\n" +
                    $"<color=#FFD700><b>{dispName}</b></color> <color=#CCCCCC>({locationDesc})</color>";
            }
            catch { }
        }

        // ==========================================
        // PER-USER CHAT COMMANDS, SPRECHBLASEN & WUNSCH-JOBS
        // ==========================================
        private void HandleUserChatCommand(UserChatCommand userCmd)
        {
            string username = userCmd.Username;
            string rawMsg = (userCmd.CommandText ?? "").Trim();
            string cmd = rawMsg.ToLowerInvariant();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(rawMsg)) return;

            // Inaktivitaets-Timer aktualisieren bei jeder Chat-Aktivitaet
            if (_knownViewers.TryGetValue(username, out ViewerInfo activeChatter))
            {
                activeChatter.LastSeen = DateTime.UtcNow;
            }

            // Falls der Zuschauer noch nicht zugewiesen ist, erledigt das die getaktete Zuweisungsschleife automatisch ohne FPS-Ruckler!

            // 1. Normale Chat-Nachricht (kein !-Befehl) -> Live-Sprechblase über dem Kopf!
            if (!userCmd.IsCommand)
            {
                if (_knownViewers.TryGetValue(username, out ViewerInfo chatViewer) && !chatViewer.IsBot)
                {
                    string cleanBubble = SanitizeSpeechBubbleText(rawMsg);
                    if (!string.IsNullOrEmpty(cleanBubble))
                    {
                        float bubbleSecs = TwitchPlugin.SpeechBubbleDuration != null ? Mathf.Max(5f, TwitchPlugin.SpeechBubbleDuration.Value) : 30f;
                        chatViewer.ActiveSpeechBubble = cleanBubble;
                        chatViewer.SpeechBubbleExpireTime = Time.time + bubbleSecs;
                        RefreshViewerNametag(chatViewer);
                    }
                }
                return;
            }

            // 2. Wunsch-Job / Besucher-Wechsel Befehle (!mitfahren [fahrgeschäft], !gast, !kasse, !bude, !chip, !arbeit, !reko)
            if (cmd.StartsWith("!mitfahren") || cmd.StartsWith("!gast") || cmd.StartsWith("!besucher"))
            {
                string rideArg = "";
                int spaceIdx = rawMsg.IndexOf(' ');
                if (spaceIdx > 0 && spaceIdx + 1 < rawMsg.Length)
                {
                    rideArg = rawMsg.Substring(spaceIdx + 1).Trim();
                }
                SwitchViewerToVisitor(username, rideArg);
                return;
            }
            if (cmd.StartsWith("!kasse"))
            {
                SwitchViewerToDesiredStaff(username, ViewerJobPreference.StaffKasse, "KASSE");
                return;
            }
            if (cmd.StartsWith("!chip") || cmd.StartsWith("!sammler"))
            {
                SwitchViewerToDesiredStaff(username, ViewerJobPreference.StaffChip, "CHIP-SAMMLER");
                return;
            }
            if (cmd.StartsWith("!bude") || cmd.StartsWith("!stand"))
            {
                SwitchViewerToDesiredStaff(username, ViewerJobPreference.StaffBude, "STAND");
                return;
            }
            if (cmd.StartsWith("!reko") || cmd.StartsWith("!operator"))
            {
                SwitchViewerToDesiredStaff(username, ViewerJobPreference.StaffReko, "REKOMMANDEUR");
                return;
            }
            if (cmd.StartsWith("!arbeit") || cmd.StartsWith("!job"))
            {
                SwitchViewerToDesiredStaff(username, ViewerJobPreference.StaffAny, null);
                return;
            }

            // 3. Animations-Befehle (!jubel, !klatschen, !zugabe)
            bdt desiredAction = bdt.None;
            string bubbleText = null;
            bool playScreamAudio = false;

            if (TwitchPlugin.EnableJubel.Value && (cmd.StartsWith("!jubel") || cmd.StartsWith("!handsup") || cmd.StartsWith("!hände") || cmd.StartsWith("!haende") || cmd.StartsWith("!tanz") || cmd.StartsWith("!dance")))
            {
                desiredAction = bdt.Hands_Up;
                bubbleText = "[!jubel]";
            }
            else if (TwitchPlugin.EnableKlatschen.Value && (cmd.StartsWith("!klatschen") || cmd.StartsWith("!clap") || cmd.StartsWith("!applaus")))
            {
                desiredAction = bdt.Clap;
                bubbleText = "[!klatschen]";
            }
            else if (TwitchPlugin.EnableZugabe.Value && (cmd.StartsWith("!zugabe") || cmd.StartsWith("!again") || cmd.StartsWith("!scream") || cmd.StartsWith("!schrei")))
            {
                desiredAction = bdt.Hands_Up;
                bubbleText = "[!zugabe]";
                playScreamAudio = true;
            }
            else
            {
                return;
            }

            TriggerSingleViewerAction(username, desiredAction, bubbleText, playScreamAudio);
        }

        private static string SanitizeSpeechBubbleText(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            // Keine Links oder RichText-Tags erlauben
            if (input.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0 || input.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }
            string s = input.Replace("<", "").Replace(">", "").Replace("\r", " ").Replace("\n", " ").Trim();
            // Entferne Unicode-Emojis (Surrogate Pairs & Sonderzeichen > 0x024F), da die Spiel-Schriftart diese sonst als leeres Viereck □ darstellt!
            StringBuilder cleanSb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsSurrogate(c)) continue;
                if (c <= 0x024F || c == '€' || c == '–' || c == '—' || c == '…')
                {
                    cleanSb.Append(c);
                }
            }
            s = cleanSb.ToString().Trim();
            if (string.IsNullOrEmpty(s)) return null;

            if (s.Length > 44)
            {
                s = s.Substring(0, 41) + "...";
            }
            return s;
        }

        private bool FindTargetRideOrLocationByKeyword(string rideKeyword, out Ride_Controller foundRide, out Vector3 targetWorldPos, out string displayRideName)
        {
            foundRide = null;
            targetWorldPos = Vector3.zero;
            displayRideName = null;

            if (!_staffDiscoveredForScene || _cachedRideControllers.Count == 0)
            {
                _staffDiscoveredForScene = true;
                DiscoverRideStaffSlots();
            }

            // 1. Falls ein Wunsch-Name angegeben wurde (z.B. "!mitfahren riesenrad" oder "!mitfahren mega dance")
            if (!string.IsNullOrEmpty(rideKeyword))
            {
                string normQuery = rideKeyword.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                string altQuery = normQuery;
                if (normQuery.Contains("riesenrad") || normQuery.Contains("ferris")) { normQuery = "ferris"; altQuery = "riesenrad"; }
                else if (normQuery.Contains("spiegel") || normQuery.Contains("mirror")) { normQuery = "mirror"; altQuery = "spiegel"; }
                else if (normQuery.Contains("break") || normQuery.Contains("mega")) { normQuery = "megadance"; altQuery = "megadance"; }
                else if (normQuery.Contains("musik") || normQuery.Contains("sound") || normQuery.Contains("express")) { normQuery = "soundexpress"; altQuery = "soundexpress"; }
                else if (normQuery.Contains("teufel")) { normQuery = "teufelstanz"; altQuery = "teufelstanz"; }
                else if (normQuery.Contains("flipper") || normQuery.Contains("pinball")) { normQuery = "pinball"; altQuery = "pinball"; }

                // A) Suche zuerst in den entdeckten Mitarbeiter-Plätzen (dort stehen die exakten Namen wie [KASSE • FERRISWHEEL] oder [KASSE • RIESENRAD]!)
                for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                {
                    StaffSlot s = _discoveredStaffSlots[i];
                    if (s == null || s.TargetTransform == null) continue;
                    string jTitle = (s.JobTitle ?? "").ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                    string objName = (s.TargetTransform.name ?? "").ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

                    if (jTitle.Contains(normQuery) || jTitle.Contains(altQuery) || objName.Contains(normQuery) || objName.Contains(altQuery))
                    {
                        targetWorldPos = s.TargetTransform.position;
                        // Extrahiere den schönen Namen hinter "•" falls vorhanden
                        int dotIdx = (s.JobTitle ?? "").IndexOf('•');
                        displayRideName = dotIdx >= 0 && dotIdx + 1 < s.JobTitle.Length
                            ? s.JobTitle.Substring(dotIdx + 1).Trim()
                            : TranslateFairgroundObjectName(rideKeyword);

                        // Suche den zugehörigen oder nächstgelegenen Ride_Controller
                        Ride_Controller parentRc = s.TargetTransform.GetComponentInParent<Ride_Controller>();
                        if (parentRc != null)
                        {
                            foundRide = parentRc;
                        }
                        else
                        {
                            float bestRcDist = 45f;
                            foreach (var rc in _cachedRideControllers)
                            {
                                if (rc == null || rc.transform == null) continue;
                                float d = Vector3.Distance(targetWorldPos, rc.transform.position);
                                if (d < bestRcDist)
                                {
                                    bestRcDist = d;
                                    foundRide = rc;
                                }
                            }
                        }
                        if (foundRide != null)
                        {
                            _rideInstanceNameCache[foundRide.GetInstanceID()] = displayRideName;
                        }
                        return true;
                    }
                }

                // B) Suche direkt über die Ride_Controller-Hierarchie
                foreach (var rc in _cachedRideControllers)
                {
                    if (rc == null || rc.gameObject == null || rc.transform == null) continue;
                    Transform curr = rc.transform;
                    for (int d = 0; d < 4 && curr != null; d++)
                    {
                        string rName = CleanUnityObjectName(curr.name);
                        string normRide = (rName ?? "").ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                        if (!string.IsNullOrEmpty(normRide) && (normRide.Contains(normQuery) || normRide.Contains(altQuery)))
                        {
                            foundRide = rc;
                            targetWorldPos = rc.transform.position;
                            displayRideName = TranslateFairgroundObjectName(rName);
                            _rideInstanceNameCache[rc.GetInstanceID()] = displayRideName;
                            return true;
                        }
                        curr = curr.parent;
                    }
                }
            }

            // 2. Falls nur "!mitfahren" (ohne Name) getippt wurde: Prüfe, ob der Streamer selbst gerade ein Fahrgeschäft steuert!
            foreach (var rc in _cachedRideControllers)
            {
                if (rc == null || rc.transform == null) continue;
                try
                {
                    if (rc.Self_Controling)
                    {
                        foundRide = rc;
                        targetWorldPos = rc.transform.position;
                        if (!_rideInstanceNameCache.TryGetValue(rc.GetInstanceID(), out displayRideName) || string.IsNullOrEmpty(displayRideName) || IsGenericHierarchyName(displayRideName))
                        {
                            string rawRide = CleanUnityObjectName(rc.gameObject.name);
                            if (IsGenericHierarchyName(rawRide) && rc.transform.parent != null)
                            {
                                rawRide = CleanUnityObjectName(rc.transform.parent.name);
                            }
                            displayRideName = TranslateFairgroundObjectName(!string.IsNullOrEmpty(rawRide) ? rawRide : "FAHRGESCHÄFT");
                        }
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private void SwitchViewerToVisitor(string username, string desiredRideKeyword = "")
        {
            if (!_knownViewers.TryGetValue(username, out ViewerInfo viewer)) return;
            viewer.JobPreference = ViewerJobPreference.VisitorOnly;

            // Falls der User aktuell an einer Kasse/Bude/Fahrgeschäft arbeitet -> Platz freigeben!
            if (_usernameToStaffId.TryGetValue(username, out int staffId))
            {
                _staffAssignments.Remove(staffId);
                _usernameToStaffId.Remove(username);
                _assignedUsernames.Remove(username);
                viewer.AssignedStaffRoleTitle = null;

                if (_staffNametags.TryGetValue(staffId, out TMP_Text tm))
                {
                    if (tm != null && tm.gameObject != null) UnityEngine.Object.Destroy(tm.gameObject);
                    _staffNametags.Remove(staffId);
                }
            }

            bool hasTargetLocation = FindTargetRideOrLocationByKeyword(desiredRideKeyword, out Ride_Controller targetRide, out Vector3 targetRidePos, out string targetRideDisplayName);

            var peepsDict = Peep_Controller.Peeps;

            // Falls der Zuschauer schon einen Besucher-Peep hat und kein bestimmtes Fahrgeschäft gewünscht wurde -> fertig!
            if (_usernameToPeepId.TryGetValue(username, out int currentPeepId) && !hasTargetLocation)
            {
                return;
            }

            int targetPeepId = -1;
            Peep targetPeep = null;

            if (peepsDict != null)
            {
                // A) Wenn ein konkretes Ziel-Fahrgeschäft gefunden wurde (z.B. "!mitfahren riesenrad" oder "!mitfahren mega dance"):
                // Suche den Peep, der bereits IN diesem Fahrgeschäft sitzt oder am nächsten direkt vor dem Eingang / in der Warteschlange steht!
                if (hasTargetLocation)
                {
                    // Falls der Zuschauer schon auf genau diesem Fahrgeschäft sitzt, nichts ändern!
                    if (targetRide != null
                        && _usernameToPeepId.TryGetValue(username, out int existingPId)
                        && peepsDict.TryGetValue((ushort)existingPId, out Peep existingP)
                        && existingP != null && existingP.transform != null)
                    {
                        Ride_Controller currRc = existingP.transform.GetComponentInParent<Ride_Controller>();
                        if (currRc != null && currRc.GetInstanceID() == targetRide.GetInstanceID())
                        {
                            return;
                        }
                    }

                    // 1. Priorität: Ein freier Peep, der schon in diesem Fahrgeschäft sitzt oder ganz nah davor steht
                    float bestFreeDist = float.MaxValue;
                    foreach (var entry in peepsDict)
                    {
                        Peep p = entry.Value;
                        if (p == null || p.transform == null || p.gameObject == null) continue;
                        if (_peepAssignments.ContainsKey(entry.Key)) continue;

                        Ride_Controller pRc = p.transform.GetComponentInParent<Ride_Controller>();
                        bool seatedOnTarget = (targetRide != null && pRc != null && pRc.GetInstanceID() == targetRide.GetInstanceID());
                        float d = seatedOnTarget ? 0f : Vector3.Distance(p.transform.position, targetRidePos);

                        if (d < bestFreeDist)
                        {
                            bestFreeDist = d;
                            targetPeepId = entry.Key;
                            targetPeep = p;
                            if (seatedOnTarget) break;
                        }
                    }

                    // 2. Priorität: Falls alle Peeps am Fahrgeschäft schon Namen haben, tausche mit einem normalen Gast am/im Ziel-Fahrgeschäft!
                    if (targetPeep == null || bestFreeDist > 22f)
                    {
                        float bestSwapDist = bestFreeDist;
                        foreach (var entry in peepsDict)
                        {
                            Peep p = entry.Value;
                            if (p == null || p.transform == null || p.gameObject == null) continue;
                            if (!_peepAssignments.TryGetValue(entry.Key, out ViewerInfo occ) || occ == viewer) continue;
                            if (occ.IsStaffEligible || occ.JobPreference == ViewerJobPreference.VisitorOnly) continue;

                            Ride_Controller pRc = p.transform.GetComponentInParent<Ride_Controller>();
                            bool seatedOnTarget = (targetRide != null && pRc != null && pRc.GetInstanceID() == targetRide.GetInstanceID());
                            float d = seatedOnTarget ? 0f : Vector3.Distance(p.transform.position, targetRidePos);

                            if (d < bestSwapDist)
                            {
                                bestSwapDist = d;
                                targetPeepId = entry.Key;
                                targetPeep = p;
                                if (seatedOnTarget) break;
                            }
                        }
                    }
                }

                // B) Allgemeiner Fallback (wenn kein bestimmtes Fahrgeschäft angegeben wurde):
                if (targetPeep == null)
                {
                    foreach (var entry in peepsDict)
                    {
                        if (entry.Value == null || entry.Value.gameObject == null) continue;
                        if (!_peepAssignments.ContainsKey(entry.Key))
                        {
                            targetPeepId = entry.Key;
                            targetPeep = entry.Value;
                            break;
                        }
                    }
                }

                if (targetPeep == null)
                {
                    foreach (var entry in peepsDict)
                    {
                        if (entry.Value == null || entry.Value.gameObject == null) continue;
                        if (_peepAssignments.TryGetValue(entry.Key, out ViewerInfo occ) && occ != viewer && occ.JobPreference != ViewerJobPreference.VisitorOnly)
                        {
                            targetPeepId = entry.Key;
                            targetPeep = entry.Value;
                            break;
                        }
                    }
                }
            }

            // Falls der Zuschauer vorher schon einen anderen Peep hatte -> alten Peep sauber freigeben
            if (_usernameToPeepId.TryGetValue(username, out int oldPeepId) && oldPeepId != targetPeepId)
            {
                _peepAssignments.Remove(oldPeepId);
                _usernameToPeepId.Remove(username);
                _assignedUsernames.Remove(username);
                if (_peepNametags.TryGetValue(oldPeepId, out TMP_Text oldTm))
                {
                    if (oldTm != null && oldTm.gameObject != null) UnityEngine.Object.Destroy(oldTm.gameObject);
                    _peepNametags.Remove(oldPeepId);
                }
            }

            if (targetPeep != null && targetPeepId != -1)
            {
                // Falls auf dem Ziel-Peep noch ein anderer Zuschauer saß -> diesen zurück in die Warteschlange legen
                if (_peepAssignments.TryGetValue(targetPeepId, out ViewerInfo prevOcc) && prevOcc != null && prevOcc != viewer)
                {
                    _peepAssignments.Remove(targetPeepId);
                    _usernameToPeepId.Remove(prevOcc.Username);
                    _assignedUsernames.Remove(prevOcc.Username);
                    EnqueueViewerByRole(prevOcc);
                }

                viewer.AssignedStaffRoleTitle = null;
                _peepAssignments[targetPeepId] = viewer;
                _assignedUsernames.Add(viewer.Username);
                _usernameToPeepId[viewer.Username] = targetPeepId;
                CreateOrUpdateNametag(targetPeep.transform, targetPeepId, viewer, false, 2.15f);

                if (targetRide != null)
                {
                    int rId = targetRide.GetInstanceID();
                    _viewerCurrentRideId[viewer.Username] = rId;
                    if (!string.IsNullOrEmpty(targetRideDisplayName))
                    {
                        _rideInstanceNameCache[rId] = targetRideDisplayName;
                    }
                }
            }
            else
            {
                EnqueueViewerByRole(viewer);
            }

            UpdateNametagsAndAssignments();

            string shownName = !string.IsNullOrEmpty(viewer.DisplayName) ? viewer.DisplayName : viewer.Username;
            string nameColor = viewer.GetEffectiveNameColorHex();
            if (!string.IsNullOrEmpty(targetRideDisplayName))
            {
                _latestJoinFeedText = $"<color=#00E676><b>[MITFAHREN]</b></color> <color={nameColor}><b>{shownName}</b></color> <color=#E0E0E0>ist jetzt unterwegs zu / eingestiegen bei:</color> <color=#00E5FF><b>[{targetRideDisplayName}]</b></color>!";
            }
            else
            {
                _latestJoinFeedText = $"<color=#00E676><b>[FEIERABEND]</b></color> <color={nameColor}><b>{shownName}</b></color> <color=#E0E0E0>hat seinen Arbeitsplatz verlassen und läuft jetzt als Besucher über den Platz!</color>";
            }
            _joinFeedExpireTime = Time.time + 12f;
            UpdateTopHeaderOverlay();
        }

        private void SwitchViewerToDesiredStaff(string username, ViewerJobPreference pref, string requiredRoleKeyword)
        {
            if (!_knownViewers.TryGetValue(username, out ViewerInfo viewer)) return;
            viewer.JobPreference = pref;

            if (!_staffDiscoveredForScene)
            {
                _staffDiscoveredForScene = true;
                DiscoverRideStaffSlots();
            }

            // Suche einen freien Slot (oder einen von einem normalen Gast ohne Wunsch-Job belegten Slot), der zum Wunsch passt
            StaffSlot targetSlot = null;
            for (int i = 0; i < _discoveredStaffSlots.Count; i++)
            {
                StaffSlot s = _discoveredStaffSlots[i];
                if (s.TargetTransform == null || !s.TargetTransform.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(requiredRoleKeyword) && (s.JobTitle == null || s.JobTitle.IndexOf(requiredRoleKeyword, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                if (!_staffAssignments.ContainsKey(s.InstanceId))
                {
                    targetSlot = s;
                    break;
                }
            }

            // Falls kein komplett freier Slot dieser Kategorie da ist, darf ein Mod/VIP/Sub einen Slot übernehmen, der von jemandem ohne festen Wunsch besetzt ist
            if (targetSlot == null)
            {
                for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                {
                    StaffSlot s = _discoveredStaffSlots[i];
                    if (s.TargetTransform == null || !s.TargetTransform.gameObject.activeInHierarchy) continue;
                    if (!string.IsNullOrEmpty(requiredRoleKeyword) && (s.JobTitle == null || s.JobTitle.IndexOf(requiredRoleKeyword, StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    if (_staffAssignments.TryGetValue(s.InstanceId, out ViewerInfo occ) && occ != viewer)
                    {
                        if (!occ.IsStaffEligible || viewer.IsStaffEligible)
                        {
                            // Alten Mitarbeiter ablösen
                            occ.AssignedStaffRoleTitle = null;
                            _assignedUsernames.Remove(occ.Username);
                            _usernameToStaffId.Remove(occ.Username);
                            _staffAssignments.Remove(s.InstanceId);
                            EnqueueViewerByRole(occ);
                            targetSlot = s;
                            break;
                        }
                    }
                }
            }

            if (targetSlot == null) return;

            // Alten Besucher-Peep oder alten Staff-Slot dieses Users freigeben
            if (_usernameToPeepId.TryGetValue(username, out int oldPeepId))
            {
                _peepAssignments.Remove(oldPeepId);
                _usernameToPeepId.Remove(username);
                _assignedUsernames.Remove(username);
                if (_peepNametags.TryGetValue(oldPeepId, out TMP_Text pTm))
                {
                    if (pTm != null && pTm.gameObject != null) UnityEngine.Object.Destroy(pTm.gameObject);
                    _peepNametags.Remove(oldPeepId);
                }
            }
            if (_usernameToStaffId.TryGetValue(username, out int oldStaffId))
            {
                _staffAssignments.Remove(oldStaffId);
                _usernameToStaffId.Remove(username);
                _assignedUsernames.Remove(username);
                if (_staffNametags.TryGetValue(oldStaffId, out TMP_Text sTm))
                {
                    if (sTm != null && sTm.gameObject != null) UnityEngine.Object.Destroy(sTm.gameObject);
                    _staffNametags.Remove(oldStaffId);
                }
            }

            viewer.AssignedStaffRoleTitle = targetSlot.JobTitle;
            _staffAssignments[targetSlot.InstanceId] = viewer;
            _assignedUsernames.Add(viewer.Username);
            _usernameToStaffId[viewer.Username] = targetSlot.InstanceId;
            CreateOrUpdateNametag(targetSlot.TargetTransform, targetSlot.InstanceId, viewer, true, targetSlot.HeightOffset);
            ShowJoinNotification(viewer, true, targetSlot.JobTitle);
            TwitchPlugin.Log?.LogInfo($"[Twitch-Job] '{viewer.DisplayName ?? viewer.Username}' hat per Chat-Befehl gewechselt zu: [{targetSlot.JobTitle}]!");
        }

        private Peep_Interaction_Controller FindActivePeepInteractionController(Transform preferNearTransform = null)
        {
            try
            {
                // 1. Falls eine Ziel-Position (Peep oder Mitarbeiter) bekannt ist, suche im selben Ride_Controller oder im Umkreis
                if (preferNearTransform != null)
                {
                    var rcInParent = preferNearTransform.GetComponentInParent<Ride_Controller>();
                    if (rcInParent != null)
                    {
                        var picInRide = rcInParent.GetComponentInChildren<Peep_Interaction_Controller>(true);
                        if (picInRide != null) return picInRide;
                    }
                }

                // 2. Falls der Spieler gerade selbst ein Fahrgeschäft steuert (Self_Controling), nimm genau dessen Peep_Interaction_Controller (wie in der WebApp!)
                if (!_staffDiscoveredForScene)
                {
                    _staffDiscoveredForScene = true;
                    DiscoverRideStaffSlots();
                }

                for (int i = 0; i < _cachedRideControllers.Count; i++)
                {
                    var rc = _cachedRideControllers[i];
                    if (rc == null) continue;
                    bool selfCtrl = false;
                    try { selfCtrl = rc.Self_Controling; } catch { }
                    if (selfCtrl)
                    {
                        var pic = rc.GetComponentInChildren<Peep_Interaction_Controller>(true);
                        if (pic != null) return pic;
                    }
                }

                // 3. Verwende die gecachten Peep_Interaction_Controller (kein FindObjectsOfType im laufenden Spiel!)
                if (_cachedPeepInteractionControllers.Count > 0)
                {
                    if (preferNearTransform != null)
                    {
                        Peep_Interaction_Controller nearest = null;
                        float bestDist = float.MaxValue;
                        Vector3 pos = preferNearTransform.position;
                        for (int i = 0; i < _cachedPeepInteractionControllers.Count; i++)
                        {
                            var item = _cachedPeepInteractionControllers[i];
                            if (item == null || item.transform == null) continue;
                            float d = Vector3.Distance(pos, item.transform.position);
                            if (d < bestDist)
                            {
                                bestDist = d;
                                nearest = item;
                            }
                        }
                        if (nearest != null) return nearest;
                    }

                    for (int i = 0; i < _cachedPeepInteractionControllers.Count; i++)
                    {
                        var item = _cachedPeepInteractionControllers[i];
                        if (item != null && item.Interaction_Containers != null && item.Interaction_Containers.Count > 0)
                        {
                            return item;
                        }
                    }
                    return _cachedPeepInteractionControllers[0];
                }
            }
            catch { }
            return null;
        }

        private void TriggerWebAppStylePeepReaction(Peep_Interaction_Controller pic, Seat_Peep_Animation_Controller ac, bdt action, bool playAgainOrScream, float ratio = 0.85f)
        {
            try
            {
                if (pic == null) pic = FindActivePeepInteractionController();
                if (ac == null && pic != null) ac = pic.Seat_Peep_Animation_Controller;
                if (ac == null && _cachedSeatAnimControllers.Count > 0) ac = _cachedSeatAnimControllers[0];

                bool isHands = (action == bdt.Hands_Up);
                bool isClap = (action == bdt.Clap);

                // 1. Sitz-Animationen über Seat_Peep_Animation_Controller.kqm (exakt wie in der WebApp!)
                if (ac != null)
                {
                    try
                    {
                        ac.kqm(action, Mathf.Clamp(ratio, 0.35f, 1.0f));
                    }
                    catch { }
                }

                // 2. Audio-Container (Screaming_Audio_Source, Clap_Audio_Source, Again_Audio_Source) & Flags auf Peep_Interaction_Controller (exakt wie in der WebApp!)
                if (pic != null)
                {
                    try
                    {
                        pic.Hands_Up = isHands;
                        pic.Clap = isClap;
                        if (playAgainOrScream) pic.Scream = true;
                    }
                    catch { }

                    if (pic.Interaction_Containers != null)
                    {
                        for (int j = 0; j < pic.Interaction_Containers.Count; j++)
                        {
                            var container = pic.Interaction_Containers[j];
                            if (container == null) continue;
                            try
                            {
                                if (playAgainOrScream)
                                {
                                    if (container.Again_Audio_Source != null) container.Again_Audio_Source.Play();
                                    if (container.Screaming_Audio_Source != null) container.Screaming_Audio_Source.Play();
                                }
                                else if (isHands && container.Screaming_Audio_Source != null)
                                {
                                    container.Screaming_Audio_Source.Play();
                                }
                                else if (isClap && container.Clap_Audio_Source != null)
                                {
                                    container.Clap_Audio_Source.Play();
                                }
                            }
                            catch { }
                        }
                    }
                }

                // 3. Alle besetzten Sitze dieses Fahrgeschäfts zusätzlich direkt über ihren Animator + CPU-Override aktivieren (wie in der WebApp!)
                string handsParam = (ac != null && !string.IsNullOrEmpty(ac.hands_Up_Parameter)) ? ac.hands_Up_Parameter : "Hands_Up";
                string clapParam = (ac != null && !string.IsNullOrEmpty(ac.clap_Parameter)) ? ac.clap_Parameter : "Clap";
                Seat[] seats = null;
                if (ac != null) seats = ac.GetComponentsInChildren<Seat>(true);
                else if (pic != null) seats = pic.GetComponentsInChildren<Seat>(true);

                if (seats != null && seats.Length > 0)
                {
                    for (int k = 0; k < seats.Length; k++)
                    {
                        Seat s = seats[k];
                        if (s == null) continue;
                        Peep p = s.Peep;
                        if (p == null) continue;
                        ApplyPeepSeatOrBodyAction(p, p.Peep_Moving, s, ac, action);
                    }
                }
            }
            catch { }
        }

        private void ApplyPeepSeatOrBodyAction(Peep targetPeep, Peep_Moving moving, Seat seat, Seat_Peep_Animation_Controller ac, bdt action, ActiveUserActionState actionState = null)
        {
            try
            {
                if (moving != null)
                {
                    try { moving.rideViewSeatCpuAnimationRequired = true; } catch { }
                    try { moving.eventDrivenRideSeatAnimationFrozen = false; } catch { }
                    try { moving.Animation_Freezed = false; } catch { }
                }

                if (seat != null && ac != null)
                {
                    try
                    {
                        if (!ac.kqg(seat, action, true))
                        {
                            ac.fco(seat, action);
                        }
                    }
                    catch
                    {
                        try { ac.fco(seat, action); } catch { }
                    }
                }

                Animator anim = moving != null ? moving.Animator : null;
                if (anim == null && targetPeep != null)
                {
                    anim = targetPeep.GetComponentInChildren<Animator>(true);
                }

                if (anim != null)
                {
                    try
                    {
                        anim.enabled = true;
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                        string upperLayer = (ac != null && !string.IsNullOrEmpty(ac.upper_Body_Layer)) ? ac.upper_Body_Layer : "Seat Upper Body";
                        int layerIdx = anim.GetLayerIndex(upperLayer);
                        if (layerIdx < 0) layerIdx = anim.GetLayerIndex("Seat Upper Body");
                        if (layerIdx < 0) layerIdx = anim.GetLayerIndex("Upper Body");
                        if (layerIdx < 0) layerIdx = anim.GetLayerIndex("UpperBody");
                        if (layerIdx < 0 && anim.layerCount > 1) layerIdx = 1;
                        if (layerIdx >= 0)
                        {
                            anim.SetLayerWeight(layerIdx, 1f);
                        }

                        string handsParam = (ac != null && !string.IsNullOrEmpty(ac.hands_Up_Parameter)) ? ac.hands_Up_Parameter : "Hands_Up";
                        string clapParam = (ac != null && !string.IsNullOrEmpty(ac.clap_Parameter)) ? ac.clap_Parameter : "Clap";

                        bool isHandsUp = (action == bdt.Hands_Up);
                        bool isClap = (action == bdt.Clap);

                        try { anim.SetBool(handsParam, isHandsUp); } catch { }
                        try { anim.SetBool("Hands_Up", isHandsUp); } catch { }
                        try { anim.SetBool("HandsUp", isHandsUp); } catch { }
                        try { anim.SetBool(clapParam, isClap); } catch { }
                        try { anim.SetBool("Clap", isClap); } catch { }

                        // Direkt den State im Layer abspielen, damit die Arme sofort die volle Bewegung ausführen!
                        if (layerIdx >= 0)
                        {
                            string targetState = isClap ? "Clap" : (isHandsUp ? "Hands_Up" : "None");
                            try { anim.Play(targetState, layerIdx, 0f); } catch { }
                        }

                        // IK Lock aufheben: Sitze und Kassen fixieren Hände über HandIKController
                        if (ac != null)
                        {
                            try { ac.krm(anim, 1.0f); } catch { }
                        }
                    }
                    catch { }
                }

                // HandIKController des Peeps / Sitzes aufspüren und suppressen
                if (actionState != null && actionState.HandIK == null)
                {
                    HandIKController hik = null;
                    if (targetPeep != null) hik = targetPeep.GetComponentInChildren<HandIKController>(true);
                    if (hik == null && seat != null) hik = seat.GetComponentInChildren<HandIKController>(true);
                    if (hik == null && moving != null) hik = moving.GetComponentInChildren<HandIKController>(true);
                    if (hik != null)
                    {
                        actionState.HandIK = hik;
                        SuppressHandIK(hik);
                    }
                }
                else if (actionState != null && actionState.HandIK != null)
                {
                    SuppressHandIK(actionState.HandIK);
                }
            }
            catch { }
        }

        private static void SuppressHandIK(HandIKController hik)
        {
            if (hik == null) return;
            try { hik.handsIKSuppressed = true; } catch { }
            try { hik.forceZeroIKWeightWrite = true; } catch { }
            try
            {
                if (hik.leftHand != null)
                {
                    hik.leftHand.enabled = false;
                    hik.leftHand.targetWeight = 0f;
                    hik.leftHand.currentWeight = 0f;
                }
            }
            catch { }
            try
            {
                if (hik.rightHand != null)
                {
                    hik.rightHand.enabled = false;
                    hik.rightHand.targetWeight = 0f;
                    hik.rightHand.currentWeight = 0f;
                }
            }
            catch { }
        }

        private static void RestoreHandIK(HandIKController hik)
        {
            if (hik == null) return;
            try { hik.handsIKSuppressed = false; } catch { }
            try { hik.forceZeroIKWeightWrite = false; } catch { }
            try
            {
                if (hik.leftHand != null)
                {
                    hik.leftHand.enabled = true;
                    hik.leftHand.targetWeight = 1f;
                }
            }
            catch { }
            try
            {
                if (hik.rightHand != null)
                {
                    hik.rightHand.enabled = true;
                    hik.rightHand.targetWeight = 1f;
                }
            }
            catch { }
        }

                private void TriggerSingleViewerAction(string username, bdt action, string bubbleText, bool playScream)
        {
            if (!_knownViewers.TryGetValue(username, out ViewerInfo viewer)) return;

            // Falls für diesen User noch eine alte Aktion läuft, vorher sauber zurücksetzen
            if (_activeUserActions.TryGetValue(username, out ActiveUserActionState prevState))
            {
                StopSingleViewerAction(prevState);
                _activeUserActions.Remove(username);
            }

            float duration = TwitchPlugin.ActionDuration.Value;

            // Zeige über dem Kopf DIESES Zuschauers die Aktions-Anzeige (z.B. [JUBELT!])
            viewer.ActiveActionBadge = bubbleText;
            RefreshViewerNametag(viewer);

            ActiveUserActionState state = new ActiveUserActionState
            {
                Username = username,
                PeepId = -1,
                ActionType = action,
                IsAgainOrScream = playScream,
                StatusBubbleText = bubbleText,
                RemainingSeconds = duration,
                NextSeatRefreshTime = Time.time + 0.5f
            };

            Transform referenceTransform = null;

            // 1. Priorisiere Mitarbeiter/Kassierer-Prüfung vor Besucher-Peep!
            if (_usernameToStaffId.TryGetValue(username, out int staffId))
            {
                // Falls der User als Mitarbeiter an einer Kasse / Bude / Fahrgeschäft arbeitet -> animiere nur diesen Mitarbeiter!
                for (int i = 0; i < _discoveredStaffSlots.Count; i++)
                {
                    StaffSlot s = _discoveredStaffSlots[i];
                    if (s.InstanceId == staffId && s.TargetTransform != null)
                    {
                        referenceTransform = s.TargetTransform;
                        try
                        {
                            Peep_Moving empMoving = s.TargetTransform.GetComponentInChildren<Peep_Moving>(true);
                            if (empMoving == null) empMoving = s.TargetTransform.GetComponentInParent<Peep_Moving>();
                            if (empMoving != null)
                            {
                                state.TargetMoving = empMoving;
                                state.OriginalAnimMode = empMoving.Animation_Mode;
                                state.HadOriginalAnimMode = true;
                                empMoving.Animation_Freezed = false;
                            }

                            Animator empAnim = empMoving != null ? empMoving.Animator : s.TargetTransform.GetComponentInChildren<Animator>(true);
                            if (empAnim == null) empAnim = s.TargetTransform.GetComponentInParent<Animator>();
                            if (empAnim != null)
                            {
                                state.WalkAnimator = empAnim;
                                empAnim.enabled = true;
                                empAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                                int upperIdx = empAnim.GetLayerIndex("Seat Upper Body");
                                if (upperIdx < 0) upperIdx = empAnim.GetLayerIndex("UpperBody");
                                if (upperIdx < 0) upperIdx = empAnim.GetLayerIndex("Upper Body");
                                if (upperIdx < 0 && empAnim.layerCount > 1) upperIdx = 1;
                                if (upperIdx >= 0)
                                {
                                    empAnim.SetLayerWeight(upperIdx, 1f);
                                }
                                try { empAnim.SetBool("Hands_Up", action == bdt.Hands_Up); } catch { }
                                try { empAnim.SetBool("HandsUp", action == bdt.Hands_Up); } catch { }
                                try { empAnim.SetBool("Clap", action == bdt.Clap); } catch { }

                                if (upperIdx >= 0)
                                {
                                    string targetState = (action == bdt.Clap) ? "Clap" : ((action == bdt.Hands_Up) ? "Hands_Up" : "None");
                                    try { empAnim.Play(targetState, upperIdx, 0f); } catch { }
                                }
                            }

                            // HandIK Controller für Mitarbeiter suchen & IK unterdrücken (Hände vom Tisch/Pult lösen)
                            HandIKController hik = s.TargetTransform.GetComponentInChildren<HandIKController>(true);
                            if (hik == null) hik = s.TargetTransform.GetComponentInParent<HandIKController>();
                            if (hik != null)
                            {
                                state.HandIK = hik;
                                SuppressHandIK(hik);
                            }

                            Basic_Seating_Pos bsp = s.TargetTransform.GetComponentInChildren<Basic_Seating_Pos>(true);
                            if (bsp == null) bsp = s.TargetTransform.GetComponentInParent<Basic_Seating_Pos>();
                            if (bsp != null)
                            {
                                state.SeatingPos = bsp;
                            }
                        }
                        catch { }
                        break;
                    }
                }
                TwitchPlugin.Log?.LogInfo($"[Twitch-Chat] Mitarbeiter {viewer.DisplayName ?? username} führt allein '{bubbleText}' aus!");
            }
            else if (_usernameToPeepId.TryGetValue(username, out int peepId))
            {
                state.PeepId = peepId;
                var peepsDict = Peep_Controller.Peeps;
                if (peepsDict != null && peepsDict.TryGetValue((ushort)peepId, out Peep targetPeep) && targetPeep != null)
                {
                    referenceTransform = targetPeep.transform;
                    bool foundInSeat = false;
                    Seat matchedSeat = null;
                    Seat_Peep_Animation_Controller matchedAc = null;

                    // 1a. Direkte Prüfung im Scene-Graph (wenn der Peep in einer Gondel sitzt, ist Seat ein Parent)
                    try
                    {
                        if (targetPeep.transform != null)
                        {
                            matchedSeat = targetPeep.transform.GetComponentInParent<Seat>();
                            if (matchedSeat != null)
                            {
                                matchedAc = matchedSeat.GetComponentInParent<Seat_Peep_Animation_Controller>();
                                if (matchedAc == null)
                                {
                                    var rc = matchedSeat.GetComponentInParent<Ride_Controller>();
                                    if (rc != null) matchedAc = rc.GetComponentInChildren<Seat_Peep_Animation_Controller>(true);
                                }
                            }
                        }
                    }
                    catch { }

                    // 1b. Suche über gecachte Seat_Peep_Animation_Controller (active_Peeps + Sitze) ohne FindObjectsOfType!
                    if (matchedSeat == null || matchedAc == null)
                    {
                        if (!_staffDiscoveredForScene)
                        {
                            _staffDiscoveredForScene = true;
                            DiscoverRideStaffSlots();
                        }

                        for (int aIdx = 0; aIdx < _cachedSeatAnimControllers.Count; aIdx++)
                        {
                            var ac = _cachedSeatAnimControllers[aIdx];
                            if (ac == null) continue;
                            if (ac.active_Peeps != null)
                            {
                                foreach (var kvp in ac.active_Peeps)
                                {
                                    Seat seat = kvp.Key;
                                    Peep seatedPeep = kvp.Value;
                                    if (seat != null && seatedPeep != null && seatedPeep.ID == targetPeep.ID)
                                    {
                                        matchedSeat = seat;
                                        matchedAc = ac;
                                        break;
                                    }
                                }
                            }
                            if (matchedSeat == null)
                            {
                                var seatsInRide = ac.GetComponentsInChildren<Seat>(true);
                                if (seatsInRide != null)
                                {
                                    foreach (var seat in seatsInRide)
                                    {
                                        if (seat != null && seat.Peep != null && seat.Peep.ID == targetPeep.ID)
                                        {
                                            matchedSeat = seat;
                                            matchedAc = ac;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (matchedSeat != null && matchedAc != null) break;
                        }
                    }

                    if (matchedSeat != null)
                    {
                        foundInSeat = true;
                        state.ActiveSeat = matchedSeat;
                        state.SeatAnimCtrl = matchedAc;
                        state.TargetMoving = targetPeep.Peep_Moving;
                        ApplyPeepSeatOrBodyAction(targetPeep, targetPeep.Peep_Moving, matchedSeat, matchedAc, action, state);
                        TwitchPlugin.Log?.LogInfo($"[Twitch-Chat] {viewer.DisplayName ?? username} sitzt im Fahrgeschäft und führt allein '{bubbleText}' aus!");
                    }

                    // 2. Wenn der Peep DIESES Users auf dem Platz herumläuft/steht -> animiere nur seinen Oberkörper (Klatschen/Hände hoch), kein Ganzkörper-Tanzen!
                    if (!foundInSeat && targetPeep.Peep_Moving != null)
                    {
                        Peep_Moving pm = targetPeep.Peep_Moving;
                        state.TargetMoving = pm;
                        try
                        {
                            state.OriginalAnimMode = pm.Animation_Mode;
                            state.HadOriginalAnimMode = true;
                            pm.Animation_Freezed = false;

                            // Wir belassen den Fortbewegungsmodus (Stand oder Walk), damit der Peep nicht herumtanzt
                            // sondern natürlich stehen bleibt oder weiterläuft während er klatscht/jubelt!
                        }
                        catch { }

                        Animator anim = pm.Animator;
                        if (anim != null)
                        {
                            state.WalkAnimator = anim;
                            try
                            {
                                anim.enabled = true;
                                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                                int upperIdx = anim.GetLayerIndex("Seat Upper Body");
                                if (upperIdx < 0) upperIdx = anim.GetLayerIndex("UpperBody");
                                if (upperIdx < 0) upperIdx = anim.GetLayerIndex("Upper Body");
                                if (upperIdx < 0 && anim.layerCount > 1) upperIdx = 1;
                                if (upperIdx >= 0)
                                {
                                    anim.SetLayerWeight(upperIdx, 1f);
                                }
                                try { anim.SetBool("Hands_Up", action == bdt.Hands_Up); } catch { }
                                try { anim.SetBool("HandsUp", action == bdt.Hands_Up); } catch { }
                                try { anim.SetBool("Clap", action == bdt.Clap); } catch { }

                                if (upperIdx >= 0)
                                {
                                    string targetState = (action == bdt.Clap) ? "Clap" : ((action == bdt.Hands_Up) ? "Hands_Up" : "None");
                                    try { anim.Play(targetState, upperIdx, 0f); } catch { }
                                }
                            }
                            catch { }
                        }

                        HandIKController hik = targetPeep.GetComponentInChildren<HandIKController>(true);
                        if (hik != null)
                        {
                            state.HandIK = hik;
                            SuppressHandIK(hik);
                        }

                        TwitchPlugin.Log?.LogInfo($"[Twitch-Chat] Besucher-Peep #{peepId} von {viewer.DisplayName ?? username} führt allein '{bubbleText}' aus!");
                    }

                    if (playScream)
                    {
                        try
                        {
                            var audioSource = targetPeep.GetComponentInChildren<AudioSource>();
                            if (audioSource != null) audioSource.Play();
                        }
                        catch { }
                    }
                }
            }

            // 4. Sound-Effekt (Jubel / Klatschen / Zugabe) NUR abspielen, wenn der Zuschauer wirklich im Fahrgeschäft sitzt!
            // Wenn er zu Fuß herumläuft oder an einer Bude/Kasse steht, bleibt der Fahrgeschäft-Massen-Sound stumm.
            if (state.ActiveSeat != null)
            {
                try
                {
                    Peep_Interaction_Controller pic = FindActivePeepInteractionController(referenceTransform);
                    if (pic != null && pic.Interaction_Containers != null)
                    {
                        state.PeepInteractCtrl = pic;
                        bool isHands = (action == bdt.Hands_Up);
                        bool isClap = (action == bdt.Clap);

                        for (int j = 0; j < pic.Interaction_Containers.Count; j++)
                        {
                            var container = pic.Interaction_Containers[j];
                            if (container == null) continue;
                            try
                            {
                                if (playScream)
                                {
                                    if (container.Again_Audio_Source != null) container.Again_Audio_Source.Play();
                                    if (container.Screaming_Audio_Source != null) container.Screaming_Audio_Source.Play();
                                }
                                else if (isHands && container.Screaming_Audio_Source != null)
                                {
                                    container.Screaming_Audio_Source.Play();
                                }
                                else if (isClap && container.Clap_Audio_Source != null)
                                {
                                    container.Clap_Audio_Source.Play();
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            _activeUserActions[username] = state;
        }

        private void CheckSpeechBubblesAndRideBoarding()
        {
            try
            {
                // 1. Abgelaufene Live-Sprechblasen nach 7.5 Sekunden ausblenden
                float now = Time.time;
                foreach (var kvp in _knownViewers)
                {
                    ViewerInfo v = kvp.Value;
                    if (!string.IsNullOrEmpty(v.ActiveSpeechBubble) && now >= v.SpeechBubbleExpireTime)
                    {
                        v.ActiveSpeechBubble = null;
                        RefreshViewerNametag(v);
                    }
                }

                // 2. Prüfe, ob ein Twitch-Zuschauer gerade in ein Fahrgeschäft eingestiegen ist (besonders wenn der Streamer selbst steuert!)
                var peepsDict = Peep_Controller.Peeps;
                if (peepsDict == null || _peepAssignments.Count == 0) return;

                foreach (var kvp in _peepAssignments)
                {
                    int peepId = kvp.Key;
                    ViewerInfo viewer = kvp.Value;
                    if (viewer == null) continue;

                    if (!peepsDict.TryGetValue((ushort)peepId, out Peep p) || p == null || p.transform == null)
                    {
                        _viewerCurrentRideId.Remove(viewer.Username);
                        continue;
                    }

                    // Wenn ein Peep in einer Gondel / einem Sitz Platz nimmt, wird er im Scene-Graph Kind des Sitzes bzw. des Ride_Controllers!
                    Ride_Controller rc = p.transform.GetComponentInParent<Ride_Controller>();
                    if (rc != null)
                    {
                        int rideId = rc.GetInstanceID();
                        if (!_viewerCurrentRideId.TryGetValue(viewer.Username, out int prevRideId) || prevRideId != rideId)
                        {
                            _viewerCurrentRideId[viewer.Username] = rideId;

                            if (!_rideInstanceNameCache.TryGetValue(rideId, out string rideName) || string.IsNullOrEmpty(rideName))
                            {
                                rideName = CleanUnityObjectName(rc.gameObject.name).ToUpperInvariant();
                                if (string.IsNullOrEmpty(rideName)) rideName = "FAHRGESCHÄFT";
                                _rideInstanceNameCache[rideId] = rideName;
                            }

                            bool playerControllingThisRide = false;
                            try { playerControllingThisRide = rc.Self_Controling; } catch { }

                            string shownName = !string.IsNullOrEmpty(viewer.DisplayName) ? viewer.DisplayName : viewer.Username;
                            string nameColor = viewer.GetEffectiveNameColorHex();

                            if (playerControllingThisRide)
                            {
                                _latestJoinFeedText = $"<color=#FFD700><b>[BEI DIR EINGESTIEGEN]</b></color> <color={nameColor}><b>{shownName}</b></color> <color=#E0E0E0>sitzt jetzt bei dir im</color> <color=#00E5FF><b>[{rideName}]</b></color>!";
                                _joinFeedExpireTime = Time.time + 14f;
                            }
                            else
                            {
                                _latestJoinFeedText = $"<color=#A970FF><b>[FAHRT GESTARTET]</b></color> <color={nameColor}><b>{shownName}</b></color> <color=#E0E0E0>ist gerade eingestiegen in:</color> <color=#00E5FF><b>[{rideName}]</b></color>!";
                                _joinFeedExpireTime = Time.time + 10f;
                            }
                        }
                    }
                    else
                    {
                        _viewerCurrentRideId.Remove(viewer.Username);
                    }
                }
            }
            catch { }
        }

        private void UpdateActiveUserActions()
        {
            if (_activeUserActions.Count == 0) return;

            float now = Time.time;
            List<string> finishedUsers = null;
            foreach (var kvp in _activeUserActions)
            {
                ActiveUserActionState state = kvp.Value;
                state.RemainingSeconds -= Time.deltaTime;
                if (state.RemainingSeconds <= 0f)
                {
                    if (finishedUsers == null) finishedUsers = new List<string>();
                    finishedUsers.Add(kvp.Key);
                    continue;
                }

                // Halte die Animation für die gesamte Dauer aktiv (verhindert, dass Peep_Moving oder andere Mods sie vorzeitig abbrechen!)
                try
                {
                    if (state.ActiveSeat != null)
                    {
                        if (now >= state.NextSeatRefreshTime)
                        {
                            state.NextSeatRefreshTime = now + 0.45f;
                            ApplyPeepSeatOrBodyAction(null, state.TargetMoving, state.ActiveSeat, state.SeatAnimCtrl, state.ActionType, state);
                        }
                    }
                    else if (state.TargetMoving != null || state.WalkAnimator != null)
                    {
                        // Stelle sicher, dass der Upper Body Layer auf Gewicht 1 bleibt und der State aktiv ist
                        Animator anim = state.WalkAnimator;
                        if (anim == null && state.TargetMoving != null) anim = state.TargetMoving.Animator;
                        if (anim != null)
                        {
                            int upperIdx = anim.GetLayerIndex("Seat Upper Body");
                            if (upperIdx < 0) upperIdx = anim.GetLayerIndex("UpperBody");
                            if (upperIdx < 0) upperIdx = anim.GetLayerIndex("Upper Body");
                            if (upperIdx < 0 && anim.layerCount > 1) upperIdx = 1;
                            if (upperIdx >= 0)
                            {
                                anim.SetLayerWeight(upperIdx, 1f);
                            }
                        }
                    }

                    // IK kontinuierlich unterdrücken während der Aktion
                    if (state.HandIK != null)
                    {
                        SuppressHandIK(state.HandIK);
                    }
                }
                catch { }
            }

            if (finishedUsers != null)
            {
                for (int i = 0; i < finishedUsers.Count; i++)
                {
                    string user = finishedUsers[i];
                    if (_activeUserActions.TryGetValue(user, out ActiveUserActionState state))
                    {
                        StopSingleViewerAction(state);
                        _activeUserActions.Remove(user);
                    }
                }
            }
        }

        private void StopSingleViewerAction(ActiveUserActionState state)
        {
            if (state == null) return;

            // 1. Setze nur den Sitz dieses einzelnen Zuschauers wieder auf Normal (bdt.None) zurück
            if (state.ActiveSeat != null)
            {
                if (state.SeatAnimCtrl != null)
                {
                    try
                    {
                        if (!state.SeatAnimCtrl.kqg(state.ActiveSeat, bdt.None, true))
                        {
                            state.SeatAnimCtrl.fco(state.ActiveSeat, bdt.None);
                        }
                    }
                    catch
                    {
                        try { state.SeatAnimCtrl.fco(state.ActiveSeat, bdt.None); } catch { }
                    }
                }

                if (state.TargetMoving != null)
                {
                    try { state.TargetMoving.rideViewSeatCpuAnimationRequired = false; } catch { }
                }
            }

            // 2. Setze Peep_Moving (Dance -> Walk/Stand) wieder sauber auf den ursprünglichen Zustand zurück
            if (state.TargetMoving != null && state.ActiveSeat == null)
            {
                try
                {
                    try { state.TargetMoving.gzu(false, true); } catch { }
                    Peep_Animation_Mode restoreMode = state.HadOriginalAnimMode ? state.OriginalAnimMode : Peep_Animation_Mode.Walk;
                    if (restoreMode == Peep_Animation_Mode.Dance)
                    {
                        restoreMode = (state.TargetMoving.Moving_Mode == sc.Walk) ? Peep_Animation_Mode.Walk : Peep_Animation_Mode.Stand;
                    }
                    state.TargetMoving.Animation_Mode = restoreMode;
                    try { state.TargetMoving.gzx(restoreMode); } catch { }
                }
                catch { }
            }

            // 3. Setze den eigenen Animator & UpperBody-Layer zurück
            Animator animToReset = state.WalkAnimator;
            if (animToReset == null && state.TargetMoving != null)
            {
                try { animToReset = state.TargetMoving.Animator; } catch { }
            }

            if (animToReset != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(Peep_Moving.DanceAnimatorParameterName))
                    {
                        try { animToReset.SetBool(Peep_Moving.DanceAnimatorParameterName, false); } catch { }
                    }
                    try { animToReset.SetBool("Hands_Up", false); } catch { }
                    try { animToReset.SetBool("HandsUp", false); } catch { }
                    try { animToReset.SetBool("Clap", false); } catch { }

                    int upperIdx = animToReset.GetLayerIndex("Seat Upper Body");
                    if (upperIdx < 0) upperIdx = animToReset.GetLayerIndex("UpperBody");
                    if (upperIdx < 0) upperIdx = animToReset.GetLayerIndex("Upper Body");
                    if (upperIdx < 0 && animToReset.layerCount > 1) upperIdx = 1;
                    if (upperIdx >= 0)
                    {
                        animToReset.SetLayerWeight(upperIdx, 0f);
                        try { animToReset.Play("None", upperIdx, 0f); } catch { }
                    }
                }
                catch { }
            }

            // 3b. Stelle Hand-IK wieder her
            if (state.HandIK != null)
            {
                RestoreHandIK(state.HandIK);
            }

            // 4. Setze Peep_Interaction_Controller & Seat_Peep_Animation_Controller (wie in der WebApp!) zurück, falls kein anderer User sie gerade nutzt
            bool otherActionStillActive = false;
            foreach (var kvp in _activeUserActions)
            {
                if (!string.Equals(kvp.Key, state.Username, StringComparison.OrdinalIgnoreCase) && kvp.Value.RemainingSeconds > 0f)
                {
                    otherActionStillActive = true;
                    break;
                }
            }

            if (!otherActionStillActive)
            {
                if (state.SeatAnimCtrl != null)
                {
                    try { state.SeatAnimCtrl.kqm(bdt.None, 1f); } catch { }
                    try { state.SeatAnimCtrl.kqu(); } catch { }
                    try { state.SeatAnimCtrl.kqv(); } catch { }
                }
                if (state.PeepInteractCtrl != null)
                {
                    try
                    {
                        state.PeepInteractCtrl.Hands_Up = false;
                        state.PeepInteractCtrl.Clap = false;
                        state.PeepInteractCtrl.Scream = false;
                        if (state.PeepInteractCtrl.Interaction_Containers != null)
                        {
                            for (int j = 0; j < state.PeepInteractCtrl.Interaction_Containers.Count; j++)
                            {
                                var c = state.PeepInteractCtrl.Interaction_Containers[j];
                                if (c == null) continue;
                                try { if (c.Screaming_Audio_Source != null) c.Screaming_Audio_Source.Stop(); } catch { }
                                try { if (c.Clap_Audio_Source != null) c.Clap_Audio_Source.Stop(); } catch { }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 5. Entferne das Aktions-Label über dem Kopf des Users
            if (_knownViewers.TryGetValue(state.Username, out ViewerInfo viewer))
            {
                viewer.ActiveActionBadge = null;
                RefreshViewerNametag(viewer);
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;
            try { _httpListener?.Stop(); } catch { }
            try { _ircThread?.Interrupt(); } catch { }
            try { _httpThread?.Interrupt(); } catch { }
        }

        // ==========================================
        // LOCAL LIVE-DASHBOARD HTTP SERVER (127.0.0.1:48888)
        // ==========================================
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }

        private void RefreshDashboardStatusJson()
        {
            try
            {
                string chan = _vodDemoActive ? "DEMO: freizeitgestoert (VOD)" : (!string.IsNullOrEmpty(_currentChannel) ? _currentChannel : TwitchPlugin.ChannelName.Value);
                int peepCount = _peepAssignments.Count;
                int staffCount = _staffAssignments.Count;
                int total = peepCount + staffCount;

                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"channel\":\"{EscapeJson(chan)}\",");
                sb.Append($"\"scene\":\"{EscapeJson(_lastSceneName ?? "")}\",");
                sb.Append($"\"totalActive\":{total},");
                sb.Append($"\"peepCount\":{peepCount},");
                sb.Append($"\"staffCount\":{staffCount},");
                sb.Append($"\"showTopHeader\":{(TwitchPlugin.ShowTopHeader.Value ? "true" : "false")},");
                sb.Append($"\"showNametags\":{(TwitchPlugin.ShowNametags.Value ? "true" : "false")},");
                sb.Append($"\"showBadges\":{(TwitchPlugin.ShowBadges.Value ? "true" : "false")},");
                sb.Append($"\"assignModsToStaff\":{(TwitchPlugin.AssignModsToStaff.Value ? "true" : "false")},");
                sb.Append($"\"allowAllViewersAsStaff\":{(TwitchPlugin.AllowAllViewersAsStaff.Value ? "true" : "false")},");
                sb.Append($"\"excludeBroadcaster\":{(TwitchPlugin.ExcludeBroadcaster.Value ? "true" : "false")},");
                sb.Append($"\"debugMode\":{(TwitchPlugin.DebugMode != null && TwitchPlugin.DebugMode.Value ? "true" : "false")},");
                sb.Append($"\"debugStreamerName\":\"{EscapeJson(TwitchPlugin.DebugStreamerName != null ? TwitchPlugin.DebugStreamerName.Value : "")}\",");
                sb.Append($"\"speechBubbleDuration\":{Mathf.RoundToInt(TwitchPlugin.SpeechBubbleDuration != null ? TwitchPlugin.SpeechBubbleDuration.Value : 30f)},");
                sb.Append($"\"maxVisibleNametags\":{(TwitchPlugin.MaxVisibleNametags != null ? TwitchPlugin.MaxVisibleNametags.Value : 60)},");
                sb.Append($"\"viewerInactivityMinutes\":{Mathf.RoundToInt(TwitchPlugin.ViewerInactivityMinutes != null ? TwitchPlugin.ViewerInactivityMinutes.Value : 10f)},");
                sb.Append($"\"vodDemoActive\":{(_vodDemoActive ? "true" : "false")},");
                sb.Append($"\"vodDemoCount\":{_vodDemoItems.Count},");
                sb.Append($"\"vodDemoPlayed\":{_vodDemoIdx},");
                sb.Append($"\"trackedUser\":\"{EscapeJson(_trackedUsername ?? "")}\",");
                sb.Append("\"viewers\":[");

                bool first = true;
                foreach (var kvp in _staffAssignments)
                {
                    ViewerInfo v = kvp.Value;
                    if (v == null) continue;
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{");
                    sb.Append($"\"user\":\"{EscapeJson(v.Username)}\",");
                    sb.Append($"\"name\":\"{EscapeJson(v.DisplayName ?? v.Username)}\",");
                    sb.Append($"\"role\":\"{v.Role}\",");
                    sb.Append($"\"subMonths\":{v.SubMonths},");
                    sb.Append($"\"verified\":{(v.IsVerified ? "true" : "false")},");
                    sb.Append($"\"color\":\"{EscapeJson(v.GetEffectiveNameColorHex())}\",");
                    sb.Append($"\"job\":\"{EscapeJson(v.AssignedStaffRoleTitle ?? "MITARBEITER")}\",");
                    sb.Append("\"ride\":\"\"");
                    sb.Append("}");
                }

                foreach (var kvp in _peepAssignments)
                {
                    ViewerInfo v = kvp.Value;
                    if (v == null) continue;
                    if (!first) sb.Append(",");
                    first = false;
                    string rideStr = "";
                    if (_viewerCurrentRideId.TryGetValue(v.Username, out int rId) && _rideInstanceNameCache.TryGetValue(rId, out string rName))
                    {
                        rideStr = rName;
                    }
                    sb.Append("{");
                    sb.Append($"\"user\":\"{EscapeJson(v.Username)}\",");
                    sb.Append($"\"name\":\"{EscapeJson(v.DisplayName ?? v.Username)}\",");
                    sb.Append($"\"role\":\"{v.Role}\",");
                    sb.Append($"\"subMonths\":{v.SubMonths},");
                    sb.Append($"\"verified\":{(v.IsVerified ? "true" : "false")},");
                    sb.Append($"\"color\":\"{EscapeJson(v.GetEffectiveNameColorHex())}\",");
                    sb.Append("\"job\":\"\",");
                    sb.Append($"\"ride\":\"{EscapeJson(rideStr)}\"");
                    sb.Append("}");
                }

                sb.Append("]}");
                _cachedStatusJson = sb.ToString();
            }
            catch { }
        }

        private void HttpDashboardWorkerLoop()
        {
            try
            {
                _httpListener = new TcpListener(IPAddress.Loopback, 48888);
                _httpListener.Start();
                TwitchPlugin.Log?.LogInfo("[Twitch] Live-HTML-Dashboard Server aktiv auf http://127.0.0.1:48888");

                if (TwitchPlugin.AutoOpenDashboard != null && TwitchPlugin.AutoOpenDashboard.Value)
                {
                    OpenDashboardInBrowser();
                }

                while (_isRunning)
                {
                    TcpClient client = _httpListener.AcceptTcpClient();
                    HandleHttpClient(client);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    TwitchPlugin.Log?.LogWarning($"[Twitch] Dashboard-Server Hinweis: {ex.Message}");
                }
            }
        }

                private static void OpenDashboardInBrowser()
        {
            try
            {
                new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(1200);
                        string url = "http://127.0.0.1:48888/";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        TwitchPlugin.Log?.LogInfo("[Twitch] Live-Dashboard wurde automatisch im Webbrowser geoeffnet: " + url);
                    }
                    catch (Exception ex)
                    {
                        TwitchPlugin.Log?.LogWarning("[Twitch] Konnte Browser nicht automatisch oeffnen: " + ex.Message);
                    }
                })
                {
                    IsBackground = true,
                    Name = "TwitchDashboardBrowserLauncher"
                }.Start();
            }
            catch { }
        }

        private void HandleHttpClient(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string reqLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(reqLine)) return;

                    string[] parts = reqLine.Split(' ');
                    if (parts.Length < 2) return;
                    string fullPath = parts[1];

                    string responseBody = "{\"ok\":true}";
                    string contentType = "application/json; charset=utf-8";

                    if (fullPath == "/" || fullPath.StartsWith("/index", StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith("/dashboard", StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = "text/html; charset=utf-8";
                        string htmlPath = Path.Combine(Paths.ConfigPath, "FairgroundTwitch_Dashboard.html");
                        if (File.Exists(htmlPath))
                        {
                            responseBody = File.ReadAllText(htmlPath, Encoding.UTF8);
                        }
                        else
                        {
                            responseBody = "<html><body><h2>Fairground Online Twitch Dashboard</h2><p>Lade Datei...</p></body></html>";
                        }
                    }
                    else

                    if (fullPath.StartsWith("/status", StringComparison.OrdinalIgnoreCase))
                    {
                        responseBody = _cachedStatusJson;
                    }
                    else if (fullPath.StartsWith("/set?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(5);
                        string key = ExtractQueryParam(query, "key");
                        string val = ExtractQueryParam(query, "val");

                        _mainThreadActions.Enqueue(() => ApplyLiveDashboardSetting(key, val));
                    }
                    else if (fullPath.StartsWith("/action?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(8);
                        string user = ExtractQueryParam(query, "user");
                        string cmd = ExtractQueryParam(query, "cmd");

                        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(cmd))
                        {
                            _commandQueue.Enqueue(new UserChatCommand
                            {
                                Username = user,
                                CommandText = cmd,
                                IsCommand = true
                            });
                        }
                    }
                    else if (fullPath.StartsWith("/announce?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(10);
                        string msg = ExtractQueryParam(query, "msg");
                        string secStr = ExtractQueryParam(query, "sec");
                        float durationSec = 45f;
                        if (float.TryParse(secStr, out float parsedSec) && parsedSec > 0f)
                        {
                            durationSec = parsedSec;
                        }

                        if (!string.IsNullOrEmpty(msg))
                        {
                            _mainThreadActions.Enqueue(() =>
                            {
                                if (string.Equals(msg, "clear", StringComparison.OrdinalIgnoreCase))
                                {
                                    _latestAnnounceText = "";
                                    _announceExpireTime = 0f;
                                }
                                else
                                {
                                    _latestAnnounceText = $"<color=#FFD700><b>[DURCHSAGE]</b></color> <color=#FFFFFF><b>{msg.Replace("<", "").Replace(">", "")}</b></color>";
                                    _announceExpireTime = Time.time + durationSec;
                                }
                                UpdateTopHeaderOverlay();
                            });
                        }
                    }
                    else if (fullPath.StartsWith("/track?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(7);
                        string user = ExtractQueryParam(query, "user");
                        _mainThreadActions.Enqueue(() =>
                        {
                            if (string.IsNullOrEmpty(user) || string.Equals(user, "clear", StringComparison.OrdinalIgnoreCase) || string.Equals(_trackedUsername, user, StringComparison.OrdinalIgnoreCase))
                            {
                                _trackedUsername = "";
                            }
                            else
                            {
                                _trackedUsername = user;
                            }
                            UpdateTrackedViewerRadar();
                            RefreshDashboardStatusJson();
                        });
                    }
                    else if (fullPath.StartsWith("/demo?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(6);
                        string act = ExtractQueryParam(query, "act");
                        if (act == "start" || act == "toggle")
                        {
                            _mainThreadActions.Enqueue(() =>
                            {
                                LoadVodDemoData();
                                _vodDemoActive = true;
                                _vodDemoTimer = 0f;
                                _vodDemoIdx = 0;
                                try { _currentTcp?.Close(); } catch { }
                                ResetAllAssignments();
                                TwitchPlugin.Log?.LogInfo("[TwitchDemo] Freizeitgestoert VOD-Demo gestartet (Live-IRC pausiert, Pl?tze zurueckgesetzt)!");
                            });
                        }
                        else if (act == "stop")
                        {
                            _mainThreadActions.Enqueue(() =>
                            {
                                _vodDemoActive = false;
                                ResetAllAssignments();
                                TwitchPlugin.Log?.LogInfo("[TwitchDemo] Freizeitgestoert VOD-Demo gestoppt. Live-IRC wird wieder aktiviert.");
                            });
                        }
                    }
                    else if (fullPath.StartsWith("/simulate?", StringComparison.OrdinalIgnoreCase))
                    {
                        string query = fullPath.Substring(10);
                        string simUser = ExtractQueryParam(query, "user");
                        string simMsg = ExtractQueryParam(query, "msg");
                        if (string.IsNullOrEmpty(simUser))
                        {
                            simUser = TwitchPlugin.DebugStreamerName != null && !string.IsNullOrEmpty(TwitchPlugin.DebugStreamerName.Value)
                                ? TwitchPlugin.DebugStreamerName.Value
                                : (!string.IsNullOrEmpty(_currentChannel) ? _currentChannel : TwitchPlugin.ChannelName.Value);
                        }
                        if (!string.IsNullOrEmpty(simUser))
                        {
                            if (TwitchPlugin.DebugMode != null) TwitchPlugin.DebugMode.Value = true;
                            ViewerInfo simViewer = new ViewerInfo
                            {
                                Username = simUser.ToLowerInvariant().Trim(),
                                DisplayName = simUser.Trim(),
                                TwitchColorHex = "#FF2A2A",
                                Role = ViewerRole.Broadcaster,
                                IsSub = true,
                                IsFounder = true,
                                IsVip = true,
                                IsMod = true,
                                IsVerified = true,
                                IsBot = false,
                                SubMonths = 36,
                                SubTier = 3,
                                LastSeen = DateTime.UtcNow
                            };
                            _newViewersQueue.Enqueue(simViewer);
                            if (!string.IsNullOrEmpty(simMsg))
                            {
                                _commandQueue.Enqueue(new UserChatCommand
                                {
                                    Username = simViewer.Username,
                                    CommandText = simMsg,
                                    IsCommand = simMsg.StartsWith("!")
                                });
                            }
                        }
                    }

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(responseBody);
                    string header =
                        "HTTP/1.1 200 OK\r\n" +
                        $"Content-Type: {contentType}\r\n" +
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                        "Cache-Control: no-cache\r\n" +
                        $"Content-Length: {bodyBytes.Length}\r\n" +
                        "Connection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(bodyBytes, 0, bodyBytes.Length);
                }
            }
            catch { }
        }

        private static string ExtractQueryParam(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return "";
            string[] pairs = query.Split('&');
            foreach (string p in pairs)
            {
                int eq = p.IndexOf('=');
                if (eq > 0)
                {
                    string k = p.Substring(0, eq);
                    if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(p.Substring(eq + 1));
                    }
                }
            }
            return "";
        }

        private void ApplyLiveDashboardSetting(string key, string val)
        {
            if (string.IsNullOrEmpty(key)) return;
            bool boolVal = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1";

            switch (key)
            {
                case "Channel":
                    if (!string.IsNullOrEmpty(val))
                    {
                        string cleanChan = val.Trim().ToLowerInvariant().Replace("#", "");
                        TwitchPlugin.ChannelName.Value = cleanChan;
                        SetDesiredChannel(cleanChan);
                        _lastHeaderChannel = "";
                        UpdateTopHeaderOverlay();
                    }
                    break;
                case "ShowTopHeader":
                    TwitchPlugin.ShowTopHeader.Value = boolVal;
                    UpdateTopHeaderOverlay();
                    break;
                case "ShowNametags":
                    TwitchPlugin.ShowNametags.Value = boolVal;
                    foreach (var kvp in _peepNametags)
                    {
                        if (kvp.Value != null) kvp.Value.gameObject.SetActive(boolVal);
                    }
                    foreach (var kvp in _staffNametags)
                    {
                        if (kvp.Value != null) kvp.Value.gameObject.SetActive(boolVal);
                    }
                    break;
                case "ShowBadges":
                    TwitchPlugin.ShowBadges.Value = boolVal;
                    foreach (var kvp in _peepAssignments) RefreshViewerNametag(kvp.Value);
                    foreach (var kvp in _staffAssignments) RefreshViewerNametag(kvp.Value);
                    break;
                case "AssignModsToStaff":
                    TwitchPlugin.AssignModsToStaff.Value = boolVal;
                    UpdateNametagsAndAssignments();
                    break;
                case "AllowAllViewersAsStaff":
                    TwitchPlugin.AllowAllViewersAsStaff.Value = boolVal;
                    UpdateNametagsAndAssignments();
                    break;
                case "ExcludeBroadcaster":
                    TwitchPlugin.ExcludeBroadcaster.Value = boolVal;
                    break;
                case "DebugMode":
                    if (TwitchPlugin.DebugMode != null) TwitchPlugin.DebugMode.Value = boolVal;
                    break;
                case "DebugStreamerName":
                    if (TwitchPlugin.DebugStreamerName != null) TwitchPlugin.DebugStreamerName.Value = (val ?? "").Trim();
                    break;
                case "SpeechBubbleDuration":
                    if (float.TryParse(val, out float parsedBubbleSec) && parsedBubbleSec >= 5f)
                    {
                        TwitchPlugin.SpeechBubbleDuration.Value = parsedBubbleSec;
                    }
                    break;
                case "MaxVisibleNametags":
                    if (int.TryParse(val, out int parsedMaxTags) && parsedMaxTags >= 10)
                    {
                        TwitchPlugin.MaxVisibleNametags.Value = parsedMaxTags;
                    }
                    break;
                case "ViewerInactivityMinutes":
                    if (float.TryParse(val, out float parsedInactMin) && parsedInactMin >= 0f)
                    {
                        TwitchPlugin.ViewerInactivityMinutes.Value = parsedInactMin;
                    }
                    break;
            }

            try { TwitchPlugin.Instance?.Config?.Save(); } catch { }
            RefreshDashboardStatusJson();
        }

        private void IrcWorkerLoop()
        {
            while (_isRunning)
            {
                if (_vodDemoActive)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                string channel = _desiredChannel;
                if (string.IsNullOrEmpty(channel))
                {
                    channel = TwitchPlugin.ChannelName.Value.ToLowerInvariant().Trim();
                }

                if (string.IsNullOrEmpty(channel))
                {
                    Thread.Sleep(2000);
                    continue;
                }

                _currentChannel = channel;
                TcpClient tcp = null;
                SslStream ssl = null;
                StreamReader reader = null;
                StreamWriter writer = null;

                try
                {
                    TwitchPlugin.Log?.LogInfo($"[TwitchIRC] Verbinde mit Twitch Chat (#{channel})...");
                    tcp = new TcpClient("irc.chat.twitch.tv", 6697);
                    _currentTcp = tcp;
                    ssl = new SslStream(tcp.GetStream(), false);
                    ssl.AuthenticateAsClient("irc.chat.twitch.tv");

                    var utf8NoBom = new UTF8Encoding(false);
                    reader = new StreamReader(ssl, utf8NoBom);
                    writer = new StreamWriter(ssl, utf8NoBom) { AutoFlush = true };

                    string anonymousNick = "justinfan" + new System.Random().Next(10000, 99999);
                    writer.WriteLine("CAP REQ :twitch.tv/tags twitch.tv/commands");
                    writer.WriteLine("PASS SCHMOOPIIE");
                    writer.WriteLine("NICK " + anonymousNick);
                    writer.WriteLine("JOIN #" + channel);

                    TwitchPlugin.Log?.LogInfo($"[TwitchIRC] Erfolgreich im Twitch-Kanal #{channel} eingeloggt!");

                    string line;
                    while (_isRunning && (line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("PING"))
                        {
                            writer.WriteLine("PONG " + line.Substring(5));
                            continue;
                        }

                        if (line.Contains("PRIVMSG"))
                        {
                            ParseTwitchMessage(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning && string.Equals(_currentChannel, _desiredChannel, StringComparison.OrdinalIgnoreCase))
                    {
                        TwitchPlugin.Log?.LogWarning($"[TwitchIRC] Verbindungsfehler: {ex.Message}. Neuverbindung in 5 Sekunden...");
                        Thread.Sleep(5000);
                    }
                }
                finally
                {
                    _currentTcp = null;
                    try { writer?.Dispose(); } catch { }
                    try { reader?.Dispose(); } catch { }
                    try { ssl?.Dispose(); } catch { }
                    try { tcp?.Dispose(); } catch { }
                }
            }
        }

        private static string ExtractIrcTagValue(string tags, string key)
        {
            if (string.IsNullOrEmpty(tags)) return null;
            string search = key + "=";
            int idx = -1;
            if (tags.StartsWith(search, StringComparison.Ordinal))
            {
                idx = search.Length;
            }
            else
            {
                int semiSearch = tags.IndexOf(";" + search, StringComparison.Ordinal);
                if (semiSearch != -1)
                {
                    idx = semiSearch + 1 + search.Length;
                }
            }

            if (idx == -1) return null;

            int endIdx = tags.IndexOf(';', idx);
            if (endIdx == -1) return tags.Substring(idx);
            return tags.Substring(idx, endIdx - idx);
        }

        private void ParseTwitchMessage(string raw)
        {
            try
            {
                string tags = "";
                string user = "";
                string message = "";

                if (raw.StartsWith("@"))
                {
                    int spaceIdx = raw.IndexOf(' ');
                    tags = raw.Substring(1, spaceIdx - 1);
                    raw = raw.Substring(spaceIdx + 1);
                }

                int privMsgIdx = raw.IndexOf("PRIVMSG", StringComparison.Ordinal);
                if (privMsgIdx == -1) return;

                int colonIdx = raw.IndexOf(':', privMsgIdx);
                if (colonIdx != -1)
                {
                    message = raw.Substring(colonIdx + 1).Trim();
                }

                int exmarkIdx = raw.IndexOf('!');
                if (exmarkIdx != -1 && raw.StartsWith(":"))
                {
                    user = raw.Substring(1, exmarkIdx - 1);
                }

                if (string.IsNullOrEmpty(user)) return;

                string displayName = ExtractIrcTagValue(tags, "display-name");
                if (string.IsNullOrEmpty(displayName)) displayName = user;

                string colorHex = ExtractIrcTagValue(tags, "color");
                string badgeStr = ExtractIrcTagValue(tags, "badges") ?? "";
                string badgeInfoStr = ExtractIrcTagValue(tags, "badge-info") ?? "";
                string modTag = ExtractIrcTagValue(tags, "mod");
                string subTag = ExtractIrcTagValue(tags, "subscriber");
                string vipTag = ExtractIrcTagValue(tags, "vip");

                string debugCustomUser = TwitchPlugin.DebugStreamerName != null ? TwitchPlugin.DebugStreamerName.Value.Trim() : "";
                bool isDebugSelf = TwitchPlugin.DebugMode != null && TwitchPlugin.DebugMode.Value && !string.IsNullOrEmpty(debugCustomUser) && string.Equals(user, debugCustomUser, StringComparison.OrdinalIgnoreCase);
                bool isBroadcaster = badgeStr.Contains("broadcaster") || string.Equals(user, _currentChannel, StringComparison.OrdinalIgnoreCase) || isDebugSelf;
                bool isMod = modTag == "1" || badgeStr.Contains("moderator") || badgeStr.Contains("lead_moderator");
                bool isVip = vipTag == "1" || badgeStr.Contains("vip");
                bool isFounder = badgeStr.Contains("founder") || badgeInfoStr.Contains("founder");
                bool isSub = subTag == "1" || isFounder || badgeStr.Contains("subscriber") || badgeInfoStr.Contains("subscriber");
                bool isVerified = badgeStr.Contains("partner/") || badgeStr.Contains("verified") || badgeStr.Contains("ambassador");

                string lowerUser = user.ToLowerInvariant();
                bool isBot = badgeStr.Contains("bot-badge")
                    || lowerUser == "nightbot"
                    || lowerUser == "streamelements"
                    || lowerUser == "streamlabs"
                    || lowerUser == "moobot"
                    || lowerUser == "fossabot"
                    || lowerUser == "wizebot"
                    || lowerUser == "CommanderRoot".ToLowerInvariant()
                    || lowerUser == "sery_bot";

                int subMonths = 0;
                int subTier = 1;

                foreach (string prefix in new[] { "subscriber/", "founder/" })
                {
                    int infoIdx = badgeInfoStr.IndexOf(prefix, StringComparison.Ordinal);
                    if (infoIdx != -1)
                    {
                        string val = badgeInfoStr.Substring(infoIdx + prefix.Length);
                        int commaIdx = val.IndexOf(',');
                        if (commaIdx != -1) val = val.Substring(0, commaIdx);
                        if (int.TryParse(val, out int parsed) && parsed > 0)
                        {
                            subMonths = parsed;
                            break;
                        }
                    }
                }

                int subBadgeIdx = badgeStr.IndexOf("subscriber/", StringComparison.Ordinal);
                if (subBadgeIdx != -1)
                {
                    string subVal = badgeStr.Substring(subBadgeIdx + 11);
                    int commaIdx = subVal.IndexOf(',');
                    if (commaIdx != -1) subVal = subVal.Substring(0, commaIdx);
                    if (int.TryParse(subVal, out int badgeNum))
                    {
                        if (badgeNum >= 3000)
                        {
                            subTier = 3;
                            if (subMonths == 0) subMonths = badgeNum - 3000;
                        }
                        else if (badgeNum >= 2000)
                        {
                            subTier = 2;
                            if (subMonths == 0) subMonths = badgeNum - 2000;
                        }
                        else if (subMonths == 0)
                        {
                            subMonths = badgeNum;
                        }
                    }
                }

                ViewerRole role = ViewerRole.Guest;
                if (isBroadcaster) role = ViewerRole.Broadcaster;
                else if (isMod) role = ViewerRole.Moderator;
                else if (isVip) role = ViewerRole.VIP;
                else if (isFounder) role = ViewerRole.Founder;
                else if (isSub) role = ViewerRole.Subscriber;

                ViewerInfo viewer = new ViewerInfo
                {
                    Username = user,
                    DisplayName = displayName,
                    TwitchColorHex = colorHex,
                    Role = role,
                    IsSub = isSub,
                    IsFounder = isFounder,
                    IsVip = isVip,
                    IsMod = isMod,
                    IsVerified = isVerified,
                    IsBot = isBot,
                    SubMonths = subMonths,
                    SubTier = subTier,
                    LastSeen = DateTime.UtcNow
                };

                _newViewersQueue.Enqueue(viewer);

                if (!string.IsNullOrEmpty(message))
                {
                    _commandQueue.Enqueue(new UserChatCommand
                    {
                        Username = user,
                        CommandText = message,
                        IsCommand = message.StartsWith("!")
                    });
                }
            }
            catch (Exception ex)
            {
                TwitchPlugin.Log?.LogWarning("[TwitchIRC] Parse-Fehler: " + ex.Message);
            }
        }
    }
}
