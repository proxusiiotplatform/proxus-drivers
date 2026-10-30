"""PyModbus lab server: the deterministic seed of
tools/Proxus.Drivers.Interop.Validation/reference/pymodbus_server.py plus a server-side write counter.

Every accepted write request (FC05, FC06, FC15, FC16, FC22, FC23) increments a counter that clients read
from input register 4095. The fault harness uses it to prove that an ambiguous write was applied at most once.
"""
import argparse

from pymodbus import FramerType
from pymodbus.datastore import ModbusDeviceContext, ModbusSequentialDataBlock, ModbusServerContext
from pymodbus.pdu.device import ModbusDeviceIdentification
from pymodbus.server import StartTcpServer

WRITE_COUNTER_ADDRESS = 4095
WRITE_FUNCTIONS = {5, 6, 15, 16, 22, 23}


class CountingDeviceContext(ModbusDeviceContext):
    writes = 0

    def setValues(self, func_code, address, values):
        super().setValues(func_code, address, values)
        if func_code in WRITE_FUNCTIONS:
            CountingDeviceContext.writes += 1
            super().setValues(4, WRITE_COUNTER_ADDRESS, [CountingDeviceContext.writes & 0xFFFF])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=502)
    parser.add_argument("--framer", choices=("socket", "rtu", "ascii"), default="socket",
                        help="socket = Modbus TCP (MBAP); rtu = Modbus RTU frames over TCP; ascii = Modbus ASCII frames over TCP")
    args = parser.parse_args()
    # ModbusDeviceContext maps protocol address 0 to data-block address 1, hence the leading slot.
    hr = [0] + [(i * 3 + 7) & 0xFFFF for i in range(4096)]
    ir = [0] + [(0x8000 + i) & 0xFFFF for i in range(4096)]
    co = [False] + [(i % 3) == 0 for i in range(4096)]
    di = [False] + [(i % 5) == 0 for i in range(4096)]
    ir[1 + WRITE_COUNTER_ADDRESS] = 0
    device = CountingDeviceContext(
        di=ModbusSequentialDataBlock(0, di), co=ModbusSequentialDataBlock(0, co),
        ir=ModbusSequentialDataBlock(0, ir), hr=ModbusSequentialDataBlock(0, hr))
    context = ModbusServerContext(devices=device, single=True)
    identity = ModbusDeviceIdentification(info_name={
        "VendorName": "PyModbus", "ProductCode": "PM312", "MajorMinorRevision": "3.12.0",
        "ProductName": "PyModbus Proxus Lab Server"})
    framer = {"rtu": FramerType.RTU, "ascii": FramerType.ASCII}.get(args.framer, FramerType.SOCKET)
    print(f"READY {args.port} {args.framer}", flush=True)
    StartTcpServer(context=context, identity=identity, address=(args.host, args.port), framer=framer)


if __name__ == "__main__":
    main()
