# WINSANE - FUNKCIONÁLIS SPECIFIKÁCIÓ

**Projekt:** Winsane - Windows 11 Rendszeroptimalizáló

## 1. Általános Követelmények és Konfiguráció

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-1.1.1** | **Projekt célja** | Grafikus felületű eszköz Windows rendszer-optimalizáláshoz és diagnosztikához. | FS / Áttekintés |
| **FS-1.1.2** | **Fő ablak** | A főablaknak (`Winsane`) a `ctk.CTk` osztályból kell származnia, animált (fade-in) indítással. | `frontend/ui.py` |
| **FS-1.2.1** | **Mappaszerkezet** | Az alkalmazásnak a **`C:\Winsane`** mappát kell létrehoznia és használnia. | `backend/config.py` |
| **FS-1.2.2** | **Konfigurációs fájl** | A beállításokat a **`C:\Winsane\data.yaml`** fájlban kell tárolni. | `backend/config.py` |
| **FS-1.2.3** | **Config betöltés** | Indításkor a helyi és a távoli (GitHub) konfigurációt össze kell fésülni (`merge_configs`). | `init_config` |
| **FS-1.2.4** | **Config mentés** | Minden állapotváltozás (kapcsoló, téma) után azonnal menteni kell a fájlt. | `save_config` |

---

## 2. UI/UX Funkciók (Sidebar)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-2.1.1** | **Téma váltás** | "☼" gombbal váltás Light/Dark/System mód között. A választást menteni kell. | `toggle_theme` |
| **FS-2.1.2** | **Accent szín** | "🎨" gombbal egyéni szín választása, az UI azonnali frissítésével. | `pick_color` |
| **FS-2.2.1** | **Power Scheduler** | "⏻" gomb: Időzített leállítás (`-s`), újraindítás (`-r`) vagy BIOS (`-fw`) ablak (`PowerTimer`). | `frontend/ui.py` |
| **FS-2.3.1** | **PowerShell futtatás** | Minden tweak parancsot adminisztrátori jogosultsággal kell futtatni. | `run_powershell_as_admin` |
| **FS-2.3.2** | **Hibakezelés** | Sikertelen parancsvégrehajtás esetén hibaüzenetet kell megjeleníteni. | `backend/config.py` |

---

## 3. Optimalizáló (Optimizer) és Egyéni Tweakek

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-3.1.1** | **Megjelenítés** | Minden tweak külön soron (`TweakItemControl`), névvel és leírással jelenjen meg. | `frontend/ui.py` |
| **FS-3.1.2** | **Állapot** | A kapcsoló állása tükrözze a `data.yaml`-ben tárolt `enabled` értéket. | `TweakItemControl` |
| **FS-3.1.3** | **Végrehajtás** | Kapcsoláskor a megfelelő (True/False) PowerShell parancs lefut, az állapot mentődik. | `toggle_tweak` |
| **FS-3.2.1** | **User Tweak Űrlap** | A "User" fülön legyen űrlap (`AddTweakFrame`) új elemek hozzáadására. | `SubTabView` |
| **FS-3.2.2** | **Kötelező mezők** | Név, ON parancs, OFF parancs kötelező. Hiány esetén hibaüzenet. | `add_user_tweak` |
| **FS-3.2.3** | **Hozzáadás** | Sikeres hozzáadáskor bekerül az `items` listába és a config fájlba. | `AddTweakFrame.add_tweak` |
| **FS-3.2.4** | **UI Frissítés** | Az új elem azonnal jelenjen meg a listában, a "Nincs tweak" üzenet tűnjön el. | `frontend/ui.py` |
| **FS-3.2.5** | **Törlés gomb** | Csak a felhasználói (`is_user_tweak=True`) elemek mellett legyen törlés (Delete) gomb. | `TweakItemControl` |
| **FS-3.2.6** | **Megerősítés** | Törlés előtt felugró ablak (`messagebox.askyesno`) kérjen megerősítést. | `on_delete_press` |
| **FS-3.2.7** | **Eltávolítás** | Megerősítés után törlés az adatstruktúrából és a widget megsemmisítése. | `delete_user_tweak` |

---

## 4. Tisztító (Cleaner) és Alkalmazások (Apps)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-4.1.1** | **Kategóriák** | A Cleaner és Apps fülek tartalmát a `data.yaml`-ből, kategóriákra bontva kell betölteni. | `SubTabView` |
| **FS-4.1.2** | **Logika** | A Cleaner ugyanazt a `TweakItemControl` logikát használja, mint az Optimizer. | `frontend/ui.py` |
| **FS-5.1.2** | **Winget** | App telepítés/eltávolítás a `winget` parancs segítségével történik. | `run_powershell_as_admin` |

---

## 5. Képernyő Beállítások (Display)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-6.1.1** | **Vizualizáció** | `ctk.CTkCanvas` használata a monitorok elrendezésének kirajzolásához. | `DisplayFrame` |
| **FS-6.1.2** | **Adatok** | Monitorok pozíciójának és felbontásának valós idejű lekérdezése. | `get_monitor_layout` |
| **FS-6.1.3** | **Kiválasztás** | A vászonra kattintva a monitor kijelölhető, a beállítási panel frissül. | `select_monitor` |
| **FS-6.1.4** | **Módok listázása** | Az elérhető felbontások és Hz értékek listázása legördülő menüben. | `list_display_modes` |
| **FS-6.1.5** | **Alkalmazás** | "Apply" gomb: felbontás és frekvencia beállítása a választott monitoron. | `apply_settings` |
| **FS-6.1.6** | **Vetítési mód** | Külön menü a vetítési módok (Extend, Duplicate) váltására. | `set_projection_mode` |
| **FS-6.1.7** | **Pywin32 check** | Ha a `pywin32` hiányzik, a funkció tiltása és figyelmeztetés megjelenítése. | `is_available` |

---

## 6. Rendszer-irányítópult (Dashboard)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-7.1.1** | **Statikus adatok** | Indításkor egyszeri lekérés: CPU, Alaplap, RAM típus, OS, TPM. | `_get_static_info` |
| **FS-7.1.2** | **Dinamikus adatok** | 5 másodpercenként frissítés: CPU %, RAM %, GPU %, Lemez %. | `update_info` |
| **FS-7.1.3** | **Elrendezés** | A layout felépítése a `data.yaml` alapján (bal/jobb oszlop). | `InfoFrame` |
| **FS-7.1.4** | **RAM formátum** | Kijelzés: "XX% (Használt GB / Összes GB)". | `frontend/dashboard_frame.py` |
| **FS-7.1.5** | **Lemezhasználat** | Minden meghajtó (`C:\`, `D:\`) külön sorban, % és GB adatokkal. | `frontend/dashboard_frame.py` |
| **FS-7.1.6** | **GPU infó** | GPU név, VRAM és terhelés lekérése `GPUtil` vagy `wmi` segítségével. | `get_dynamic_data` |