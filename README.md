# FNFBot Continued
## I remade FNFBot and stuff cool haha funn-

Credits go to the original contributors of [FNFBot](https://github.com/Kade-github/FNFBot/tree/master):
- KadeDev
- PIjus
- Roy The Arsonist
- Maniues

### WARNING!
FNFBot doesn't detect when the song starts automatically, you must press your Start/Stop key yourself. In case notes aren't hitting on time, adjust the offset accordingly.

---

### What is FNFBot?
FNFBot is a bot that automatically plays through Friday Night Funkin' songs by parsing the chart from a chart JSON file and sending keystrokes.

---

### How do I use it?
1. Load the song chart's `.json` file with the **Load JSON** button.
2. Configure your keybinds, offset and humaniser settings. Settings will save automatically now.
3. Start the song inside the game, then press your Start/Stop key at the proper time.

---

### Keybinds
All keybinds can now be rebound easily by simply pressing any button in the **Keybinds** section. Press a key to bind to any function and your keybinds will automatically save to `config.json`.

| Keybind | Default | Description |
| ------- | ------- | ----------- |
| Start/Stop | F1 | Start or stop the bot |
| Offset+ | F2 | Increase offset by 5ms |
| Offset- | F3 | Decrease offset by 5ms |
| Left lane | ← | Arrow key left |
| Down lane | ↓ | Arrow key down |
| Up lane | ↑ | Arrow key up |
| Right lane | → | Arrow key right |

Press **Reset** to reset all keybinds to default.

---

### Settings

| Setting | Description |
| ------- | ----------- |
| Offset (ms) | Time offset for note playing (positive = later, negative = sooner)| |
| Dev Min / Max (ms) | New humaniser - Adds randomized time delay before hitting a note (0, 0 = perfect bot) |
| Miss % | Miss a certain percentage of notes |
| Tap Hold Min / Max (ms) | Sets how long tap notes should be pressed |

---

### Installation
Extract the .zip file and run `FNFBot.exe`. All .dll files included in the folder are required, and don't move the .exe outside of its folder.

---

### Notes
- 6k/9k support is probably not happening. Not soon at least.
- Only works with base game charts (standard 4-lane).
