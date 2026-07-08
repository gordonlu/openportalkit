# Docker

Local services for OpenPortalKit development.

## Prerequisite

Docker Compose v2+ plugin is required:

```bash
docker compose version
```

If `apt` cannot locate `docker-compose-plugin`, your machine is likely using Ubuntu's default Docker packages instead of Docker's official apt repository.
The package is provided by Docker's apt repository, not every Ubuntu mirror.

Official references:

- Docker Engine on Ubuntu: <https://docs.docker.com/engine/install/ubuntu/>
- Docker Compose plugin on Linux: <https://docs.docker.com/compose/install/linux/>

If `docker.io`, old `docker-compose`, `containerd`, or `runc` packages were installed from Ubuntu's repository, remove the conflicting packages first:

```bash
sudo apt remove docker.io docker-compose docker-compose-v2 docker-doc podman-docker containerd runc
```

Recommended Docker apt repository setup:

```bash
sudo apt-get update
sudo apt-get install ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

sudo tee /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF

sudo apt-get update
sudo apt-get install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
docker compose version
```

Manual user-local Compose plugin fallback, matching Docker's Linux install documentation.
This example is for `x86_64`; replace `x86_64` with `aarch64` on ARM64 machines, and replace `v5.1.2` when Docker publishes a newer Compose release you want to use.

```bash
DOCKER_CONFIG=${DOCKER_CONFIG:-$HOME/.docker}
mkdir -p "$DOCKER_CONFIG/cli-plugins"
curl -SL https://github.com/docker/compose/releases/download/v5.1.2/docker-compose-linux-x86_64 \
  -o "$DOCKER_CONFIG/cli-plugins/docker-compose"
chmod +x "$DOCKER_CONFIG/cli-plugins/docker-compose"
docker compose version
```

## Start Services

```bash
cp docker/.env.example docker/.env
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d
```

If `5432` is already used by a local PostgreSQL server, change `POSTGRES_PORT` in `docker/.env` before starting services:

```env
POSTGRES_PORT=15432
```

Then use this development connection string for the Docker database:

```txt
Host=localhost;Port=15432;Database=openportalkit;Username=openportalkit;Password=openportalkit_dev
```

If image pulls time out against Docker Hub, verify normal Docker access first:

```bash
docker info
docker pull postgres:17
```

On networks where Docker Hub is slow or blocked, configure a Docker daemon proxy or registry mirror in `/etc/docker/daemon.json`, then restart Docker. Keep this as a machine-local setting; do not commit private mirror or proxy URLs to this repository.

## Services

- PostgreSQL 17 on `localhost:5432`
- Redis 8 on `localhost:6379`

Default PostgreSQL connection string:

```txt
Host=localhost;Port=5432;Database=openportalkit;Username=openportalkit;Password=openportalkit_dev
```

Default Redis connection string:

```txt
localhost:6379
```

## Health

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
docker compose --env-file docker/.env -f docker/docker-compose.yml logs postgres
docker compose --env-file docker/.env -f docker/docker-compose.yml logs redis
```

The application hosts are not containerized yet.
