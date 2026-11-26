#!/usr/bin/env python3
"""MYG Electrical yerleşik cihaz kütüphanesi üreticisi.

Çıktı: src/MYG.Electrical.Symbols/Devices/devices.json (uygulamaya gömülü).

Cihaz kütüphanesi sembol kütüphanesinden ayrıdır: bir cihaz, ana sembolü, ek parça sembolleri, kontak kapasitesi
ve ticari/elektriksel bilgileriyle tanımlanır. Buradaki kayıtlar GENEL şablonlardır: üretici, model ve sipariş
numarası bilerek boş bırakılmıştır (uydurma ürün verisi olmasın diye). Kullanıcı kendi ürünlerini
%AppData%\\MYG.Electrical\\Devices\\*.json dosyasına ekler veya projede cihaz özelliklerinden girer.

Kullanım:  python3 tools/symbolgen/devices.py
"""
import json
import os

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "src", "MYG.Electrical.Symbols", "Devices", "devices.json")
VERSION = "1.0.0"


def contacts(*spec):
    """("no","13","14"), ("co","11","12","14"), ("power","1","2") ..."""
    kinds = {"no": "no", "nc": "nc", "co": "changeover", "power": "power"}
    return [{"kind": kinds[s[0]], "pins": list(s[1:])} for s in spec]


POWER3 = [("power", "1", "2"), ("power", "3", "4"), ("power", "5", "6")]


def dev(id, name, category, symbol, props=None, parts=(), cts=(), keywords=(), description="", manufacturer=""):
    d = {"id": id, "name": name, "category": category, "symbol": symbol}
    if manufacturer:
        d["manufacturer"] = manufacturer
    if description:
        d["description"] = description
    if keywords:
        d["keywords"] = list(keywords)
    if parts:
        d["parts"] = list(parts)
    if cts:
        d["contacts"] = contacts(*cts)
    if props:
        d["properties"] = props
    return d


K = "Kontaktör ve Röle"
P = "Koruma"
M = "Motor ve Sürücü"
A = "PLC ve Otomasyon"
B = "Sensör ve Saha"
S = "Kumanda ve Sinyal"
X = "Klemens ve Konnektör"
E = "Ölçüm"
G = "Enerji"

