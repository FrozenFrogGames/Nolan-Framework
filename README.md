<div align="center">
  <h1>NOLAN</h1>
  <p><strong>Narrative Oriented Language for game designer</strong></p>
  <img src="https://img.shields.io/badge/.NET-5.0+-512bd4?logo=dotnet" alt="Framework">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License">
  <img src="https://img.shields.io/badge/Status-Proof--of--Concept-orange" alt="Status">
</div>

---

## 📝 Introduction
**NOLAN** is a domain-specific language (DSL) designed to bridge the gap between narrative writing and game logic. It allows creators to manipulate sets of **gametags** within a scene using a strict syntax inspired by Ink.

The core philosophy is to maintain a human-readable format that shares the **ASCII-Art aesthetic** of Neo4j’s Cypher, making the relationship between interactable actors and world states intuitive.

## 🚀 Key Features
* **Portability:** Built on **Microsoft .NET** for cross-platform compatibility.
* **Flexible Output:** Parses text into **JSON** for runtime interpretation or direct code compilation.
* **CLI Ready:** Test your entire game logic in the terminal without launching a heavy engine.

## ⌨️ Syntax Preview

WORK IN PROGRESS

```nolan
== WORLD [CAVE, castle;dragon] [HOME, door;hero;king]
++ ()-[?hero]->() #HELLO
Hello World
++ ()-[?door<CAVE>]->()
++ (dragon<CAVE>)-{hero}[?door<CAVE>]->(dragon.fire<CAVE>;hero.kill<CAVE>) #GAMEOVER
<$GAMEOVER/>
++ (?hero)-[?king]->() #TALK
(?<Accepterez-vous ma quête preux chevalier?><Sauverez-vous le royaume mon ami?>)
-- (hero) Oui[.] votre majesté.
--> (hero.sword) THANK
-- [Peut-être?] Ça dépend, c'est quoi?
--- (!<Libérez le royaume du dragon.><Tuez la bête.>)
---- (hero) Oui[.] mais j'ai besoin d'une arme.
----- Prenez l'épée royale mon brave.
----> (hero.sword) THANK
---- Non[!], je suis contre la cruauté animale.
-- Non[!], merci.
++ (?king)-{hero.sword}[?castle<HOME>]->(hero.sword<HOME>)
++ ()-[?hero.sword]->() #QUEST
I Love King Quest
++ ()-[?castle<HOME>]->()
++ (dragon<CAVE>)-{hero.sword}[?door<CAVE>]->(dragon.kill<CAVE>;hero.sword<CAVE>) #HAPPYENDING
Tous les gens du royame vécurent heureux et eurent beaucoup d'enfants.<$GAMEOVER/>
++ (?hero.sword)-[?king]->() #THANK
Merci mon valeureux</>et bonne chance!
++ ()-{hero.sword}[king]->(hero.sword;king.kill) #BADENDING
Une ère de tyrannie s'abbattu sur le royaume pour plusieurs générations.<$GAMEOVER/>