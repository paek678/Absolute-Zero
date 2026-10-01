"""Loopback-only test transport: deterministic delay/loss, no production networking changes."""
import ctypes
import os
import heapq
import random
import select
import socket
import threading
import time


class MatrixUdpProxy:
    def __init__(self, server_port, delay_ms=0, loss=0, seed=1):
        self.server = ("127.0.0.1", server_port)
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.socket.bind(("127.0.0.1", 0))
        # Keep the proxy alive after the intentionally killed player's socket closes.
        if os.name == "nt":
            # CPython does not expose SIO_UDP_CONNRESET on all Windows builds.
            # Configure the socket explicitly instead of silently skipping the ioctl.
            from ctypes import wintypes
            ws2 = ctypes.WinDLL("Ws2_32.dll")
            ioctl = ws2.WSAIoctl
            ioctl.argtypes = [ctypes.c_size_t, wintypes.DWORD, ctypes.c_void_p,
                              wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD,
                              ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p, ctypes.c_void_p]
            ioctl.restype = ctypes.c_int
            disabled, returned = wintypes.BOOL(False), wintypes.DWORD()
            if ioctl(self.socket.fileno(), 0x9800000C, ctypes.byref(disabled),
                     ctypes.sizeof(disabled), None, 0, ctypes.byref(returned), None, None) != 0:
                error = ws2.WSAGetLastError()
                self.socket.close()
                raise OSError(error, "Failed to configure UDP proxy connection-reset behavior")
        self.port = self.socket.getsockname()[1]
        self.delay = delay_ms / 1000
        self.loss = loss
        self.uplink_hold_until = 0
        self.random = random.Random(seed)
        self.stop = threading.Event()
        self.stats = dict(received=0, dropped=0, forwarded=0, errors=0, error_details=[])
        self.thread = threading.Thread(target=self.run, daemon=True)
        self.thread.start()

    def run(self):
        client = None
        pending = []
        serial = 0
        while not self.stop.is_set():
            readable, _, _ = select.select([self.socket], [], [], .005)
            if readable:
                try:
                    data, source = self.socket.recvfrom(65535)
                    self.stats["received"] += 1
                    if source == self.server:
                        destination = client
                    else:
                        client = source
                        destination = self.server
                    if destination:
                        if self.random.random() < self.loss:
                            self.stats["dropped"] += 1
                        else:
                            serial += 1
                            due = time.monotonic() + self.delay
                            if destination == self.server:
                                due = max(due, self.uplink_hold_until)
                            heapq.heappush(pending, (due, serial, destination, data))
                except OSError as error:
                    self.record_error("receive", error)
            while pending and pending[0][0] <= time.monotonic():
                _, _, destination, data = heapq.heappop(pending)
                try:
                    self.socket.sendto(data, destination)
                    self.stats["forwarded"] += 1
                except OSError as error:
                    self.record_error("send", error)

    def record_error(self, operation, error):
        self.stats["errors"] += 1
        if len(self.stats["error_details"]) < 8:
            self.stats["error_details"].append(dict(operation=operation,
                errno=error.errno, winerror=getattr(error, "winerror", None),
                message=str(error), stopping=self.stop.is_set(), monotonic=time.monotonic()))

    def close(self):
        self.stop.set()
        self.thread.join(timeout=2)
        self.socket.close()
