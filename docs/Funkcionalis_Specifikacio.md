## 📝 Winsane - Funkcionális Specifikáció (Átdolgozott)

### 1. Általános Követelmények és Konfiguráció

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-1.1.1** | **Projekt neve és célja** | Winsane – Grafikus felületű eszköz Windows rendszer-optimalizáláshoz és rendszer-diagnosztikához. | FS / Áttekintés |
| **FS-1.1.2** | **Fő ablak** | A fő alkalmazás ablaknak (osztály: `Winsane`) a `ctk.CTk` osztályból kell származnia, és animált (fade-in) indítást kell biztosítania. | `frontend/ui.py` (`Winsane.__init__`) |
| **FS-1.2.1** | **Konfigurációs mappa** | Az alkalmazásnak létre kell hoznia és használnia kell a **`C:\Winsane`** mappát. | `backend/config.py` (`WINSANE_FOLDER`) |
| **FS-1.2.2** | **Konfigurációs fájl** | A beállításokat a **`C:\Winsane\data.yaml`** fájlban kell tárolnia. | `backend/config.py` (`DATA_FILE`) |
| **FS-1.2.3** | **Konfiguráció betöltése/frissítése** | Indításkor a helyi (`data.yaml`) és a távoli (GitHub) konfigurációt össze kell fésülnie a `merge_configs` logika alapján, majd az összefésült változatot el kell mentenie helyi fájlként. | `backend/config.py` (`init_config`, `merge_configs`) |
| **FS-1.2.4** | **Konfiguráció mentése** | Minden állapotváltozás (pl. kapcsoló átkattintása, téma, szín) után az egész konfigurációs adatstruktúrát el kell menteni a helyi fájlba. | `backend/config.py` (`save_config`) |

---

### 2. UI/UX Funkciók és Beállítások (Sidebar)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-2.1.1** | **Téma váltás** | A felhasználónak a "☼" gombbal képesnek kell lennie váltani a "Light", "Dark" és "System" témamódok között. A választást menteni kell a configban. | `frontend/ui.py` (`Winsane.toggle_theme`) |
| **FS-2.1.2** | **Másodlagos szín** | A felhasználónak a "🎨" gombbal képesnek kell lennie egyéni "Accent" színt választani (`getColor`). A választást menteni kell, és az UI-t azonnal frissíteni kell. | `frontend/ui.py` (`Winsane.pick_color`, `Winsane.refresh_accent`) |
| **FS-2.2.1** | **Power Scheduler** | A "⏻" gombnak meg kell nyitnia egy külön ablakot (`PowerTimer`), amely óra/perc/másodperc alapon időzített **leállítást (`-s`, `-f`)**, **újraindítást (`-r`, `-f`)** vagy **BIOS-ba való újraindítást (`-r`, `-fw`)** tesz lehetővé a `shutdown` paranccsal. | `frontend/ui.py` (`PowerTimer`, `PowerTimer._do`) |
| **FS-2.3.1** | **PowerShell végrehajtás** | Minden rendszer-tweakhez tartozó PowerShell parancsot a `run_powershell_as_admin` funkción keresztül kell végrehajtani, emelt jogosultsággal. | `backend/config.py` (`run_powershell_as_admin`) |
| **FS-2.3.2** | **Hibaüzenetek** | Sikertelen parancsvégrehajtás esetén hibaüzenetet kell megjeleníteni a felhasználónak. | `backend/config.py` (`run_powershell_as_admin`) |

---

### 3. Fő Fül: Optimalizáló (Optimizer)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-3.1.1** | **Tweak megjelenítés** | Minden tweaknek külön soron kell megjelennie (`TweakItemControl`), mutatva a nevét, leírását, és egy kapcsolót. | `frontend/ui.py` (`TweakItemControl`) |
| **FS-3.1.2** | **Állapot kezelés** | A kapcsoló állásának tükröznie kell a `data.yaml`-ben tárolt `enabled` állapotot. | `frontend/ui.py` (`TweakItemControl.__init__`) |
| **FS-3.1.3** | **Tweak végrehajtás** | A kapcsoló átkattintásakor végre kell hajtani a konfigurációban meghatározott megfelelő PowerShell parancsot (`True` vagy `False` kulcs alatt), és menteni kell az új állapotot a `data.yaml`-be. | `frontend/ui.py` (`TweakItemControl.toggle_tweak`) |

