#!/usr/bin/env python3
"""MYG Electrical IEC sembol kütüphanesi üreticisi.

Çıktı: src/MYG.Electrical.Symbols/Library/*.json (uygulamaya gömülü kaynak).

Kurallar:
  * Ölçüler mm, Y aşağı, orijin = ekleme noktası.
  * Tüm pinler 5 mm grid üzerinde (otomatik bağlantı bunu gerektirir).
  * Sembol kimlikleri (id) proje dosyalarında saklanır: bir kez yayımlanan id DEĞİŞTİRİLMEZ.
  * Yeni sembol eklemek için kod değişikliği gerekmez; JSON dosyası yeterlidir. Bu betik yalnızca
    yerleşik kütüphaneyi tutarlı üretmek içindir.

Kullanım:  python3 tools/symbolgen/generate.py
"""
from __future__ import annotations

import json
import math
import os
import sys

R = 10.0  # standart dikey sembolde pin uzaklığı
LEAD_END = 4.0
BLADE_TIP_X = 3.5
BLADE_TIP_Y = 4.5
LIB_VERSION = "1.0.0"

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "src", "MYG.Electrical.Symbols", "Library")


def num(v):
    v = float(v)
    if abs(v - round(v)) < 1e-12:
        return int(round(v))
    return v


def blade_mid_x(ox):
    return ox - BLADE_TIP_X / 2


class B:
    """Sembol oluşturucu (C# SymbolBuilder ile birebir aynı çıktıyı üretir)."""

    def __init__(self, sid, name, category, prefix, role="standalone", description="", keywords=()):
        self.d = dict(id=sid, name=name, category=category, description=description,
                      keywords=list(keywords), referencePrefix=prefix, role=role)
        self.prims = []
        self.pins = []
        self.conns = []
        self.props = {}

    # ---- temel şekiller ----
    def line(self, x1, y1, x2, y2, dashed=False):
        p = dict(kind="line", x1=num(x1), y1=num(y1), x2=num(x2), y2=num(y2))
        if dashed:
            p["dashed"] = True
        self.prims.append(p)
        return self

    def poly_lines(self, closed, *pts):
        for a, b in zip(pts, pts[1:]):
            self.line(a[0], a[1], b[0], b[1])
        if closed and len(pts) > 2:
            self.line(pts[-1][0], pts[-1][1], pts[0][0], pts[0][1])
        return self

    def polygon(self, pts, closed=True, filled=False):
        p = dict(kind="polygon", points=[num(c) for pt in pts for c in pt])
        if not closed:
            p["open"] = True
        if filled:
            p["filled"] = True
        self.prims.append(p)
        return self

    def rect(self, x, y, w, h, filled=False, dashed=False):
        p = dict(kind="rect", x=num(x), y=num(y), width=num(w), height=num(h))
        if filled:
            p["filled"] = True
        if dashed:
            p["dashed"] = True
        self.prims.append(p)
        return self

    def circle(self, cx, cy, r, filled=False):
        p = dict(kind="circle", centerX=num(cx), centerY=num(cy), radius=num(r))
        if filled:
            p["filled"] = True
        self.prims.append(p)
        return self

    def arc(self, cx, cy, r, start, sweep):
        self.prims.append(dict(kind="arc", centerX=num(cx), centerY=num(cy), radius=num(r),
                               startAngle=num(start), sweepAngle=num(sweep)))
        return self

    def text(self, x, y, t, h):
        self.prims.append(dict(kind="text", x=num(x), y=num(y), text=t, height=num(h)))
        return self

    def pin(self, pid, x, y, direction, function="general", description=None):
        assert abs(x / 5 - round(x / 5)) < 1e-9 and abs(y / 5 - round(y / 5)) < 1e-9, (self.d["id"], pid, x, y)
        p = dict(id=pid, x=num(x), y=num(y), direction=direction)
        if function != "general":
            p["function"] = function
        if description:
            p["description"] = description
        self.pins.append(p)
        return self

    def conn(self, a, b, kind="switched"):
        c = dict(a=a, b=b)
        if kind != "switched":
            c["kind"] = kind
        self.conns.append(c)
        return self

    def prop(self, k, v):
        self.props[k] = v
        return self

    # ---- tekrar kullanılan IEC parçaları ----
    def no_contact(self, ox, top, bottom, function="contactNo", oy=0.0):
        self.line(ox, oy - R, ox, oy - LEAD_END)
        self.line(ox, oy + R, ox, oy + LEAD_END)
        self.line(ox, oy + LEAD_END, ox - BLADE_TIP_X, oy - BLADE_TIP_Y)
        self.pin(top, ox, oy - R, "up", function).pin(bottom, ox, oy + R, "down", function)
        return self.conn(top, bottom)

    def nc_contact(self, ox, top, bottom, function="contactNc", oy=0.0):
        self.line(ox, oy - R, ox, oy - LEAD_END)
        self.line(ox, oy - LEAD_END, ox + LEAD_END, oy - LEAD_END)
        self.line(ox, oy + R, ox, oy + LEAD_END)
        self.line(ox, oy + LEAD_END, ox + BLADE_TIP_X + 1, oy - BLADE_TIP_Y - 0.5)
        self.pin(top, ox, oy - R, "up", function).pin(bottom, ox, oy + R, "down", function)
        return self.conn(top, bottom)

    def coil_box(self, a1="A1", a2="A2", ox=0.0):
        self.rect(ox - 5, -3, 10, 6)
        self.line(ox, -R, ox, -3).line(ox, 3, ox, R)
        return self.pin(a1, ox, -R, "up", "coil").pin(a2, ox, R, "down", "coil")

    def box(self, w, h, label=None, label_h=3.0, top=(), bottom=(), left=(), right=(), pitch=10.0, lead=5.0,
            label_y=0.0):
        """Dikdörtgen cihaz kutusu; pinler kenarlardan 'lead' kadar dışarıda, 'pitch' aralıklı.
        top/bottom/left/right: (pinId, function) veya pinId listesi."""
        hw, hh = w / 2, h / 2
        self.rect(-hw, -hh, w, h)
        if label:
            for i, part in enumerate(label.split("\n")):
                self.text(0, label_y + i * (label_h + 1), part, label_h)

        def norm(p):
            return p if isinstance(p, tuple) else (p, "general")

        def place(items, side):
            items = [norm(p) for p in items]
            n = len(items)
            for i, (pid, fn) in enumerate(items):
                off = (i - (n - 1) / 2) * pitch
                if side == "top":
                    self.line(off, -hh - lead, off, -hh).pin(pid, off, -hh - lead, "up", fn)
                elif side == "bottom":
                    self.line(off, hh, off, hh + lead).pin(pid, off, hh + lead, "down", fn)
                elif side == "left":
                    self.line(-hw - lead, off, -hw, off).pin(pid, -hw - lead, off, "left", fn)
                else:
                    self.line(hw, off, hw + lead, off).pin(pid, hw + lead, off, "right", fn)

        place(top, "top")
        place(bottom, "bottom")
        place(left, "left")
        place(right, "right")
        return self

    def build(self):
        d = dict(self.d)
        if not d["description"]:
            del d["description"]
        if not d["keywords"]:
            del d["keywords"]
        if d["role"] == "standalone":
            del d["role"]
        d["primitives"] = self.prims
        d["pins"] = self.pins
        if self.conns:
            d["internalConnections"] = self.conns
        if self.props:
            d["defaultProperties"] = self.props
        return d


def poles(b, pins, pitch=10.0, nc=False, function="power"):
    """Çok kutuplu güç kontağı dizisi; ortalanmış."""
    n = len(pins)
    xs = [(i - (n - 1) / 2) * pitch for i in range(n)]
    for x, (t, bt) in zip(xs, pins):
        (b.nc_contact if nc else b.no_contact)(x, t, bt, function)
    return xs


def size_for(n, pitch=10.0, minimum=20.0):
    w = max(minimum, n * pitch)
    return math.ceil(w / 10) * 10


def through_line(b, x, top, bottom, function="power", kind="permanent"):
    b.line(x, -R, x, R).pin(top, x, -R, "up", function).pin(bottom, x, R, "down", function)
    return b.conn(top, bottom, kind)


# =====================================================================================
# Kategoriler
# =====================================================================================
C_PROT = "Koruma"
C_SW = "Kontaktör ve Röle"
C_MOT = "Motor ve Sürücü"
C_PLC = "PLC ve Otomasyon"
C_SENS = "Sensör ve Saha"
C_CTRL = "Kumanda ve Sinyal"
C_TERM = "Klemens ve Konnektör"
C_MEAS = "Ölçüm"
C_PWR = "Enerji"
C_GEN = "Kurulum ve Diğer"

POWER3 = [("1", "2"), ("3", "4"), ("5", "6")]


