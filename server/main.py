import asyncio
from Bleak_Bluetooth import scan_bluetooth_devices
from conn import connect_to_server, receive_messages,HOST,PORT
from cooking_session import send_recipe
conn, _ = connect_to_server(HOST,PORT)

async def send_login(conn):
    cook = await scan_bluetooth_devices()

    if not cook:
        print("There is no device")
        return False

    msg = f"{cook[0]};{cook[1]};{cook[2]}"
    conn.sendall((msg + "\n").encode("utf-8"))
    return True

ok = asyncio.run(send_login(conn))

if ok:
    #start_gesture_detection(conn)
    while True:
        msg = receive_messages(conn)
        sl = msg.split(";")
        if sl[0] == "recipe_id":
            send_recipe(conn, sl)

else:
    conn.close()