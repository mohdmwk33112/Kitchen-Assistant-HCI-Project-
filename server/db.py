import sqlite3

name = "users.db"

def create_db():
    conn = sqlite3.connect(name)
    cursor = conn.cursor()

    cursor.execute("""CREATE TABLE IF NOT EXISTS cook(
    cook_id INTEGER PRIMARY KEY AUTOINCREMENT,
    cook_name TEXT NOT NULL,
    cook_address TEXT NOT NULL,
    expirence TEXT)""")
    conn.commit()
    conn.close()

def create_user(address, name, expirence, conn):
    cursor = conn.cursor()
    cursor.execute("insert into cook(cook_name, cook_address, expirence) values (?,?,?)",(name,address,expirence))
    conn.commit()
    conn.close()