# ---------------------------------------------------------------- mevcut 14 sembol (id + geometri sabit)
def contactor_3p():
    b = B("iec.contactor.3p", "Kontaktör (3 kutup)", C_SW + "/Kontaktörler", "K", role="contact",
          description="Kontaktör ana kontakları (1-2, 3-4, 5-6). Bobin (A1-A2) aynı etiketle ayrı yerleştirilir.",
          keywords=["contactor", "güç kontağı", "ana kontak", "1L1"])
    pitch = 10
    for i, (t, bt) in enumerate(POWER3):
        ox = (i - 1) * pitch
        b.no_contact(ox, t, bt, "power")
        b.arc(ox, -4, 1.2, 0, 180)
    return b.line(blade_mid_x(-pitch), 0, blade_mid_x(pitch), 0, dashed=True)


def mcb_3p():
    b = B("iec.mcb", "Otomatik sigorta (3 kutup)", C_PROT + "/Otomatik sigortalar", "F",
          keywords=["MCB", "C16", "B16", "kesici", "3P"])
    pitch = 10
    for i, (t, bt) in enumerate(POWER3):
        ox = (i - 1) * pitch
        b.no_contact(ox, t, bt, "power")
        b.line(ox - 1, -5, ox + 1, -3).line(ox + 1, -5, ox - 1, -3)
    b.line(blade_mid_x(-pitch) - 6, 0, blade_mid_x(pitch), 0, dashed=True)
    b.rect(blade_mid_x(-pitch) - 9, -1.5, 3, 3)
    return b.prop("Anma akımı", "").prop("Karakteristik", "C")


def fuse():
    return (B("iec.fuse", "Sigorta", C_PROT + "/Sigortalar", "F", keywords=["fuse", "buşon", "NH", "cam sigorta"])
            .line(0, -R, 0, R).rect(-2, -5, 4, 10)
            .pin("1", 0, -R, "up", "power").pin("2", 0, R, "down", "power")
            .conn("1", "2", "permanent").prop("Anma akımı", ""))


def motor_3ph():
    cy, r, pitch, entry, bend = 8.0, 8.0, 10.0, 5.0, -3.0
    edge_y = cy - math.sqrt(r * r - entry * entry)
    return (B("iec.motor.3ph", "Motor (3 faz)", C_MOT + "/Motorlar", "M", keywords=["asenkron", "motor", "3~"])
            .circle(0, cy, r).text(0, cy - 1.5, "M", 5).text(0, cy + 3.5, "3~", 3)
            .line(-pitch, -R, -pitch, bend).line(-pitch, bend, -entry, edge_y)
            .line(0, -R, 0, cy - r)
            .line(pitch, -R, pitch, bend).line(pitch, bend, entry, edge_y)
            .pin("U1", -pitch, -R, "up", "power").pin("V1", 0, -R, "up", "power").pin("W1", pitch, -R, "up", "power")
            .prop("Güç", "").prop("Gerilim", "400V").prop("Akım", "").prop("Devir", ""))


def coil():
    return (B("iec.coil", "Bobin", C_SW + "/Bobinler", "K", role="main",
              description="Kontaktör/röle bobini; aynı etiketli kontaklar bu cihaza aittir.",
              keywords=["coil", "A1", "A2", "kontaktör bobini"])
            .coil_box().prop("Bobin gerilimi", "24VDC"))


def relay():
    cx = 15
    b = B("iec.relay", "Röle (bobin + NO)", C_SW + "/Röleler", "K", keywords=["relay", "röle"])
    b.coil_box()
    b.no_contact(cx, "13", "14")
    return b.line(5, 0, blade_mid_x(cx), 0, dashed=True).prop("Bobin gerilimi", "24VDC")


def contact_no():
    return (B("iec.contact.no", "Açık kontak (NO)", C_SW + "/Yardımcı kontaklar", "K", role="contact",
              keywords=["NO", "normalde açık", "13-14"])
            .no_contact(0, "13", "14"))


def contact_nc():
    return (B("iec.contact.nc", "Kapalı kontak (NC)", C_SW + "/Yardımcı kontaklar", "K", role="contact",
              keywords=["NC", "normalde kapalı", "11-12"])
            .nc_contact(0, "11", "12"))


def pushbutton_no():
    head = -8
    return (B("iec.pushbutton.no", "Buton (NO)", C_CTRL + "/Butonlar", "S", keywords=["start", "buton", "basmalı"])
            .no_contact(0, "13", "14")
            .line(head, 0, blade_mid_x(0), 0, dashed=True)
            .line(head, -2.5, head, 2.5).line(head, -2.5, head + 1, -2.5).line(head, 2.5, head + 1, 2.5))


def estop():
    stem = -7
    return (B("iec.estop", "Acil stop (NC)", C_CTRL + "/Butonlar", "S", keywords=["emergency", "acil", "mantar"])
            .nc_contact(0, "11", "12")
            .line(stem, 0, blade_mid_x(0) + 2, 0, dashed=True)
            .line(stem, -3, stem, 3)
            .arc(stem, 0, 3, 90, 180))


def limitswitch():
    tip = -6
    return (B("iec.limitswitch", "Limit switch (NO)", C_SENS + "/Limit switch", "B",
              keywords=["limit", "sınır", "pozisyon"])
            .no_contact(0, "13", "14")
            .line(tip, 0, blade_mid_x(0), 0, dashed=True)
            .poly_lines(True, (tip, -2), (tip - 3, 0), (tip, 2)))


def sensor(sid, name, text, keywords, out_fn="output"):
    d = 6
    return (B(sid, name, C_SENS + "/Yaklaşım sensörleri", "B", keywords=keywords)
            .poly_lines(True, (0, -d), (d, 0), (0, d), (-d, 0))
            .text(0, 0, text, 2)
            .line(0, -R, 0, -d).line(0, d, 0, R).line(d, 0, R, 0)
            .pin("BN", 0, -R, "up", "supplyPositive", "+24V (kahverengi)")
            .pin("BU", 0, R, "down", "supplyNegative", "0V (mavi)")
            .pin("BK", R, 0, "right", out_fn, "Çıkış (siyah)"))


def lamp():
    r = 4.0
    k = r * math.sqrt(0.5)
    return (B("iec.lamp", "Sinyal lambası", C_CTRL + "/Sinyal lambaları", "P",
              keywords=["lamba", "pilot", "ışık", "H"])
            .circle(0, 0, r).line(-k, -k, k, k).line(-k, k, k, -k)
            .line(0, -R, 0, -r).line(0, r, 0, R)
            .pin("X1", 0, -R, "up").pin("X2", 0, R, "down").prop("Renk", ""))


def terminal():
    reach, r = 5.0, 1.5
    return (B("iec.terminal", "Klemens", C_TERM + "/Klemensler", "X", keywords=["terminal", "klemens", "sıra klemens"])
            .circle(0, 0, r).line(0, -reach, 0, -r).line(0, r, 0, reach)
            .pin("1", 0, -reach, "up", "terminal").pin("2", 0, reach, "down", "terminal")
            .conn("1", "2", "permanent"))


# Mevcut 14 sembolün tanım harfi geçmişte farklıydı (sinyal lambası H); proje dosyasındaki etiketler
# sembolden değil cihazdan okunduğu için harfin değişmesi mevcut projeleri etkilemez. Ancak
# geometri ve pinler birebir korunur (bkz. tests: Legacy_symbol_geometry_is_unchanged).


# ---------------------------------------------------------------- Koruma
def mcb_n(sid, name, n, keywords):
    b = B(sid, name, C_PROT + "/Otomatik sigortalar", "F", keywords=keywords)
    pins = [(str(2 * i + 1), str(2 * i + 2)) for i in range(n)]
    xs = poles(b, pins)
    for x in xs:
        b.line(x - 1, -5, x + 1, -3).line(x + 1, -5, x - 1, -3)
    left = blade_mid_x(xs[0])
    if n > 1:
        b.line(left - 6, 0, blade_mid_x(xs[-1]), 0, dashed=True)
    else:
        b.line(left - 6, 0, left, 0, dashed=True)
    b.rect(left - 9, -1.5, 3, 3)
    return b.prop("Anma akımı", "").prop("Karakteristik", "C")


def mcb_1pn():
    b = B("iec.mcb.1pn", "Otomatik sigorta (1P+N)", C_PROT + "/Otomatik sigortalar", "F", keywords=["MCB", "1P+N", "faz nötr"])
    xs = poles(b, [("1", "2"), ("N", "N'")])
    b.line(xs[0] - 1, -5, xs[0] + 1, -3).line(xs[0] + 1, -5, xs[0] - 1, -3)
    b.pins[2]["function"] = b.pins[3]["function"] = "neutral"
    left = blade_mid_x(xs[0])
    b.line(left - 6, 0, blade_mid_x(xs[1]), 0, dashed=True).rect(left - 9, -1.5, 3, 3)
    return b.prop("Anma akımı", "").prop("Karakteristik", "C")


