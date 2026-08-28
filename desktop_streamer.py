import ctypes
import json
import os
import re
import socket
import subprocess
import sys
import threading
import time

if getattr(sys, "frozen", False):
    os.environ["TCL_LIBRARY"] = os.path.join(sys._MEIPASS, "_tcl_data")
    os.environ["TK_LIBRARY"] = os.path.join(sys._MEIPASS, "_tk_data")

import tkinter as tk
from tkinter import ttk, messagebox

ROOT = os.path.dirname(sys.executable) if getattr(sys, "frozen", False) else os.path.dirname(os.path.abspath(__file__))
FFMPEG = os.path.join(ROOT, "ffmpeg.exe")
MEDIAMTX = os.path.join(ROOT, "mediamtx.exe")

user32 = ctypes.windll.user32
MONITORINFOF_PRIMARY = 1


def monitors():
    result = []
    MonitorEnumProc = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_int), ctypes.c_double)
    class RECT(ctypes.Structure):
        _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]
    class MONITORINFO(ctypes.Structure):
        _fields_ = [("cbSize", ctypes.c_ulong), ("rcMonitor", RECT), ("rcWork", RECT), ("dwFlags", ctypes.c_ulong)]
    def cb(handle, hdc, rect, data):
        info = MONITORINFO(); info.cbSize = ctypes.sizeof(info); user32.GetMonitorInfoW(handle, ctypes.byref(info))
        r = info.rcMonitor
        result.append((f"Монитор {len(result)+1} ({r.right-r.left}x{r.bottom-r.top})", {
            "input": "desktop", "x": r.left, "y": r.top,
            "width": r.right-r.left, "height": r.bottom-r.top
        }))
        return 1
    user32.EnumDisplayMonitors(0, 0, MonitorEnumProc(cb), 0)
    return result or [("Основной монитор", {"input": "desktop", "x": 0, "y": 0})]


