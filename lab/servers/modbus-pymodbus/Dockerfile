# PyModbus 3.12.0 (BSD-3-Clause) TCP server with the deterministic Proxus interop seed.
FROM python:3.12-slim-bookworm
RUN pip install --no-cache-dir pymodbus==3.12.0
COPY server.py /lab/server.py
EXPOSE 502
CMD ["python", "-u", "/lab/server.py", "--port", "502"]
