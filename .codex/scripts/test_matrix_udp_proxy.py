"""Regression for endpoint loss in the local network-impairment harness."""
import socket
import time
import unittest

from matrix_udp_proxy import MatrixUdpProxy


class ProxyRecoveryTests(unittest.TestCase):
    def test_closed_endpoint_then_reopened_roundtrip(self):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reserved:
            reserved.bind(("127.0.0.1", 0))
            port = reserved.getsockname()[1]
        proxy = MatrixUdpProxy(port)
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client:
                client.settimeout(2)
                for _ in range(10):
                    client.sendto(b"closed", ("127.0.0.1", proxy.port))
                    time.sleep(.025)
                with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server:
                    server.bind(("127.0.0.1", port))
                    server.settimeout(2)
                    client.sendto(b"reopened", ("127.0.0.1", proxy.port))
                    data, source = server.recvfrom(128)
                    self.assertEqual(data, b"reopened")
                    server.sendto(b"reply", source)
                    self.assertEqual(client.recv(128), b"reply")
            self.assertEqual(proxy.stats["errors"], 0, proxy.stats)
        finally:
            proxy.close()
        self.assertFalse(proxy.thread.is_alive())

    def test_other_socket_errors_are_still_recorded(self):
        proxy = MatrixUdpProxy(9)
        try:
            proxy.record_error("send", OSError(123, "test failure"))
            self.assertEqual(proxy.stats["errors"], 1)
            self.assertEqual(proxy.stats["error_details"][0]["errno"], 123)
        finally:
            proxy.close()


if __name__ == "__main__":
    unittest.main()