def thermal(b, x):
    """Termik eleman (kutup üzerinde kare dalga)."""
    b.polygon([(x, -3), (x - 1.5, -3), (x - 1.5, 0), (x + 1.5, 0), (x + 1.5, 3), (x, 3)], closed=False)


def mpcb():
    b = B("iec.mpcb.3p", "Motor koruma şalteri (3 kutup)", C_PROT + "/Motor koruma", "Q",
          keywords=["MPCB", "GV2", "motor koruma", "termik manyetik"])
    xs = poles(b, POWER3)
    for x in xs:
        b.line(x - 1, -5, x + 1, -3).line(x + 1, -5, x - 1, -3)
    left = blade_mid_x(xs[0])
    b.line(left - 6, 0, blade_mid_x(xs[-1]), 0, dashed=True)
    # termik (kare dalga) + manyetik (I>) açtırma elemanları
    b.rect(left - 12, -3, 6, 6).text(left - 9, 0, "I>", 2)
    b.rect(left - 18, -3, 6, 6)
    b.polygon([(left - 16.5, -1.5), (left - 15, -1.5), (left - 15, 1.5), (left - 13.5, 1.5)], closed=False)
    return b.prop("Ayar aralığı", "").prop("Anma akımı", "")


def overload():
    b = B("iec.overload.3p", "Termik röle (3 kutup)", C_PROT + "/Motor koruma", "F", role="main",
          description="Aşırı akım rölesi ana yolu; yardımcı kontakları 95-96 / 97-98 aynı etiketle.",
          keywords=["termik", "overload", "LRD", "aşırı akım"])
    pins = [("1", "2"), ("3", "4"), ("5", "6")]
    for i, (t, bt) in enumerate(pins):
        x = (i - 1) * 10
        b.line(x, -R, x, -3).line(x, 3, x, R)
        thermal(b, x)
        b.pin(t, x, -R, "up", "power").pin(bt, x, R, "down", "power").conn(t, bt, "permanent")
    b.rect(-14, -6, 28, 12, dashed=True)
    return b.prop("Ayar akımı", "")


def overload_contact(nc):
    sid = "iec.overload.contact.nc" if nc else "iec.overload.contact.no"
    b = B(sid, "Termik kontağı (95-96 NC)" if nc else "Termik kontağı (97-98 NO)", C_PROT + "/Motor koruma", "F",
          role="contact", keywords=["termik", "95", "96", "97", "98"])
    if nc:
        b.nc_contact(0, "95", "96")
    else:
        b.no_contact(0, "97", "98")
    thermal(b, -9)
    return b.line(-7.5, 0, blade_mid_x(0) + (2 if nc else 0), 0, dashed=True)


def rcd(n):
    sid = f"iec.rcd.{n}p"
    b = B(sid, f"Kaçak akım rölesi ({n} kutup)", C_PROT + "/Kaçak akım", "F",
          keywords=["RCD", "kaçak akım", "30mA", "300mA", "ΔI"])
    pins = [("1", "2"), ("N", "N'")] if n == 2 else [("1", "2"), ("3", "4"), ("5", "6"), ("N", "N'")]
    xs = poles(b, pins)
    for p in b.pins:
        if p["id"].startswith("N"):
            p["function"] = "neutral"
    left = blade_mid_x(xs[0])
    b.line(left - 8, 0, blade_mid_x(xs[-1]), 0, dashed=True)
    b.rect(left - 14, -3, 6, 6).text(left - 11, 0, "ΔI", 2)
    return b.prop("Kaçak akım", "30mA").prop("Anma akımı", "")


def rcbo():
    b = B("iec.rcbo.2p", "Kaçak akım korumalı sigorta (RCBO 1P+N)", C_PROT + "/Kaçak akım", "F",
          keywords=["RCBO", "kaçak akım", "sigorta"])
    xs = poles(b, [("1", "2"), ("N", "N'")])
    b.pins[2]["function"] = b.pins[3]["function"] = "neutral"
    b.line(xs[0] - 1, -5, xs[0] + 1, -3).line(xs[0] + 1, -5, xs[0] - 1, -3)
    left = blade_mid_x(xs[0])
    b.line(left - 8, 0, blade_mid_x(xs[-1]), 0, dashed=True)
    b.rect(left - 14, -3, 6, 6).text(left - 11, 0, "ΔI", 2)
    return b.prop("Kaçak akım", "30mA").prop("Anma akımı", "").prop("Karakteristik", "C")


def surge():
    b = B("iec.surge.arrester", "Parafudr (aşırı gerilim koruma)", C_PROT + "/Aşırı gerilim", "F",
          keywords=["SPD", "parafudr", "surge", "yıldırım"])
    b.rect(-3, -5, 6, 10).line(0, -R, 0, -5).line(0, 5, 0, R)
    b.polygon([(-1.5, 3), (1.5, -3)], closed=False).polygon([(1.5, -3), (0.3, -2.4), (1.2, -1.6)], filled=True)
    return b.pin("L", 0, -R, "up", "power").pin("PE", 0, R, "down", "protectiveEarth").prop("Uc", "275V")


def fuse_3p():
    b = B("iec.fuse.3p", "Sigorta (3 kutup)", C_PROT + "/Sigortalar", "F", keywords=["NH", "3 faz sigorta"])
    for i, (t, bt) in enumerate(POWER3):
        x = (i - 1) * 10
        b.line(x, -R, x, R).rect(x - 2, -5, 4, 10)
        b.pin(t, x, -R, "up", "power").pin(bt, x, R, "down", "power").conn(t, bt, "permanent")
    return b.prop("Anma akımı", "")


def fuse_disconnector():
    b = B("iec.fuse.disconnector", "Sigortalı ayırıcı (1 kutup)", C_PROT + "/Sigortalar", "F",
          keywords=["sigortalı ayırıcı", "kartuş", "klemens sigorta"])
    b.line(0, -R, 0, -LEAD_END).line(0, R, 0, LEAD_END + 2)
    b.line(0, LEAD_END + 2, -BLADE_TIP_X, -BLADE_TIP_Y)
    b.polygon([(0.2, 5.6), (-3.3, -3.0), (-5.1, -2.3), (-1.6, 6.3)])
    return b.pin("1", 0, -R, "up", "power").pin("2", 0, R, "down", "power").conn("1", "2").prop("Anma akımı", "")


def disconnector_3p():
    b = B("iec.disconnector.3p", "Yük ayırıcı / ana şalter (3 kutup)", C_PROT + "/Ayırıcılar", "Q",
          keywords=["ana şalter", "pako", "yük ayırıcı", "main switch"])
    xs = poles(b, POWER3)
    for x in xs:
        b.line(x - 1.2, -4, x + 1.2, -4)
    left = blade_mid_x(xs[0])
    b.line(left - 7, 0, blade_mid_x(xs[-1]), 0, dashed=True)
    b.line(left - 7, -3, left - 7, 3).line(left - 7, 3, left - 5, 3)
    return b.prop("Anma akımı", "")


# ---------------------------------------------------------------- Kontaktör ve röle
def coil_timed(sid, name, filled, kw):
    b = B(sid, name, C_SW + "/Bobinler", "K", role="main", keywords=kw)
    b.coil_box()
    b.rect(-11, -3, 6, 6, filled=filled)
    if not filled:
        b.line(-11, -3, -5, 3).line(-11, 3, -5, -3)
    return b.prop("Bobin gerilimi", "24VDC").prop("Süre", "")


def contact_co():
    b = B("iec.contact.co", "Değişken kontak (CO)", C_SW + "/Yardımcı kontaklar", "K", role="contact",
          keywords=["CO", "changeover", "11-12-14", "değişken"])
    # 11 ortak (alt), 12 NC (sol üst), 14 NO (üst)
    b.line(0, R, 0, LEAD_END).line(0, LEAD_END, -BLADE_TIP_X, -BLADE_TIP_Y)
    b.line(0, -R, 0, -LEAD_END)
    b.line(-5, -R, -5, -BLADE_TIP_Y).line(-5, -BLADE_TIP_Y, -3.5, -BLADE_TIP_Y)
    b.pin("12", -5, -R, "up", "contactNc").pin("14", 0, -R, "up", "contactNo").pin("11", 0, R, "down", "contactCommon")
    return b.conn("11", "12").conn("11", "14")


