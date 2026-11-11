import asyncio
import os
import sys
import socket
import urllib.parse
import urllib.request

# FORCE IPv4 to completely bypass IPv6 rate-limits!
orig_getaddrinfo = socket.getaddrinfo
def getaddrinfo_v4(host, port, family=0, type=0, proto=0, flags=0):
    return orig_getaddrinfo(host, port, socket.AF_INET, type, proto, flags)
socket.getaddrinfo = getaddrinfo_v4

from TikTokLive import TikTokLiveClient
from TikTokLive.events import ConnectEvent, CommentEvent, DisconnectEvent

# Ensure Windows console doesn't crash on emojis (cp1252 fix)
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

target_user = sys.argv[1] if len(sys.argv) > 1 else "freizeitgestoert"
target_user = target_user.replace("@", "").strip()

print(f"[BRIDGE] Initialisiere TikTok Live Bridge fuer @{target_user} (IPv4 Direct Mode)...", flush=True)

client = TikTokLiveClient(unique_id=target_user)

@client.on(ConnectEvent)
async def on_connect(event: ConnectEvent):
    print(f"[BRIDGE] >>> ERFOLGREICH MIT TIKTOK LIVE VERBUNDEN! (@{target_user}, Room: {client.room_id}) <<<", flush=True)

@client.on(CommentEvent)
async def on_comment(event: CommentEvent):
    try:
        nick = getattr(event.user, "nickname", None)
        u_name = getattr(event.user, "unique_id", None) or getattr(event.user, "display_id", None) or getattr(event.user, "user_id", "TikTokUser")

        if not nick:
            nick = u_name

        comment = event.comment

        safe_nick = str(nick).encode("ascii", "replace").decode("ascii")
        safe_user = str(u_name).encode("ascii", "replace").decode("ascii")
        safe_msg = str(comment).encode("ascii", "replace").decode("ascii")
        print(f"[TIKTOK-CHAT] {safe_nick} (@{safe_user}): {safe_msg}", flush=True)

        u_enc = urllib.parse.quote(str(u_name))
        n_enc = urllib.parse.quote(str(nick))
        msg_enc = urllib.parse.quote(str(comment))
        url = f"http://127.0.0.1:48888/simulate_tiktok?user={u_enc}&nick={n_enc}&msg={msg_enc}"
        urllib.request.urlopen(url, timeout=2)
    except Exception:
        pass

@client.on(DisconnectEvent)
async def on_disconnect(event: DisconnectEvent):
    print("[BRIDGE] TikTok Live getrennt. Versuche Neuverbindung...", flush=True)

async def main():
    while True:
        try:
            print(f"[BRIDGE] Starte Verbindung zu @{target_user}...", flush=True)
            await client.start()
        except Exception as e:
            err_str = str(e)
            print(f"[BRIDGE] Status: {err_str}", flush=True)
            if "rate_limit" in err_str.lower():
                print("[BRIDGE] Sign-Server Rate-Limit. Warte 30s...", flush=True)
                await asyncio.sleep(30)
            else:
                await asyncio.sleep(10)

if __name__ == "__main__":
    asyncio.run(main())
