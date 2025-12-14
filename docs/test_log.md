# WINSANE - TESZTJEGYZŐKÖNYV

| TESZT ID | TESZT LEÍRÁSA | BEMENET / LÉPÉS | VÁRT EREDMÉNY | KAPOTT EREDMÉNY | OK? |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TC01** | **Alkalmazás indítása** | `main.py` futtatása internetkapcsolattal. | A program elindul, a konfigurációs fájl (`data.yaml`) létrejön a `C:\Winsane` mappában. | A mappa és a fájl sikeresen létrejött. | **OK** |
| **TC02** | **Téma váltás** | "☼" gomb megnyomása (Light/Dark). | A színek azonnal megváltoznak, a beállítás mentődik. | Megfelelő színek (Korábbi hiba javítva: BUG-001). | **OK** |
| **TC03** | **Optimalizáció (Tweak)** | "Optimizer" fül -> Kapcsoló átállítása. | A PowerShell parancs lefut, a rendszer beállítása módosul. | A parancs lefutott, hibaüzenet nem volt. | **OK** |
| **TC04** | **Saját Tweak - Hiányos** | "User" fül -> Csak a név kitöltése -> "Add". | Hibaüzenet jelenik meg, nem engedi menteni. | Hibaüzenet: "Minden mező kötelező". | **OK** |
| **TC05** | **Saját Tweak - Hozzáadás** | Minden mező (Név, ON, OFF) kitöltése -> "Add". | A tweak megjelenik a listában "Delete" gombbal. | A tweak megjelent és működik. | **OK** |
| **TC06** | **Tweak Törlése** | "Delete" gombra kattintás egy saját tweaknél. | Megerősítő ablak után a tweak eltűnik. | Sikeresen törölve a listából és a fájlból. | **OK** |
| **TC07** | **Monitor felismerés** | "Display" fül megnyitása. | A vásznon a monitorok helyes elrendezése látszik. | A vizualizáció helyes. | **OK** |
| **TC08** | **Felbontás váltás** | Monitor kiválasztása -> Új felbontás -> "Apply". | A monitor átvált az új felbontásra. | A váltás sikeres. | **OK** |
| **TC09** | **Dashboard frissítés** | "Dashboard" fül figyelése. | Az adatok (CPU, RAM) 5 mp-enként frissülnek. | Az értékek valós időben változnak. | **OK** |
| **TC10** | **App telepítés** | "Apps" fül -> Program kiválasztása -> Install. | A program települ, a felület nem fagy le. | Sikeres telepítés (Korábbi fagyás javítva: BUG-015). | **OK** |