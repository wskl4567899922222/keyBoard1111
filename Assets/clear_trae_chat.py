# -*- coding: utf-8 -*-
# 清理 Trae CN 各工作区 state.vscdb 中的会话缓存键
import sqlite3, glob, os

base = os.path.join(os.environ["APPDATA"], "Trae CN", "User")

global_patterns = [
    "chat-suggest:cache:%",
    "%revert:attachment-meta:v1:session:%",
    "all_session_badges_%",
]
ws_patterns = [
    "ai-chat.chatQueryCompletion.v2.%",
    "ai-chat.chatQueryCompletion.%",
]
always_delete = [
    "chat.ChatSessionStore.index",
    "ai-chat-v2.lastActiveSessionId",
]

def clean(db, pats):
    if not os.path.exists(db):
        return 0
    try:
        c = sqlite3.connect(db)
        cur = c.cursor()
        n = 0
        for p in pats:
            cur.execute("DELETE FROM ItemTable WHERE key LIKE ?", (p,))
            n += cur.rowcount
        q = "DELETE FROM ItemTable WHERE key IN (%s)" % ",".join("?" * len(always_delete))
        cur.execute(q, always_delete)
        n += cur.rowcount
        c.commit()
        c.close()
        return n
    except Exception as e:
        print("  skip:", db, e)
        return 0

total = clean(os.path.join(base, "globalStorage", "state.vscdb"), global_patterns)
for ws in glob.glob(os.path.join(base, "workspaceStorage", "*", "state.vscdb")):
    total += clean(ws, ws_patterns)
print("cleaned keys:", total)
