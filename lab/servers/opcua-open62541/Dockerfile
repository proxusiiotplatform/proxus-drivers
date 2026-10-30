# open62541 v1.4.14 (MPL-2.0) with mbedTLS encryption, the history plugin and events, compiled together with the lab's deterministic server.
# Source is fetched at build time and is not redistributed by this repository. The certificate is created at
# image build time so it survives the container restarts that the fault harness performs.
FROM debian:bookworm-slim AS build
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates cmake make gcc libc6-dev \
      python3 libmbedtls-dev openssl \
 && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch v1.4.14 https://github.com/open62541/open62541.git /src \
 && cd /src && git submodule update --init --depth 1 deps/mdnsd deps/ua-nodeset 2>/dev/null || true
WORKDIR /src/build
RUN cmake -DCMAKE_BUILD_TYPE=Release -DUA_ENABLE_ENCRYPTION=MBEDTLS -DUA_NAMESPACE_ZERO=FULL -DUA_ENABLE_HISTORIZING=ON -DUA_ENABLE_SUBSCRIPTIONS_EVENTS=ON \
      -DUA_ENABLE_AMALGAMATION=OFF -DBUILD_SHARED_LIBS=OFF -DCMAKE_INSTALL_PREFIX=/usr/local .. \
 && make -j4 && make install
COPY server.c /lab/server.c
RUN gcc -O2 -o /lab/open62541-lab-server /lab/server.c -lopen62541 -lmbedtls -lmbedx509 -lmbedcrypto -lpthread -lm
RUN mkdir -p /lab/pki && openssl req -x509 -newkey rsa:2048 -sha256 -days 3650 -nodes \
      -keyout /lab/pki/server-key.pem -out /lab/pki/server.pem -subj "/CN=Proxus Lab open62541" \
      -addext "subjectAltName=URI:urn:proxus:lab:open62541,DNS:opcua-open62541,DNS:toxiproxy,DNS:localhost,IP:127.0.0.1" \
      -addext "keyUsage=critical,digitalSignature,nonRepudiation,keyEncipherment,dataEncipherment" \
      -addext "extendedKeyUsage=serverAuth,clientAuth" -addext "basicConstraints=critical,CA:FALSE" \
 && openssl x509 -in /lab/pki/server.pem -outform der -out /lab/pki/server.der \
 && openssl rsa -in /lab/pki/server-key.pem -outform der -out /lab/pki/server-key.der

FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends libmbedtls14 libmbedx509-1 libmbedcrypto7 \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /lab /lab
ENV LAB_OPCUA_HOSTNAME=opcua-open62541
EXPOSE 4840
CMD ["/lab/open62541-lab-server", "/lab/pki/server.der", "/lab/pki/server-key.der"]