#### 3.2. Egyéni Tweakek (User Tab)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-3.2.1** | **Űrlap biztosítása** | A "User" fülnek tartalmaznia kell egy **`AddTweakFrame`** űrlapot az egyéni tweakek hozzáadásához. | `frontend/ui.py` (`SubTabView`) |
| **FS-3.2.2** | **Kötelező mezők** | A **"Tweak Name"**, a **"PowerShell (ON)"** és a **"PowerShell (OFF)"** parancsok kötelezők. Hibaüzenet jelenik meg, ha hiányoznak. | `backend/config.py` (`add_user_tweak`) |
| **FS-3.2.3** | **Hozzáadás** | A `add_user_tweak` hívása hozzáadja az új tweaket az `Optimizer -> User` kategória `items` listájához és elmenti a konfigurációt. | `frontend/ui.py` (`AddTweakFrame.add_tweak`) |
| **FS-3.2.4** | **Dinamikus UI frissítés** | Sikeres hozzáadás után az új tweaknek azonnal meg kell jelennie a görgethető listában (UI), és ha volt "Nincs tweak" üzenet, azt el kell távolítani. | `frontend/ui.py` (`AddTweakFrame.add_tweak`) |
| **FS-3.2.5** | **Törlés gomb** | Minden "User" fülön lévő tweak mellett meg kell jelennie egy "Törlés" (🗑️) gombnak (`is_user_tweak=True`). | `frontend/ui.py` (`TweakItemControl.__init__`) |
| **FS-3.2.6** | **Törlés megerősítés** | A "Törlés" gombra kattintva egy **megerősítő párbeszédablakot** (`messagebox.askyesno`) kell megjeleníteni. | `frontend/ui.py` (`TweakItemControl.on_delete_press`) |
| **FS-3.2.7** | **Tweak eltávolítása** | Megerősítés után a `delete_user_tweak` funkció eltávolítja a tweaket a config adatstruktúrából, majd a widget megsemmisül (`self.destroy()`), a config mentésre kerül. | `frontend/ui.py` (`TweakItemControl.on_delete_press`) |
| **FS-3.2.8** | **Beépített tweakek** | A beépített (nem "User") tweakek mellett nem jelenhet meg törlés gomb (`is_user_tweak=False`). | `frontend/ui.py` (`TweakItemControl.__init__`) |

---

### 4. Fő Fül: Cleaner (Tisztító)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-4.1.1** | **Kategóriák** | A Cleaner fülnek a `data.yaml` alapján kell betöltenie a kategóriákat (pl. `Junk Files`, `Browser`), és minden kategóriát külön al-fülön kell megjeleníteni. | `frontend/ui.py` (`SubTabView`) |
| **FS-4.1.2** | **Tweak logika** | A Cleaner tweakek ugyanazt a `TweakItemControl` logikát használják a kapcsoló és a PowerShell parancs végrehajtásához, mint az Optimizer. | `frontend/ui.py` (`SubTabView`) |

---

### 5. Fő Fül: Apps (Alkalmazások)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-5.1.1** | **Kategóriák** | Az Apps fülnek a `data.yaml` alapján kell betöltenie a kategóriákat (pl. `Browsers`, `Communication`, `Development` stb.). | `frontend/ui.py` (`SubTabView`) |
| **FS-5.1.2** | **Winget parancsok** | Az alkalmazások telepítése (`True` parancs) és eltávolítása (`False` parancs) a **`winget`** parancsok futtatásával történik, PowerShell-en keresztül. | `backend/config.py` (`run_powershell_as_admin`) |

---