def timed_contact(sid, name, nc, ondelay):
    b = B(sid, name, C_SW + "/Zaman kontakları", "K", role="contact",
          keywords=["zaman rölesi", "timer", "gecikmeli", "TON" if ondelay else "TOF"])
    if nc:
        b.nc_contact(0, "15", "16")
    else:
        b.no_contact(0, "17", "18")
    mid = blade_mid_x(0) + (2 if nc else 0)
    b.line(-8, 0, mid, 0, dashed=True)
    # paraşüt işareti: açılma yönü gecikmeyi gösterir
    if ondelay:
        b.arc(-8, 0, 2.5, 90, 180)
    else:
        b.arc(-8, 0, 2.5, -90, 180)
    return b


def contactor_3p_aux():
    b = B("iec.contactor.3p.aux", "Kontaktör 3 kutup + 1NO", C_SW + "/Kontaktörler", "K", role="contact",
          keywords=["contactor", "yardımcı kontak", "13-14"])
    xs = poles(b, POWER3)
    for x in xs:
        b.arc(x, -4, 1.2, 0, 180)
    b.no_contact(20, "13", "14")
    return b.line(blade_mid_x(xs[0]), 0, blade_mid_x(20), 0, dashed=True)


def box_dev(sid, name, cat, prefix, w, h, label, top=(), bottom=(), left=(), right=(), role="standalone",
            keywords=(), description="", label_h=3.0, pitch=10.0, label_y=0.0):
    b = B(sid, name, cat, prefix, role=role, keywords=keywords, description=description)
    return b.box(w, h, label=label, label_h=label_h, top=top, bottom=bottom, left=left, right=right, pitch=pitch,
                 label_y=label_y)


def phase_relay():
    b = box_dev("iec.relay.phase", "Faz sırası / faz kontrol rölesi", C_SW + "/İzleme röleleri", "K", 40, 20,
                "L1 L2 L3\nFAZ", top=[("L1", "power"), ("L2", "power"), ("L3", "power")],
                bottom=[("11", "contactCommon"), ("12", "contactNc"), ("14", "contactNo")],
                keywords=["faz sırası", "faz koruma", "phase sequence", "asimetri"], label_h=2.5, label_y=-2)
    return b.conn("11", "12").conn("11", "14").prop("Gerilim", "400VAC")


def safety_relay():
    b = box_dev("iec.relay.safety", "Güvenlik rölesi", C_SW + "/Güvenlik", "K", 70, 30, "SAFETY",
                top=[("A1", "supplyPositive"), ("S11", "output"), ("S12", "input"), ("S21", "output"), ("S22", "input"),
                     ("13", "contactNo"), ("23", "contactNo")],
                bottom=[("A2", "supplyNegative"), ("S33", "input"), ("S34", "input"), ("14", "contactNo"),
                        ("24", "contactNo")],
                keywords=["safety relay", "PNOZ", "acil stop rölesi", "güvenlik"])
    return b.conn("13", "14").conn("23", "24").prop("Kategori / PL", "")


def ssr():
    b = box_dev("iec.relay.ssr", "Katı hal rölesi (SSR)", C_SW + "/Röleler", "K", 20, 20, "SSR",
                top=[("3", "input"), ("1", "power")], bottom=[("4", "input"), ("2", "power")],
                keywords=["SSR", "solid state", "katı hal"])
    return b.conn("1", "2").prop("Yük akımı", "").prop("Kontrol gerilimi", "3-32VDC")


def relay_co():
    b = B("iec.relay.co", "Röle (bobin + CO)", C_SW + "/Röleler", "K", keywords=["röle", "değişken", "interface"])
    b.coil_box()
    cx = 20
    b.line(cx, R, cx, LEAD_END).line(cx, LEAD_END, cx - BLADE_TIP_X, -BLADE_TIP_Y)
    b.line(cx, -R, cx, -LEAD_END)
    b.line(cx - 5, -R, cx - 5, -BLADE_TIP_Y).line(cx - 5, -BLADE_TIP_Y, cx - 3.5, -BLADE_TIP_Y)
    b.pin("12", cx - 5, -R, "up", "contactNc").pin("14", cx, -R, "up", "contactNo").pin("11", cx, R, "down", "contactCommon")
    b.conn("11", "12").conn("11", "14")
    return b.line(5, 0, blade_mid_x(cx), 0, dashed=True).prop("Bobin gerilimi", "24VDC")


# ---------------------------------------------------------------- Motor ve sürücü
def motor_3ph_pe():
    cy, r, pitch, entry, bend = 8.0, 8.0, 10.0, 5.0, -3.0
    edge_y = cy - math.sqrt(r * r - entry * entry)
    return (B("iec.motor.3ph.pe", "Motor (3 faz + PE)", C_MOT + "/Motorlar", "M", keywords=["motor", "3~", "PE", "topraklı"])
            .circle(0, cy, r).text(0, cy - 1.5, "M", 5).text(0, cy + 3.5, "3~", 3)
            .line(-pitch, -R, -pitch, bend).line(-pitch, bend, -entry, edge_y)
            .line(0, -R, 0, cy - r)
            .line(pitch, -R, pitch, bend).line(pitch, bend, entry, edge_y)
            .line(20, -R, 20, cy).line(20, cy, r, cy)
            .pin("U1", -pitch, -R, "up", "power").pin("V1", 0, -R, "up", "power").pin("W1", pitch, -R, "up", "power")
            .pin("PE", 20, -R, "up", "protectiveEarth")
            .prop("Güç", "").prop("Gerilim", "400V").prop("Akım", "").prop("Devir", ""))


def motor_simple(sid, name, t2, pins, kw):
    cy, r = 8.0, 8.0
    b = B(sid, name, C_MOT + "/Motorlar", "M", keywords=kw)
    b.circle(0, cy, r).text(0, cy - 1.5, "M", 5).text(0, cy + 3.5, t2, 3)
    for (pid, fn), x in zip(pins, (-5, 5)):
        b.line(x, -R, x, cy - math.sqrt(r * r - x * x)).pin(pid, x, -R, "up", fn)
    return b.prop("Güç", "").prop("Gerilim", "")


def brake():
    b = B("iec.motor.brake", "Motor freni (elektromanyetik)", C_MOT + "/Motorlar", "M", role="contact",
          keywords=["fren", "brake", "balata"])
    b.rect(-5, -3, 10, 6).line(-5, 3, 5, -3).line(0, -R, 0, -3).line(0, 3, 0, R)
    return b.pin("BD1", 0, -R, "up", "coil").pin("BD2", 0, R, "down", "coil").prop("Gerilim", "")


def vfd_power(sid, name, inputs, kw):
    b = B(sid, name, C_MOT + "/Sürücüler", "T", role="main", keywords=kw,
          description="Sürücü güç bölümü. Kumanda klemensleri aynı etiketle 'Sürücü kumanda klemensleri' sembolüyle eklenir.")
    b.box(40, 30, top=inputs, bottom=[("U", "power"), ("V", "power"), ("W", "power"), ("PE2", "protectiveEarth")])
    b.line(-20, 15, 20, -15)
    b.text(-10, -7, "~", 6).text(10, 7, "~", 6)
    return b.prop("Güç", "").prop("Giriş gerilimi", "").prop("Çıkış akımı", "")


def vfd_control():
    top = [("DI1", "input"), ("DI2", "input"), ("DI3", "input"), ("DI4", "input"), ("DI5", "input"),
           ("COM", "supplyNegative"), ("+24V", "supplyPositive")]
    bottom = [("AI1", "input"), ("+10V", "supplyPositive"), ("GND", "supplyNegative"), ("AO1", "output"),
              ("RA", "contactNo"), ("RB", "contactNc"), ("RC", "contactCommon")]
    b = box_dev("iec.vfd.control", "Sürücü kumanda klemensleri", C_MOT + "/Sürücüler", "T", 70, 20, "VFD I/O",
                top=top, bottom=bottom, role="contact",
                keywords=["inverter", "frekans", "DI", "AI", "röle çıkışı"])
    return b.conn("RC", "RA").conn("RC", "RB")


def vfd_comm():
    return box_dev("iec.vfd.comm", "Sürücü haberleşme (RS485)", C_MOT + "/Sürücüler", "T", 30, 20, "RS485",
                   right=[("A", "communication"), ("B", "communication")], role="contact",
                   keywords=["Modbus", "RS485", "inverter"], label_h=2.5)


def softstarter():
    b = B("iec.softstarter", "Yumuşak yolverici", C_MOT + "/Sürücüler", "T",
          keywords=["soft starter", "yol verici"])
    b.box(40, 20, top=[("1L1", "power"), ("3L2", "power"), ("5L3", "power")],
          bottom=[("2T1", "power"), ("4T2", "power"), ("6T3", "power")])
    b.polygon([(-6, 4), (-2, -4), (2, 4)], closed=False).line(4, -4, 4, 4)
    return b.prop("Güç", "")