DEVICES = [
    # ---- Kontaktör ve röle
    dev("dev.contactor.9a.24vdc", "Kontaktör 9 A, 3P, 24 VDC bobin, 1NO+1NC", K + "/Kontaktörler", "iec.coil",
        {"Bobin gerilimi": "24VDC", "AC-3 akım": "9A", "AC-3 güç (400V)": "4kW"},
        parts=["iec.contactor.3p", "iec.contact.no", "iec.contact.nc"],
        cts=[*POWER3, ("no", "13", "14"), ("nc", "21", "22")], keywords=["kontaktör", "9A", "4kW"]),
    dev("dev.contactor.18a.24vdc", "Kontaktör 18 A, 3P, 24 VDC bobin, 1NO+1NC", K + "/Kontaktörler", "iec.coil",
        {"Bobin gerilimi": "24VDC", "AC-3 akım": "18A", "AC-3 güç (400V)": "7.5kW"},
        parts=["iec.contactor.3p", "iec.contact.no", "iec.contact.nc"],
        cts=[*POWER3, ("no", "13", "14"), ("nc", "21", "22")], keywords=["kontaktör", "18A", "7.5kW"]),
    dev("dev.contactor.50a.24vdc", "Kontaktör 50 A, 3P, 24 VDC bobin, 1NO+1NC", K + "/Kontaktörler", "iec.coil",
        {"Bobin gerilimi": "24VDC", "AC-3 akım": "50A", "AC-3 güç (400V)": "22kW"},
        parts=["iec.contactor.3p", "iec.contact.no", "iec.contact.nc"],
        cts=[*POWER3, ("no", "13", "14"), ("nc", "21", "22")], keywords=["kontaktör", "50A", "22kW"]),
    dev("dev.contactor.9a.230vac", "Kontaktör 9 A, 3P, 230 VAC bobin, 1NO+1NC", K + "/Kontaktörler", "iec.coil",
        {"Bobin gerilimi": "230VAC", "AC-3 akım": "9A", "AC-3 güç (400V)": "4kW"},
        parts=["iec.contactor.3p", "iec.contact.no", "iec.contact.nc"],
        cts=[*POWER3, ("no", "13", "14"), ("nc", "21", "22")], keywords=["kontaktör", "230V"]),
    dev("dev.auxcontactor.4no", "Yardımcı kontaktör 24 VDC, 4NO", K + "/Kontaktörler", "iec.coil",
        {"Bobin gerilimi": "24VDC"}, parts=["iec.contact.no"],
        cts=[("no", "13", "14"), ("no", "23", "24"), ("no", "33", "34"), ("no", "43", "44")], keywords=["yardımcı kontaktör"]),
    dev("dev.relay.2co.24vdc", "Ara röle 24 VDC, 2CO (soketli)", K + "/Röleler", "iec.coil",
        {"Bobin gerilimi": "24VDC", "Kontak akımı": "8A"}, parts=["iec.contact.co"],
        cts=[("co", "11", "12", "14"), ("co", "21", "22", "24")], keywords=["röle", "interface", "2CO"]),
    dev("dev.relay.1co.24vdc", "Ara röle 24 VDC, 1CO (ince tip)", K + "/Röleler", "iec.coil",
        {"Bobin gerilimi": "24VDC", "Kontak akımı": "6A"}, parts=["iec.contact.co"],
        cts=[("co", "11", "12", "14")], keywords=["röle", "ince röle", "1CO"]),
    dev("dev.timer.ondelay", "Zaman rölesi, çekmede gecikmeli, 24 VDC, 1CO", K + "/Zaman röleleri", "iec.coil.ondelay",
        {"Bobin gerilimi": "24VDC", "Süre aralığı": "0.1s-10dk"}, parts=["iec.contact.no.ondelay", "iec.contact.nc.ondelay"],
        cts=[("co", "15", "16", "18")], keywords=["zaman rölesi", "timer", "TON"]),
    dev("dev.relay.phase", "Faz sırası ve faz kontrol rölesi, 400 VAC", K + "/İzleme röleleri", "iec.relay.phase",
        {"Gerilim": "3x400VAC"}, keywords=["faz sırası", "faz koruma"]),
    dev("dev.relay.safety", "Güvenlik rölesi 24 VDC, 2NO, acil stop", K + "/Güvenlik", "iec.relay.safety",
        {"Besleme": "24VDC", "Kategori / PL": ""}, keywords=["güvenlik rölesi", "acil stop"]),
    dev("dev.ssr.25a", "Katı hal rölesi 25 A, 3-32 VDC kontrol", K + "/Röleler", "iec.relay.ssr",
        {"Yük akımı": "25A", "Kontrol gerilimi": "3-32VDC"}, keywords=["SSR", "rezistans"]),

    # ---- Koruma
    *[dev(f"dev.mcb.{c.lower()}{a}.{p}p", f"Otomatik sigorta {c}{a}, {p}P", P + "/Otomatik sigortalar",
          {1: "iec.mcb.1p", 2: "iec.mcb.2p", 3: "iec.mcb", 4: "iec.mcb.4p"}[p],
          {"Anma akımı": f"{a}A", "Karakteristik": c, "Kesme kapasitesi": "6kA"}, keywords=["MCB", f"{c}{a}"])
      for c, a, p in [("C", 6, 1), ("C", 10, 1), ("C", 16, 1), ("B", 16, 1), ("C", 2, 1), ("C", 16, 3), ("C", 32, 3), ("C", 63, 3), ("C", 25, 4)]],
    dev("dev.mpcb.9-14", "Motor koruma şalteri 9-14 A", P + "/Motor koruma", "iec.mpcb.3p", {"Ayar aralığı": "9-14A"},
        keywords=["motor koruma", "MPCB", "5.5kW"]),
    dev("dev.mpcb.4-6.3", "Motor koruma şalteri 4-6.3 A", P + "/Motor koruma", "iec.mpcb.3p", {"Ayar aralığı": "4-6.3A"},
        keywords=["motor koruma", "MPCB", "2.2kW"]),
    dev("dev.mpcb.37-50", "Motor koruma şalteri 37-50 A", P + "/Motor koruma", "iec.mpcb.3p", {"Ayar aralığı": "37-50A"},
        keywords=["motor koruma", "MPCB", "22kW"]),
    dev("dev.overload.9-13", "Termik röle 9-13 A, 1NO+1NC", P + "/Motor koruma", "iec.overload.3p", {"Ayar aralığı": "9-13A"},
        parts=["iec.overload.contact.nc", "iec.overload.contact.no"], cts=[("nc", "95", "96"), ("no", "97", "98")],
        keywords=["termik"]),
    dev("dev.rcd.4p.40a.300ma", "Kaçak akım rölesi 4P 40 A 300 mA", P + "/Kaçak akım", "iec.rcd.4p",
        {"Anma akımı": "40A", "Kaçak akım": "300mA"}, keywords=["RCD", "kaçak akım"]),
    dev("dev.rcd.2p.25a.30ma", "Kaçak akım rölesi 2P 25 A 30 mA", P + "/Kaçak akım", "iec.rcd.2p",
        {"Anma akımı": "25A", "Kaçak akım": "30mA"}, keywords=["RCD", "kaçak akım", "priz"]),
    dev("dev.spd.1p", "Parafudr Tip 2, 275 V", P + "/Aşırı gerilim", "iec.surge.arrester", {"Uc": "275V", "Tip": "2"},
        keywords=["parafudr", "SPD"]),
    dev("dev.mainswitch.63a", "Yük ayırıcı / ana şalter 3P 63 A", P + "/Ayırıcılar", "iec.disconnector.3p", {"Anma akımı": "63A"},
        keywords=["ana şalter", "pako"]),

    # ---- Motor ve sürücü
    dev("dev.vfd.22kw", "Frekans inverteri 22 kW, 3x400 V", M + "/Sürücüler", "iec.vfd.3ph",
        {"Güç": "22kW", "Giriş gerilimi": "3x380-480VAC", "Çıkış akımı": "45A"}, parts=["iec.vfd.control", "iec.vfd.comm"],
        cts=[("co", "RC", "RB", "RA")], keywords=["VFD", "inverter", "22kW", "sürücü"]),
    dev("dev.vfd.2.2kw.1ph", "Frekans inverteri 2.2 kW, 1x230 V giriş", M + "/Sürücüler", "iec.vfd.1ph",
        {"Güç": "2.2kW", "Giriş gerilimi": "1x220-240VAC", "Çıkış akımı": "10A"}, parts=["iec.vfd.control", "iec.vfd.comm"],
        cts=[("co", "RC", "RB", "RA")], keywords=["VFD", "inverter", "2kW", "monofaze"]),
    dev("dev.motor.22kw", "Asenkron motor 22 kW, 400 V, 1500 d/d", M + "/Motorlar", "iec.motor.3ph.pe",
        {"Güç": "22kW", "Gerilim": "400V Δ", "Akım": "41A", "Devir": "1470 d/d"}, keywords=["motor", "22kW"]),
    dev("dev.motor.2.2kw", "Asenkron motor 2.2 kW, 230/400 V, 1500 d/d", M + "/Motorlar", "iec.motor.3ph.pe",
        {"Güç": "2.2kW", "Gerilim": "230V Δ / 400V Y", "Akım": "4.9A", "Devir": "1430 d/d"}, keywords=["motor", "2.2kW"]),
    dev("dev.softstarter.22kw", "Yumuşak yolverici 22 kW", M + "/Sürücüler", "iec.softstarter", {"Güç": "22kW"},
        keywords=["soft starter"]),

    # ---- PLC ve otomasyon
    dev("dev.plc.cpu", "PLC CPU, 24 VDC, Ethernet + RS485", A + "/PLC", "iec.plc.cpu", {"Besleme": "24VDC"},
        parts=["iec.plc.di8", "iec.plc.do8", "iec.plc.input", "iec.plc.output"], keywords=["PLC", "CPU"]),
    dev("dev.plc.di8", "Dijital giriş modülü 8 DI, 24 VDC", A + "/G-Ç modülleri", "iec.plc.di8", {"Giriş tipi": "24VDC sink/source"},
        parts=["iec.plc.input"], keywords=["DI", "giriş modülü"]),
    dev("dev.plc.do8", "Dijital çıkış modülü 8 DO, transistör 0.5 A", A + "/G-Ç modülleri", "iec.plc.do8", {"Çıkış tipi": "Transistör 24VDC 0.5A"},
        parts=["iec.plc.output"], keywords=["DO", "çıkış modülü"]),
    dev("dev.plc.ai4", "Analog giriş modülü 4 AI, 4-20 mA / 0-10 V", A + "/G-Ç modülleri", "iec.plc.ai4", {"Giriş": "4-20mA / 0-10V"},
        keywords=["AI", "analog"]),
    dev("dev.hmi.delta.7", "Dokunmatik operatör paneli 7\" (Delta DOP serisi)", A + "/HMI", "iec.hmi",
        {"Ekran": "7\"", "Besleme": "24VDC", "Haberleşme": "Ethernet, RS485"}, manufacturer="Delta",
        keywords=["HMI", "Delta", "DOP", "operatör paneli"]),
    dev("dev.switch.5p", "Endüstriyel Ethernet switch, 5 port", A + "/Haberleşme", "iec.eth.switch", {"Besleme": "24VDC"},
        keywords=["switch", "ethernet"]),

    # ---- Sensör ve saha
    dev("dev.prox.m18.pnp", "Endüktif sensör M18, PNP NO, 8 mm", B + "/Yaklaşım sensörleri", "iec.sensor.pnp",
        {"Algılama mesafesi": "8mm", "Çıkış": "PNP NO", "Besleme": "10-30VDC"}, keywords=["endüktif", "M18"]),
    dev("dev.prox.m12.pnp", "Endüktif sensör M12, PNP NO, 4 mm", B + "/Yaklaşım sensörleri", "iec.sensor.pnp",
        {"Algılama mesafesi": "4mm", "Çıkış": "PNP NO", "Besleme": "10-30VDC"}, keywords=["endüktif", "M12"]),
    dev("dev.pressure.switch", "Basınç şalteri (presostat), 1NO", B + "/Basınç", "iec.pressureswitch.no",
        {"Ayar aralığı": ""}, keywords=["presostat", "basınç"]),
    dev("dev.pressure.transmitter", "Basınç transmitteri 0-10 bar, 4-20 mA", B + "/Transmitterler", "iec.transmitter",
        {"Ölçüm aralığı": "0-10bar", "Çıkış": "4-20mA"}, keywords=["transmitter", "basınç"]),
    dev("dev.door.reed", "Manyetik kapı sensörü, 1NO", B + "/Kapı ve emniyet", "iec.door.sensor", keywords=["kapı", "reed"]),
    dev("dev.door.safety", "Emniyet kapı switchi, zorlamalı açma 1NC", B + "/Kapı ve emniyet", "iec.safety.switch",
        keywords=["emniyet", "kapı switchi"]),
    dev("dev.pt100.3w", "PT100 sıcaklık sensörü, 3 telli", B + "/Sıcaklık", "iec.pt100.3w", {"Ölçüm aralığı": "-50..+400°C"},
        keywords=["PT100"]),

    # ---- Kumanda ve sinyal
    dev("dev.button.start", "Start butonu, yeşil, 1NO", S + "/Butonlar", "iec.pushbutton.no", {"Renk": "Yeşil"}, keywords=["start"]),
    dev("dev.button.stop", "Stop butonu, kırmızı, 1NC", S + "/Butonlar", "iec.pushbutton.nc", {"Renk": "Kırmızı"}, keywords=["stop"]),
    dev("dev.button.estop", "Acil stop butonu, mantar, 1NC", S + "/Butonlar", "iec.estop", {"Renk": "Kırmızı/Sarı"}, keywords=["acil stop"]),
    dev("dev.selector.2pos", "Seçici şalter 2 konum, 1NO", S + "/Seçici şalterler", "iec.selector.2pos", keywords=["seçici", "0-1"]),
    dev("dev.lamp.green.24v", "Sinyal lambası LED 24 V, yeşil", S + "/Sinyal lambaları", "iec.lamp", {"Renk": "Yeşil", "Gerilim": "24V"},
        keywords=["lamba", "çalışıyor"]),
    dev("dev.lamp.red.24v", "Sinyal lambası LED 24 V, kırmızı", S + "/Sinyal lambaları", "iec.lamp", {"Renk": "Kırmızı", "Gerilim": "24V"},
        keywords=["lamba", "arıza"]),

    # ---- Klemens
    dev("dev.terminal.2.5", "Sıra klemens 2.5 mm²", X + "/Klemensler", "iec.terminal", {"Kesit": "2.5mm²"}, keywords=["klemens"]),
    dev("dev.terminal.pe.2.5", "PE klemens 2.5 mm²", X + "/Klemensler", "iec.terminal.pe", {"Kesit": "2.5mm²"}, keywords=["PE klemens"]),
    dev("dev.terminal.fused", "Sigortalı klemens 5x20", X + "/Klemensler", "iec.terminal.fused", {"Sigorta": "5x20"}, keywords=["sigortalı"]),

    # ---- Ölçüm
    dev("dev.analyzer.modbus", "Enerji analizörü, 3 faz, Modbus RTU", E + "/Analizörler", "iec.poweranalyzer",
        {"Besleme": "85-265VAC", "Akım girişi": "x/5A", "Haberleşme": "RS485 Modbus RTU"}, keywords=["analizör", "Modbus"]),
    *[dev(f"dev.ct.{r}", f"Akım trafosu {r}/5 A", E + "/Akım trafoları", "iec.ct", {"Oran": f"{r}/5A", "Sınıf": "0.5"},
          keywords=["CT", "akım trafosu"]) for r in (50, 100, 250)],

    # ---- Enerji
    dev("dev.psu.24v.5a", "Güç kaynağı 24 VDC 5 A", G + "/Güç kaynakları", "iec.psu.24vdc", {"Çıkış": "24VDC", "Akım": "5A", "Giriş": "100-240VAC"},
        keywords=["SMPS", "24V"]),
    dev("dev.psu.24v.10a", "Güç kaynağı 24 VDC 10 A", G + "/Güç kaynakları", "iec.psu.24vdc", {"Çıkış": "24VDC", "Akım": "10A", "Giriş": "100-240VAC"},
        keywords=["SMPS", "24V"]),
    dev("dev.dist.24v", "24 VDC sigortalı dağıtım bloğu, 4 çıkış", G + "/Dağıtım", "iec.dist.24vdc", {"Çıkış sayısı": "4"},
        keywords=["dağıtım", "24VDC"]),
    dev("dev.trafo.400.24", "Kumanda trafosu 400/24 VAC 100 VA", G + "/Trafolar", "iec.transformer.control", {"Güç": "100VA", "Primer": "400V", "Sekonder": "24V"},
        keywords=["kumanda trafosu"]),
]


def main():
    ids = [d["id"] for d in DEVICES]
    assert len(ids) == len(set(ids))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    doc = {"formatVersion": 1, "library": "MYG Cihaz Kütüphanesi", "version": VERSION, "devices": DEVICES}
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{len(DEVICES)} cihaz")


if __name__ == "__main__":
    main()