### 6. Fő Fül: Display (Képernyő beállítások)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-6.1.1** | **Vizualizáció** | A Display fülnek tartalmaznia kell egy vásznat (`ctk.CTkCanvas`) a csatlakoztatott monitorok grafikus elrendezésének megjelenítésére. | `frontend/display_frame.py` (`DisplayFrame`) |
| **FS-6.1.2** | **Monitor adatok** | A monitoroknak valós időben be kell tölteniük az elrendezésüket (pozíció, felbontás) a `DisplayManager.get_monitor_layout` segítségével. | `backend/display_manager.py` (`get_monitor_layout`) |
| **FS-6.1.3** | **Monitor kiválasztás** | A vászonra kattintva ki kell választani az adott monitort, ami frissíti a beállítási panelt. | `frontend/display_frame.py` (`select_monitor`) |
| **FS-6.1.4** | **Felbontás/Frekvencia** | A kiválasztott monitorhoz elérhető felbontásokat és képfrissítési rátákat (`list_display_modes`) legördülő menüben kell listázni. | `frontend/display_frame.py` (`on_resolution_change`) |
| **FS-6.1.5** | **Beállítás alkalmazása** | Az "Apply" gombra kattintva a `DisplayManager.apply_settings` funkcióval kell a felbontást és a frekvenciát beállítani a kiválasztott monitoron. | `frontend/display_frame.py` (`apply_settings`) |
| **FS-6.1.6** | **Vetítési mód** | Külön menüpontnak kell lennie a vetítési módok (`Extend`, `Duplicate` stb.) váltására, a `DisplayManager.set_projection_mode` használatával. | `frontend/display_frame.py` (`on_projection_change`) |
| **FS-6.1.7** | **Pywin32 ellenőrzés** | Ellenőrizni kell a `pywin32` elérhetőségét. Ha nem elérhető, a funkciót le kell tiltani, és erről üzenetet kell megjeleníteni. | `backend/display_manager.py` (`is_available`) |

---

### 7. Fő Fül: Rendszer-irányítópult (Dashboard)

| ID | Követelmény | Leírás | Forrás / Függvény |
| :--- | :--- | :--- | :--- |
| **FS-7.1.1** | **Statikus adatok** | A program indításakor be kell tölteni a statikus rendszeradatokat (Alaplap, CPU, RAM sebesség, OS adatok, TPM/Secure Boot állapot), amelyek a `SystemInfoManager._get_static_info` hívásakor egyszer kerülnek lekérésre. | `backend/dashboard_manager.py` (`_get_static_info`) |
| **FS-7.1.2** | **Dinamikus adatok** | A dinamikus rendszeradatokat (CPU terheltség, RAM használat, GPU terheltség/memória, Lemezhasználat) **5 másodpercenként** kell frissíteni (`update_info`) a `SystemInfoManager.get_dynamic_data` meghívásával. | `frontend/dashboard_frame.py` (`update_info`) |
| **FS-7.1.3** | **Adat megjelenítés** | A Dashboard elrendezését a `data.yaml` `layout` szekciója alapján kell felépíteni (`InfoFrame` osztály), két oszlopot (`left`, `right`) használva. | `frontend/dashboard_frame.py` (`InfoFrame.__init__`) |
| **FS-7.1.4** | **RAM formátum** | A RAM használatot **százalékban, használt GB-ban és összes GB-ban** is meg kell jeleníteni (pl. "50.0% (8.0 GB / 16.0 GB)"). | `frontend/dashboard_frame.py` (`update_info`) |
| **FS-7.1.5** | **Lemezhasználat** | Minden csatlakoztatott meghajtóhoz (`C:\`, `D:\`) külön soron kell megjeleníteni a **százalékos terheltséget, a használt GB-ot és az összes GB-ot**. | `frontend/dashboard_frame.py` (`update_info`) |
| **FS-7.1.6** | **GPU információ** | A GPU adatokat (Név, VRAM, Terheltség) a `GPUtil` (ha elérhető) és `wmi` (ha elérhető) segítségével kell lekérni. Ha nem érhető el, erről üzenetet kell megjeleníteni. | `backend/dashboard_manager.py` (`get_dynamic_data`) |