def pump():
    cy, r, pitch, entry, bend = 8.0, 8.0, 10.0, 5.0, -3.0
    edge_y = cy - math.sqrt(r * r - entry * entry)
    return (B("iec.pump.3ph", "Pompa motoru (3 faz)", C_MOT + "/Motorlar", "M", keywords=["pompa", "pump"])
            .circle(0, cy, r).polygon([(-4, cy + 4), (4, cy + 4), (0, cy - 5)])
            .line(-pitch, -R, -pitch, bend).line(-pitch, bend, -entry, edge_y)
            .line(0, -R, 0, cy - r)
            .line(pitch, -R, pitch, bend).line(pitch, bend, entry, edge_y)
            .pin("U1", -pitch, -R, "up", "power").pin("V1", 0, -R, "up", "power").pin("W1", pitch, -R, "up", "power")
            .prop("Güç", ""))


def fan():
    cy, r = 8.0, 8.0
    b = B("iec.fan.1ph", "Fan (1 faz)", C_MOT + "/Motorlar", "M", keywords=["fan", "havalandırma", "pano fanı"])
    b.circle(0, cy, r)
    b.polygon([(0, cy), (-2, cy - 6), (2, cy - 6)]).polygon([(0, cy), (5.2, cy + 3), (3.2, cy + 5.5)])
    b.polygon([(0, cy), (-5.2, cy + 3), (-3.2, cy + 5.5)])
    for (pid, fn), x in zip((("L", "power"), ("N", "neutral")), (-5, 5)):
        b.line(x, -R, x, cy - math.sqrt(r * r - x * x)).pin(pid, x, -R, "up", fn)
    return b


# ---------------------------------------------------------------- PLC ve otomasyon
def plc_cpu():
    b = box_dev("iec.plc.cpu", "PLC CPU", C_PLC + "/PLC", "A", 60, 40, "CPU",
                top=[("L+", "supplyPositive"), ("M", "supplyNegative"), ("PE", "protectiveEarth")],
                right=[("ETH", "communication"), ("A", "communication"), ("B", "communication")], role="main",
                keywords=["PLC", "CPU", "S7-1200", "DVP", "kontrolör"],
                description="PLC ana ünitesi. G/Ç modülleri ve tek kanal G/Ç sembolleri aynı etiketle bağlanabilir.")
    return b.prop("Model", "").prop("Firmware", "")


def plc_module(sid, name, label, top, bottom, kw):
    n = max(len(top), len(bottom))
    w = size_for(n)
    # Modül ayrı bir cihazdır (kendi etiketi, ör. -A2); tek kanal G/Ç sembolleri ise CPU'nun parçasıdır.
    return box_dev(sid, name, C_PLC + "/G-Ç modülleri", "A", w, 20, label, top=top, bottom=bottom, keywords=kw
                   ).prop("Adres başlangıcı", "")


def plc_single(sid, name, label, fn, direction):
    b = B(sid, name, C_PLC + "/Tek kanal G-Ç", "A", role="contact",
          keywords=["PLC", "giriş" if fn == "input" else "çıkış", "I/O", "adres"],
          description="PLC'nin tek bir G/Ç kanalı (EPLAN tarzı dağıtık gösterim). Etiket PLC ile aynı olmalı.")
    b.rect(-5, -4, 10, 8).text(0, 0, label, 2.5)
    if direction == "up":
        b.line(0, -R, 0, -4).pin("1", 0, -R, "up", fn)
    else:
        b.line(0, 4, 0, R).pin("1", 0, R, "down", fn)
    return b.prop("Adres", "")


def hmi():
    b = box_dev("iec.hmi", "Operatör paneli (HMI)", C_PLC + "/HMI", "A", 50, 30, "HMI",
                top=[("+24V", "supplyPositive"), ("0V", "supplyNegative"), ("PE", "protectiveEarth")],
                right=[("ETH", "communication"), ("A", "communication"), ("B", "communication")],
                keywords=["HMI", "panel", "dokunmatik", "Delta DOP", "operatör"])
    b.rect(-20, -10, 30, 20, dashed=True)
    return b.prop("Model", "").prop("Ekran", "")


def psu():
    b = box_dev("iec.psu.24vdc", "Güç kaynağı 24VDC", C_PWR + "/Güç kaynakları", "T", 40, 20, None,
                top=[("L", "power"), ("N", "neutral"), ("PE", "protectiveEarth")],
                bottom=[("+1", "supplyPositive"), ("+2", "supplyPositive"), ("-1", "supplyNegative"),
                        ("-2", "supplyNegative")],
                keywords=["SMPS", "24V", "güç kaynağı", "power supply", "adaptör"])
    b.line(-20, 10, 20, -10).text(-10, -4, "~", 6).text(10, 4, "=", 6)
    return b.conn("+1", "+2", "permanent").conn("-1", "-2", "permanent").prop("Çıkış", "24VDC").prop("Akım", "")


def eth_switch():
    return box_dev("iec.eth.switch", "Ethernet switch", C_PLC + "/Haberleşme", "A", 50, 20, "SWITCH",
                   top=[("+24V", "supplyPositive"), ("0V", "supplyNegative")],
                   bottom=[(f"P{i}", "communication") for i in range(1, 6)],
                   keywords=["switch", "ethernet", "Profinet", "Modbus TCP"])


def rs485_term():
    return box_dev("iec.comm.rs485", "RS485 haberleşme portu", C_PLC + "/Haberleşme", "X", 20, 20, "485",
                   top=[("A", "communication"), ("B", "communication")], bottom=[("SH", "shield")],
                   keywords=["RS485", "Modbus RTU", "haberleşme"], label_h=2.5)


def encoder():
    return box_dev("iec.encoder", "Enkoder (inkremental)", C_SENS + "/Enkoderler", "B", 50, 20, "ENC",
                   top=[("+V", "supplyPositive"), ("0V", "supplyNegative"), ("A", "output"), ("B", "output"),
                        ("Z", "output")],
                   keywords=["encoder", "enkoder", "pulse", "A/B/Z"]).prop("Çözünürlük", "")


# ---------------------------------------------------------------- Sensör ve saha
def switch_with_actuator(sid, name, cat, prefix, nc, label, kw, pins=None):
    b = B(sid, name, cat, prefix, keywords=kw)
    t, bt = pins or (("11", "12") if nc else ("13", "14"))
    if nc:
        b.nc_contact(0, t, bt)
    else:
        b.no_contact(0, t, bt)
    mid = blade_mid_x(0) + (2 if nc else 0)
    b.line(-9, 0, mid, 0, dashed=True)
    b.rect(-15, -3, 6, 6).text(-12, 0, label, 2.5)
    return b


def limitswitch_nc():
    tip = -6
    return (B("iec.limitswitch.nc", "Limit switch (NC)", C_SENS + "/Limit switch", "B", keywords=["limit", "sınır", "NC"])
            .nc_contact(0, "11", "12")
            .line(tip, 0, blade_mid_x(0) + 2, 0, dashed=True)
            .poly_lines(True, (tip, -2), (tip - 3, 0), (tip, 2)))


def safety_switch():
    b = B("iec.safety.switch", "Emniyet kapı switchi (zorlamalı açma NC)", C_SENS + "/Kapı ve emniyet", "B",
          keywords=["kapı switch", "emniyet", "interlock", "güvenlik switchi"])
    b.nc_contact(0, "11", "12")
    b.line(-7, 0, blade_mid_x(0) + 2, 0, dashed=True)
    b.circle(-10, 0, 3).polygon([(-11.5, 1.5), (-8.5, 1.5), (-10, -1.5)], filled=True)
    return b


def door_sensor():
    b = B("iec.door.sensor", "Kapı sensörü (manyetik, NO)", C_SENS + "/Kapı ve emniyet", "B",
          keywords=["kapı", "reed", "manyetik", "door"])
    b.no_contact(0, "13", "14")
    b.line(-8, 0, blade_mid_x(0), 0, dashed=True)
    b.polygon([(-13, -3), (-8, -3), (-8, 3), (-13, 3)]).line(-10.5, -3, -10.5, 3)
    return b


def pt100(wires):
    b = B(f"iec.pt100.{wires}w", f"PT100 sıcaklık sensörü ({wires} telli)", C_SENS + "/Sıcaklık", "B",
          keywords=["PT100", "RTD", "sıcaklık", "termistör"])
    b.rect(-2, -6, 4, 12).line(-4, 5, 4, -5).text(5, 4, "θ", 3)
    b.line(0, -R, 0, -6).line(0, 6, 0, R)
    b.pin("1", 0, -R, "up", "input").pin("2", 0, R, "down", "input")
    if wires == 3:
        b.line(0, 8, -5, 8).line(-5, 8, -5, R).pin("3", -5, R, "down", "input")
    return b