def audio_devices():
    try:
        p = subprocess.run([FFMPEG, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"], capture_output=True, text=True, errors="replace")
        text = p.stderr
        names = []; in_audio = False
        for line in text.splitlines():
            if "DirectShow audio devices" in line: in_audio = True; continue
            if "DirectShow video devices" in line: in_audio = False; continue
            if "(audio)" in line: in_audio = True
            if not in_audio or "Alternative name" in line: continue
            m = re.search(r'"([^"]+)"', line)
            if m and "Alternative name" not in line and "DirectShow video devices" not in line and "DirectShow audio devices" not in line:
                name = m.group(1)
                if name not in names: names.append(name)
        return names
    except Exception:
        return []


def local_ip():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.connect(("8.8.8.8", 80))
        return sock.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        sock.close()


class App(tk.Tk):
    def __init__(self):
        super().__init__(); self.title("Desktop Streamer"); self.geometry("620x470"); self.resizable(False, False)
        self.ff = self.mx = self.ff_log = None; self.items = {}; self.status = tk.StringVar(value="Остановлено")
        self.build(); self.refresh_all(); self.protocol("WM_DELETE_WINDOW", self.close)

    def build(self):
        pad = {"padx": 14, "pady": 7}; frm = ttk.Frame(self, padding=16); frm.pack(fill="both", expand=True)
        ttk.Label(frm, text="Источник видео", font=("Segoe UI", 11, "bold")).grid(row=0, column=0, sticky="w", **pad)
        ttk.Label(frm, text="Монитор").grid(row=1,column=0,sticky="w",**pad)
        self.video = ttk.Combobox(frm, state="readonly", width=62); self.video.grid(row=1,column=1,columnspan=2,sticky="we",**pad)
        ttk.Button(frm,text="Обновить",command=self.refresh_source).grid(row=1,column=3,**pad)
        ttk.Label(frm,text="Разрешение",font=("Segoe UI", 11, "bold")).grid(row=2,column=0,sticky="w",**pad)
        self.resolution = ttk.Combobox(frm, state="readonly", values=["Исходное", "720p (1280x720)"], width=28); self.resolution.current(0); self.resolution.grid(row=3,column=0,sticky="w",**pad)
        ttk.Label(frm,text="Звук",font=("Segoe UI", 11, "bold")).grid(row=4,column=0,sticky="w",**pad)
        self.audio = ttk.Combobox(frm, state="readonly", width=62); self.audio.grid(row=5,column=0,columnspan=2,sticky="we",**pad); ttk.Button(frm,text="Обновить",command=self.refresh_audio).grid(row=5,column=2,**pad)
        ttk.Label(frm,text="RTSP порт").grid(row=6,column=0,sticky="w",**pad); self.port=ttk.Entry(frm,width=12); self.port.insert(0,"8554"); self.port.grid(row=6,column=1,sticky="w",**pad)
        ttk.Label(frm,text="Путь потока").grid(row=7,column=0,sticky="w",**pad); self.path=ttk.Entry(frm,width=30); self.path.insert(0,"desktop"); self.path.grid(row=7,column=1,sticky="w",**pad)
        self.button=ttk.Button(frm,text="Запустить поток",command=self.toggle); self.button.grid(row=8,column=0,columnspan=3,pady=18,ipadx=30,ipady=8)
        ttk.Label(frm,textvariable=self.status,foreground="#156b2f",font=("Segoe UI", 10, "bold")).grid(row=9,column=0,columnspan=3)
        self.url=tk.StringVar(value=""); ttk.Entry(frm,textvariable=self.url,state="readonly",width=55).grid(row=10,column=0,columnspan=2,pady=10,padx=14,sticky="we")
        self.copy_button=ttk.Button(frm,text="Копировать",command=self.copy_url,state="disabled"); self.copy_button.grid(row=10,column=2,padx=14)

    def refresh_all(self): self.refresh_source(); self.refresh_audio()
    def refresh_source(self):
        data = monitors(); self.items={x[0]:x[1] for x in data}; self.video["values"]=list(self.items); self.video.current(0 if data else -1)
    def refresh_audio(self):
        vals=["Без звука"]+audio_devices(); self.audio["values"]=vals; self.audio.current(0)
    def toggle(self): self.stop() if self.ff else self.start()
    def copy_url(self):
        value=self.url.get()
        if value: self.clipboard_clear(); self.clipboard_append(value); self.status.set("RTSP-адрес скопирован")
    def start(self):
        try: port=int(self.port.get()); path=self.path.get().strip() or "desktop"
        except ValueError: messagebox.showerror("Ошибка","RTSP порт должен быть числом"); return
        if not re.fullmatch(r"[A-Za-z0-9_-]+",path):
            messagebox.showerror("Ошибка","Путь потока может содержать только буквы, цифры, _ и -"); return
        if not self.video.get(): return
        config_path=os.path.join(ROOT,"streamer_mediamtx.yml")
        with open(config_path,"w",encoding="ascii") as config:
            config.write(f"rtspAddress: :{port}\nprotocols: [tcp]\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\npaths:\n  {path}:\n    source: publisher\n")
        mx_log_path=os.path.join(ROOT,"streamer_mediamtx.log")
        self.mx_log=open(mx_log_path,"w",encoding="utf-8")
        self.mx=subprocess.Popen([MEDIAMTX,config_path],cwd=ROOT,stdout=self.mx_log,stderr=subprocess.STDOUT,text=True)
        time.sleep(0.5)
        if self.mx.poll() is not None:
            self.mx_log.close(); self.mx_log=None; self.mx=None
            try:
                details=open(mx_log_path,"r",encoding="utf-8",errors="replace").read().strip().splitlines()[-1]
            except Exception: details="Не удалось запустить RTSP-сервер"
            messagebox.showerror("Ошибка запуска",details); return
        video=self.items[self.video.get()]; args=[FFMPEG,"-hide_banner","-loglevel","warning","-f","gdigrab","-framerate","15"]
        args += ["-offset_x",str(video.get("x",0)),"-offset_y",str(video.get("y",0))]
        if video.get("width"): args += ["-video_size",f'{video["width"]}x{video["height"]}']
        args += ["-i",video["input"]]
        audio=self.audio.get()
        if audio and audio!="Без звука": args += ["-f","dshow","-i",f"audio={audio}"]
        vf=[]
        if self.resolution.get().startswith("720p"): vf=["scale=-2:720"]
        args += (["-vf",vf[0]] if vf else []) + ["-c:v","libx264","-preset","veryfast","-tune","zerolatency","-pix_fmt","yuv420p","-b:v","2500k","-g","30"]
        if audio and audio!="Без звука": args += ["-c:a","aac","-b:a","128k","-ar","48000","-ac","2"]
        args += ["-f","rtsp","-rtsp_transport","tcp",f"rtsp://127.0.0.1:{port}/{path}"]
        self.ff_log=open(os.path.join(ROOT,"streamer_ffmpeg.log"),"w",encoding="utf-8")
        self.ff=subprocess.Popen(args,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=self.ff_log,text=True); self.button.config(text="Остановить поток"); self.status.set("Поток запущен"); self.url.set(f"rtsp://{local_ip()}:{port}/{path}"); self.copy_button.config(state="normal")
        threading.Thread(target=self.watch,args=(self.ff,),daemon=True).start()
    def watch(self,p):
        p.wait()
        if self.ff is p: self.after(0,lambda:(self.stop(),self.status.set("Поток завершён")))
    def stop(self):
        for proc_name in ("ff", "mx"):
            proc=getattr(self,proc_name)
            if proc:
                proc.terminate()
                try: proc.wait(timeout=2)
                except subprocess.TimeoutExpired: proc.kill(); proc.wait(timeout=2)
                setattr(self,proc_name,None)
        if self.ff_log: self.ff_log.close(); self.ff_log=None
        if getattr(self,"mx_log",None): self.mx_log.close(); self.mx_log=None
        self.button.config(text="Запустить поток"); self.status.set("Остановлено"); self.url.set("")
        self.copy_button.config(state="disabled")
    def close(self): self.stop(); self.destroy()

if __name__ == "__main__": App().mainloop()
