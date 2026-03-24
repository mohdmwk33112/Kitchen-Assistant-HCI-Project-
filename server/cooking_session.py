import sqlite3
import socket
from conn import receive_messages, connect_to_server

def send_recipe(conn, list):

    connect = sqlite3.connect("users.db")
    cursor = connect.cursor()

    cursor.execute("SELECT recipe_name, description FROM recipes WHERE recipe_id = ?", (list[1],))
    recipe = cursor.fetchone()
    print(recipe)
    cursor.execute("select ingredient_name, quantity, unit from recipe_ingredients where recipe_id = ?",(list[1],))

    recipe_ingredients = cursor.fetchall()
    print(recipe_ingredients)
    cursor.execute("select step_number, step_instruction from recipe_steps where recipe_id = ?",(list[1],))

    recipe_steps = cursor.fetchall()
    print(recipe_steps)
    if recipe:
        msg = str(recipe[0]) + ";" + str(recipe[1] or "") + ";"

        for ing in recipe_ingredients:
            msg += str(ing[0]) + ";" + str(ing[1] or "") + ";" + str(ing[2] or "") + ";"

        conn.sendall(msg.encode("utf-8"))
    else:
        conn.sendall("Recipe not found".encode("utf-8"))

    confirm = receive_messages(conn)
    list = []
    list = confirm.split(";")
    if(list[0] == "recipe_id"):
        send_recipe(conn,list)
    elif(list[0] == "confirm"):
        send_steps(conn,recipe,recipe_steps)
        return
    else:
        return

def send_steps(conn,recipe,recipe_steps):

    msg = ""

    if recipe:
        for step in recipe_steps:
            msg += str(step[0]) + ";" + str(step[1] or "") + ";"

        conn.sendall(msg.encode("utf-8"))
    else:
        conn.sendall("Recipe not found".encode("utf-8"))