def thermocouple():
    b = B("iec.thermocouple", "Termokupl", C_SENS + "/Sıcaklık", "B", keywords=["termokupl", "TC", "K tipi", "J tipi"])
    b.line(-5, -R, -5, -5).line(-5, -5, 0, 0).line(5, -R, 5, -5).line(5, -5, 0, 0)
    b.circle(0, 0, 0.8, filled=True).text(0, 4, "TC", 2.5)
    return b.pin("+", -5, -R, "up", "input").pin("-", 5, -R, "up", "input").prop("Tip", "K")


def transmitter():
    b = box_dev("iec.transmitter", "Transmitter (4-20 mA, 2 telli)", C_SENS + "/Transmitterler", "B", 20, 20, "PT",
                top=[("+", "supplyPositive")], bottom=[("-", "output")],
                keywords=["transmitter", "4-20mA", "basınç", "analog"])
    return b.prop("Ölçüm aralığı", "").prop("Çıkış", "4-20mA")


# ---------------------------------------------------------------- Kumanda ve sinyal
def pushbutton_nc():
    head = -8
    return (B("iec.pushbutton.nc", "Buton (NC)", C_CTRL + "/Butonlar", "S", keywords=["stop", "buton", "NC"])
            .nc_contact(0, "11", "12")
            .line(head, 0, blade_mid_x(0) + 2, 0, dashed=True)
            .line(head, -2.5, head, 2.5).line(head, -2.5, head + 1, -2.5).line(head, 2.5, head + 1, 2.5))


def selector(sid, name, key=False):
    b = B(sid, name, C_CTRL + "/Seçici şalterler", "S",
          keywords=["selector", "seçici", "anahtarlı" if key else "mandallı", "0-1"])
    b.no_contact(0, "13", "14")
    b.line(-8, 0, blade_mid_x(0), 0, dashed=True)
    if key:
        b.circle(-11, 0, 1.5).line(-9.5, 0, -8, 0)
    else:
        b.polygon([(-12, -2.5), (-8, -2.5), (-8, 2.5)], closed=False)
    return b


def buzzer():
    b = B("iec.buzzer", "Buzzer", C_CTRL + "/Sesli ikaz", "P", keywords=["buzzer", "ikaz", "sesli"])
    b.arc(0, 2, 5, 180, 180).line(-5, 2, 5, 2)
    b.line(-5, -R, -5, 2).line(5, -R, 5, 2)
    return b.pin("1", -5, -R, "up").pin("2", 5, -R, "up")


def horn():
    b = B("iec.horn", "Korna / siren", C_CTRL + "/Sesli ikaz", "P", keywords=["korna", "siren", "horn", "flaşör"])
    b.rect(-3, -3, 6, 6).polygon([(3, -3), (7, -6), (7, 6), (3, 3)])
    b.line(0, -R, 0, -3).line(0, 3, 0, R)
    return b.pin("1", 0, -R, "up").pin("2", 0, R, "down")


def potentiometer():
    b = B("iec.potentiometer", "Potansiyometre", C_CTRL + "/Ayar elemanları", "R", keywords=["pot", "potansiyometre", "hız ayarı"])
    b.rect(-2, -6, 4, 12).line(0, -R, 0, -6).line(0, 6, 0, R)
    b.line(R, 0, 3, 0).polygon([(2.2, 0), (4.0, -1.0), (4.0, 1.0)], filled=True)
    return b.pin("1", 0, -R, "up").pin("3", 0, R, "down").pin("2", R, 0, "right").conn("1", "3", "permanent")


def illuminated_button():
    head = -8
    b = B("iec.pushbutton.lit", "Işıklı buton (NO + lamba)", C_CTRL + "/Butonlar", "S", keywords=["ışıklı buton", "start"])
    b.no_contact(0, "13", "14")
    b.line(head, 0, blade_mid_x(0), 0, dashed=True)
    b.line(head, -2.5, head, 2.5).line(head, -2.5, head + 1, -2.5).line(head, 2.5, head + 1, 2.5)
    r, cx = 3.0, 15
    k = r * math.sqrt(0.5)
    b.circle(cx, 0, r).line(cx - k, -k, cx + k, k).line(cx - k, k, cx + k, -k)
    b.line(cx, -R, cx, -r).line(cx, r, cx, R)
    return b.pin("X1", cx, -R, "up").pin("X2", cx, R, "down")


# ---------------------------------------------------------------- Klemens ve konnektör
def terminal_pe():
    b = B("iec.terminal.pe", "PE klemensi", C_TERM + "/Klemensler", "X", keywords=["PE", "toprak klemensi", "sarı-yeşil"])
    b.circle(0, 0, 1.5).line(0, -5, 0, -1.5).line(0, 1.5, 0, 5)
    b.line(3, -1.5, 3, 1.5).line(4, -1, 4, 1).line(5, -0.5, 5, 0.5)
    return (b.pin("1", 0, -5, "up", "protectiveEarth").pin("2", 0, 5, "down", "protectiveEarth")
            .conn("1", "2", "permanent"))


def terminal_fused():
    b = B("iec.terminal.fused", "Sigortalı klemens", C_TERM + "/Klemensler", "X", keywords=["sigortalı klemens", "fuse terminal"])
    b.circle(0, -6, 1.5).line(0, -R, 0, -7.5).line(0, -4.5, 0, R).rect(-1.5, -2, 3, 7)
    return b.pin("1", 0, -R, "up", "terminal").pin("2", 0, R, "down", "terminal").conn("1", "2", "permanent")


def terminal_disconnect():
    b = B("iec.terminal.disconnect", "Ayırmalı klemens", C_TERM + "/Klemensler", "X", keywords=["ayırmalı", "test klemensi"])
    b.circle(0, -6, 1.5).line(0, -R, 0, -7.5).line(0, -4.5, 0, -1.5).line(0, 2, 0, R).line(0, 2, -2, -1.5)
    return b.pin("1", 0, -R, "up", "terminal").pin("2", 0, R, "down", "terminal").conn("1", "2")


def plug(sid, name, female):
    b = B(sid, name, C_TERM + "/Konnektörler", "X", keywords=["konnektör", "fiş", "soket", "plug"])
    b.line(0, -5, 0, 0 if not female else -1).line(0, 2, 0, 5)
    if female:
        b.arc(0, 1, 2, 180, 180)
    else:
        b.rect(-1, 0, 2, 2, filled=True)
    return b.pin("1", 0, -5, "up", "terminal").pin("2", 0, 5, "down", "terminal").conn("1", "2", "permanent")


def m12():
    return box_dev("iec.connector.m12", "M12 konnektör (4 pin)", C_TERM + "/Konnektörler", "X", 40, 10, "M12",
                   top=[("1", "supplyPositive"), ("2", "general"), ("3", "supplyNegative"), ("4", "output")],
                   keywords=["M12", "sensör kablosu", "konnektör"], label_h=2.5)


def earth():
    b = B("iec.earth", "Toprak (PE)", C_TERM + "/Toprak", "", role="graphic", keywords=["toprak", "PE", "earth", "ground"])
    b.line(0, -5, 0, 0).line(-3, 0, 3, 0).line(-2, 1, 2, 1).line(-1, 2, 1, 2)
    return b.pin("PE", 0, -5, "up", "protectiveEarth")


def chassis():
    b = B("iec.chassis", "Gövde / şasi bağlantısı", C_TERM + "/Toprak", "", role="graphic", keywords=["şasi", "gövde", "chassis"])
    b.line(0, -5, 0, 0).line(-3, 0, 3, 0).line(-3, 0, -4.5, 2).line(0, 0, -1.5, 2).line(3, 0, 1.5, 2)
    return b.pin("PE", 0, -5, "up", "protectiveEarth")


def shield():
    b = B("iec.shield", "Kablo ekranı (blendaj)", C_TERM + "/Toprak", "", role="graphic", keywords=["ekran", "blendaj", "shield"])
    b.circle(0, 0, 3).line(0, 3, 0, 5)
    b.prims[-2]["dashed"] = True
    return b.pin("SH", 0, 5, "down", "shield")


# ---------------------------------------------------------------- Ölçüm
def meter(sid, name, letter, kw):
    b = B(sid, name, C_MEAS + "/Göstergeler", "P", keywords=kw)
    b.circle(0, 0, 5).text(0, 0, letter, 4).line(0, -R, 0, -5).line(0, 5, 0, R)
    return b.pin("1", 0, -R, "up").pin("2", 0, R, "down")


def ct():
    b = B("iec.ct", "Akım trafosu (CT)", C_MEAS + "/Akım trafoları", "T",
          keywords=["CT", "akım trafosu", "100/5", "S1 S2"])
    b.line(0, -R, 0, R).circle(0, 0, 3)
    b.line(3, 0, R, 0).line(R, 0, R, 5)
    b.line(-3, 0, -R, 0).line(-R, 0, -R, 5)
    b.pin("P1", 0, -R, "up", "power").pin("P2", 0, R, "down", "power")
    b.pin("S1", -R, 5, "down", "output").pin("S2", R, 5, "down", "output")
    return b.conn("P1", "P2", "permanent").prop("Oran", "100/5A").prop("Sınıf", "0.5")


def power_analyzer():
    b = box_dev("iec.poweranalyzer", "Enerji analizörü", C_MEAS + "/Analizörler", "P", 120, 30, "ENERJİ ANALİZÖRÜ",
                top=[("V1", "input"), ("V2", "input"), ("V3", "input"), ("VN", "neutral"), ("A1", "supplyPositive"),
                     ("A2", "supplyNegative")],
                bottom=[("I1+", "input"), ("I1-", "input"), ("I2+", "input"), ("I2-", "input"), ("I3+", "input"),
                        ("I3-", "input"), ("A", "communication"), ("B", "communication")],
                keywords=["power analyzer", "analizör", "Modbus", "kWh", "enerji"], label_h=3)
    return b.prop("Model", "").prop("CT oranı", "")


def energy_meter():
    b = box_dev("iec.energymeter", "Elektrik sayacı (kWh)", C_MEAS + "/Sayaçlar", "P", 40, 20, "kWh",
                top=[("L1", "power"), ("L2", "power"), ("L3", "power"), ("N", "neutral")],
                bottom=[("L1'", "power"), ("L2'", "power"), ("L3'", "power"), ("N'", "neutral")],
                keywords=["sayaç", "kWh", "energy meter"])
    for a, c in (("L1", "L1'"), ("L2", "L2'"), ("L3", "L3'"), ("N", "N'")):
        b.conn(a, c, "permanent")
    return b


def hour_meter():
    b = B("iec.hourmeter", "Çalışma saati sayacı", C_MEAS + "/Sayaçlar", "P", keywords=["saat sayacı", "hour meter"])
    b.rect(-5, -4, 10, 8).text(0, 0, "h", 4).line(0, -R, 0, -4).line(0, 4, 0, R)
    return b.pin("1", 0, -R, "up").pin("2", 0, R, "down")


# ---------------------------------------------------------------- Enerji
def transformer(sid, name, prim, sec, kw, prefix="T"):
    b = B(sid, name, C_PWR + "/Trafolar", prefix, keywords=kw)
    b.circle(0, -3, 4).circle(0, 3, 4)
    b.line(-5, -R, -5, -6).line(-5, -6, -2.5, -6).line(5, -R, 5, -6).line(5, -6, 2.5, -6)
    b.line(-5, R, -5, 6).line(-5, 6, -2.5, 6).line(5, R, 5, 6).line(5, 6, 2.5, 6)
    b.pin(prim[0], -5, -R, "up", "power").pin(prim[1], 5, -R, "up", "power")
    b.pin(sec[0], -5, R, "down", "power").pin(sec[1], 5, R, "down", "power")
    return b.prop("Güç", "").prop("Primer", "").prop("Sekonder", "")


def battery():
    b = B("iec.battery", "Akü / batarya", C_PWR + "/Güç kaynakları", "G", keywords=["akü", "batarya", "UPS"])
    b.line(-4, -1, 4, -1).line(-2, 1, 2, 1).line(0, -R, 0, -1).line(0, 1, 0, R)
    return b.pin("+", 0, -R, "up", "supplyPositive").pin("-", 0, R, "down", "supplyNegative").prop("Gerilim", "24V")


def socket_outlet():
    b = B("iec.socket", "Priz (topraklı)", C_PWR + "/Prizler", "X", keywords=["priz", "socket", "servis prizi"])
    b.arc(0, 2, 5, 180, 180).line(-5, 2, 5, 2).line(0, -3, 0, 2)
    b.line(-5, -R, -5, 2).line(5, -R, 5, 2).line(0, -R, 0, -3)
    return (b.pin("L", -5, -R, "up", "power").pin("PE", 0, -R, "up", "protectiveEarth")
            .pin("N", 5, -R, "up", "neutral"))


def dc_distribution():
    b = box_dev("iec.dist.24vdc", "24VDC dağıtım bloğu (sigortalı)", C_PWR + "/Dağıtım", "F", 40, 10, None,
                top=[("+IN", "supplyPositive")],
                bottom=[("+1", "supplyPositive"), ("+2", "supplyPositive"), ("+3", "supplyPositive"),
                        ("+4", "supplyPositive")], keywords=["24VDC", "dağıtım", "sigortalı dağıtım", "potansiyel"])
    for i in range(1, 5):
        b.conn("+IN", f"+{i}")
    return b


# ---------------------------------------------------------------- Kurulum ve diğer
def resistor():
    b = B("iec.resistor", "Direnç", C_GEN + "/Pasif elemanlar", "R", keywords=["direnç", "resistor"])
    b.rect(-2, -5, 4, 10).line(0, -R, 0, -5).line(0, 5, 0, R)
    return b.pin("1", 0, -R, "up").pin("2", 0, R, "down").conn("1", "2", "permanent").prop("Değer", "")


def capacitor():
    b = B("iec.capacitor", "Kondansatör", C_GEN + "/Pasif elemanlar", "C", keywords=["kondansatör", "kapasitör"])
    b.line(-4, -1, 4, -1).line(-4, 1, 4, 1).line(0, -R, 0, -1).line(0, 1, 0, R)
    return b.pin("1", 0, -R, "up").pin("2", 0, R, "down").prop("Değer", "")


def diode():
    b = B("iec.diode", "Diyot", C_GEN + "/Yarı iletkenler", "R", keywords=["diyot", "serbest geçiş", "1N4007"])
    b.polygon([(-3, -2.5), (3, -2.5), (0, 2.5)]).line(-3, 2.5, 3, 2.5)
    b.line(0, -R, 0, -2.5).line(0, 2.5, 0, R)
    return b.pin("A", 0, -R, "up").pin("K", 0, R, "down")


def led():
    b = B("iec.led", "LED", C_GEN + "/Yarı iletkenler", "P", keywords=["LED", "gösterge"])
    b.polygon([(-3, -2.5), (3, -2.5), (0, 2.5)]).line(-3, 2.5, 3, 2.5)
    b.line(0, -R, 0, -2.5).line(0, 2.5, 0, R)
    b.line(4, -1, 6.5, -3).line(4, 1, 6.5, -1)
    return b.pin("A", 0, -R, "up").pin("K", 0, R, "down")


def heater():
    b = B("iec.heater", "Isıtıcı / rezistans", C_GEN + "/Isıtma", "E", keywords=["ısıtıcı", "rezistans", "heater"])
    b.rect(-5, -3, 10, 6).line(-2.5, -3, -2.5, 3).line(0, -3, 0, 3).line(2.5, -3, 2.5, 3)
    b.line(0, -R, 0, -3).line(0, 3, 0, R)
    return b.pin("1", 0, -R, "up", "power").pin("2", 0, R, "down", "power").prop("Güç", "")


def heater_3ph():
    b = B("iec.heater.3ph", "Isıtıcı (3 faz, yıldız)", C_GEN + "/Isıtma", "E", keywords=["rezistans", "3 faz", "fırın"])
    for x, p in zip((-10, 0, 10), ("1", "2", "3")):
        b.line(x, -R, x, -5).rect(x - 2, -5, 4, 10).line(x - 2, 5, x + 2, -5).line(x, 5, x, 7)
        b.pin(p, x, -R, "up", "power")
    b.line(-10, 7, 10, 7)
    return b.prop("Güç", "")


def valve():
    b = B("iec.valve.solenoid", "Selenoid valf bobini", C_GEN + "/Valfler", "Y", keywords=["valf", "selenoid", "pnömatik", "bobin"])
    b.rect(-5, -3, 10, 6).line(-5, 3, 5, -3).line(0, -R, 0, -3).line(0, 3, 0, R)
    return b.pin("1", 0, -R, "up", "coil").pin("2", 0, R, "down", "coil").prop("Gerilim", "24VDC")


def light_fixture():
    b = B("iec.light", "Aydınlatma armatürü", C_GEN + "/Aydınlatma", "E", keywords=["armatür", "aydınlatma", "lamba"])
    b.line(-6, 0, 6, 0).line(-6, -2, -6, 2).line(6, -2, 6, 2)
    b.line(-5, -R, -5, 0).line(5, -R, 5, 0)
    return b.pin("L", -5, -R, "up", "power").pin("N", 5, -R, "up", "neutral")


# =====================================================================================
LIBRARY = {
    "01-koruma.json": (C_PROT, [
        fuse, fuse_3p, fuse_disconnector, mcb_3p,
        lambda: mcb_n("iec.mcb.1p", "Otomatik sigorta (1 kutup)", 1, ["MCB", "1P", "C6", "B10"]),
        lambda: mcb_n("iec.mcb.2p", "Otomatik sigorta (2 kutup)", 2, ["MCB", "2P"]),
        lambda: mcb_n("iec.mcb.4p", "Otomatik sigorta (4 kutup)", 4, ["MCB", "4P"]),
        mcb_1pn, mpcb, overload, lambda: overload_contact(True), lambda: overload_contact(False),
        lambda: rcd(2), lambda: rcd(4), rcbo, surge, disconnector_3p,
    ]),
    "02-kontaktor-role.json": (C_SW, [
        contactor_3p, contactor_3p_aux, coil,
        lambda: coil_timed("iec.coil.ondelay", "Zaman rölesi bobini (çekmede gecikmeli)", False, ["timer", "TON", "zaman rölesi"]),
        lambda: coil_timed("iec.coil.offdelay", "Zaman rölesi bobini (düşmede gecikmeli)", True, ["timer", "TOF", "zaman rölesi"]),
        relay, relay_co, contact_no, contact_nc, contact_co,
        lambda: timed_contact("iec.contact.no.ondelay", "Gecikmeli kapanan kontak (çekmede, NO)", False, True),
        lambda: timed_contact("iec.contact.nc.ondelay", "Gecikmeli açan kontak (çekmede, NC)", True, True),
        lambda: timed_contact("iec.contact.no.offdelay", "Gecikmeli açan kontak (düşmede, NO)", False, False),
        phase_relay, safety_relay, ssr,
    ]),
    "03-motor-surucu.json": (C_MOT, [
        motor_3ph, motor_3ph_pe,
        lambda: motor_simple("iec.motor.1ph", "Motor (1 faz)", "1~", (("L", "power"), ("N", "neutral")), ["motor", "monofaze", "1~"]),
        lambda: motor_simple("iec.motor.dc", "DC motor", "=", (("+", "supplyPositive"), ("-", "supplyNegative")), ["DC motor", "fırçalı"]),
        brake, pump, fan,
        lambda: vfd_power("iec.vfd.3ph", "Frekans inverteri (3 faz giriş)",
                          [("L1", "power"), ("L2", "power"), ("L3", "power"), ("PE", "protectiveEarth")],
                          ["VFD", "inverter", "sürücü", "22 kW", "frekans"]),
        lambda: vfd_power("iec.vfd.1ph", "Frekans inverteri (1 faz giriş)",
                          [("L", "power"), ("N", "neutral"), ("PE", "protectiveEarth")],
                          ["VFD", "inverter", "sürücü", "2 kW", "monofaze giriş"]),
        vfd_control, vfd_comm, softstarter,
    ]),
    "04-plc-otomasyon.json": (C_PLC, [
        plc_cpu,
        lambda: plc_module("iec.plc.di8", "PLC dijital giriş modülü (8 DI)", "DI 8",
                           [(f"DI{i}", "input") for i in range(8)], [("1M", "supplyNegative")],
                           ["PLC", "dijital giriş", "DI", "input"]),
        lambda: plc_module("iec.plc.do8", "PLC dijital çıkış modülü (8 DO)", "DO 8",
                           [("1L+", "supplyPositive"), ("1M", "supplyNegative")],
                           [(f"DO{i}", "output") for i in range(8)], ["PLC", "dijital çıkış", "DO", "output"]),
        lambda: plc_module("iec.plc.ai4", "PLC analog giriş modülü (4 AI)", "AI 4",
                           [("AI0", "input"), ("AI1", "input"), ("AI2", "input"), ("AI3", "input")],
                           [("M", "supplyNegative")], ["PLC", "analog giriş", "4-20mA", "0-10V"]),
        lambda: plc_module("iec.plc.ao2", "PLC analog çıkış modülü (2 AO)", "AO 2", [("L+", "supplyPositive")],
                           [("AO0", "output"), ("M0", "supplyNegative"), ("AO1", "output"), ("M1", "supplyNegative")],
                           ["PLC", "analog çıkış", "0-10V"]),
        lambda: plc_single("iec.plc.input", "PLC giriş (tek kanal)", "I", "input", "up"),
        lambda: plc_single("iec.plc.output", "PLC çıkış (tek kanal)", "Q", "output", "down"),
        hmi, eth_switch, rs485_term,
    ]),
    "05-sensor-saha.json": (C_SENS, [
        lambda: sensor("iec.sensor.pnp", "Endüktif sensör (PNP)", "PNP", ["endüktif", "proximity", "PNP", "3 telli"]),
        lambda: sensor("iec.sensor.npn", "Endüktif sensör (NPN)", "NPN", ["endüktif", "proximity", "NPN"]),
        lambda: sensor("iec.sensor.cap", "Kapasitif sensör (PNP)", "CAP", ["kapasitif", "seviye", "PNP"]),
        lambda: sensor("iec.sensor.photo", "Optik sensör (PNP)", "OPT", ["fotosel", "optik", "reflektör", "PNP"]),
        limitswitch, limitswitch_nc, safety_switch, door_sensor,
        lambda: switch_with_actuator("iec.pressureswitch.no", "Basınç şalteri (NO)", C_SENS + "/Basınç", "B", False, "P",
                                     ["presostat", "basınç şalteri", "pressure switch"]),
        lambda: switch_with_actuator("iec.pressureswitch.nc", "Basınç şalteri (NC)", C_SENS + "/Basınç", "B", True, "P",
                                     ["presostat", "basınç şalteri", "pressure switch"]),
        lambda: switch_with_actuator("iec.thermostat.nc", "Termostat (NC)", C_SENS + "/Sıcaklık", "B", True, "θ",
                                     ["termostat", "sıcaklık şalteri"]),
        lambda: switch_with_actuator("iec.levelswitch.no", "Seviye şalteri / flatör (NO)", C_SENS + "/Seviye", "B", False, "L",
                                     ["flatör", "seviye", "float"]),
        lambda: switch_with_actuator("iec.flowswitch.no", "Akış şalteri (NO)", C_SENS + "/Akış", "B", False, "F",
                                     ["akış", "flow switch"]),
        lambda: pt100(2), lambda: pt100(3), thermocouple, transmitter, encoder,
    ]),
    "06-kumanda-sinyal.json": (C_CTRL, [
        pushbutton_no, pushbutton_nc, illuminated_button, estop,
        lambda: selector("iec.selector.2pos", "Seçici şalter (2 konum)"),
        lambda: selector("iec.keyswitch", "Anahtarlı şalter", key=True),
        lamp, buzzer, horn, potentiometer,
    ]),
    "07-klemens-konnektor.json": (C_TERM, [
        terminal, terminal_pe, terminal_fused, terminal_disconnect,
        lambda: plug("iec.connector.plug", "Konnektör (erkek)", False),
        lambda: plug("iec.connector.socket", "Konnektör (dişi)", True),
        m12, earth, chassis, shield,
    ]),
    "08-olcum.json": (C_MEAS, [
        lambda: meter("iec.ammeter", "Ampermetre", "A", ["ampermetre", "akım"]),
        lambda: meter("iec.voltmeter", "Voltmetre", "V", ["voltmetre", "gerilim"]),
        ct, power_analyzer, energy_meter, hour_meter,
    ]),
    "09-enerji.json": (C_PWR, [
        psu, dc_distribution, battery, socket_outlet,
        lambda: transformer("iec.transformer.1ph", "Trafo (1 faz)", ("1U1", "1U2"), ("2U1", "2U2"), ["trafo", "transformatör"]),
        lambda: transformer("iec.transformer.control", "Kumanda trafosu 400/24V", ("400V", "0"), ("24V", "0V"),
                            ["kumanda trafosu", "24VAC"]),
    ]),
    "10-kurulum-diger.json": (C_GEN, [
        resistor, capacitor, diode, led, heater, heater_3ph, valve, light_fixture,
    ]),
}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    seen = set()
    total = 0
    for fname, (title, makers) in LIBRARY.items():
        symbols = []
        for mk in makers:
            s = mk().build()
            assert s["id"] not in seen, s["id"]
            seen.add(s["id"])
            ids = [p["id"] for p in s["pins"]]
            assert len(ids) == len(set(ids)), s["id"]
            for c in s.get("internalConnections", []):
                assert c["a"] in ids and c["b"] in ids, (s["id"], c)
            symbols.append(s)
        doc = {"formatVersion": 1, "library": "MYG IEC 60617", "version": LIB_VERSION, "title": title,
               "symbols": symbols}
        with open(os.path.join(OUT_DIR, fname), "w", encoding="utf-8", newline="\n") as f:
            json.dump(doc, f, ensure_ascii=False, indent=2)
            f.write("\n")
        total += len(symbols)
        print(f"{fname}: {len(symbols)}")
    print(f"toplam {total} sembol")


if __name__ == "__main__":
    sys.exit(main())
