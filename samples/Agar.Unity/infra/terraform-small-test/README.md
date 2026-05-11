# Agar.Unity Distributed Test Terraform

This Terraform module creates a small distributed Alicloud test environment for the `samples/Agar.Unity` server.

Topology:

- 1 data ECS instance running PostgreSQL and Redis in Docker Compose
- `silo_count` Silo ECS instances
- `edge_count` Edge ECS instances
- Public Edge control-plane WebSocket on TCP `20000`
- Public Edge realtime KCP on UDP `20001`
- Orleans membership shared through PostgreSQL ADO.NET clustering

This is a distributed test topology for validating cross-machine Silo/Edge behavior. It is still not a production topology: PostgreSQL and Redis run on a single ECS instance, there is no ALB/TLS/autoscaling, and clients connect directly to one of the Edge public IPs.

## Prerequisites

Set Alicloud provider credentials before running Terraform:

```powershell
$env:ALICLOUD_ACCESS_KEY="***"
$env:ALICLOUD_SECRET_KEY="***"
$env:ALICLOUD_REGION="cn-hangzhou"
```

Build and push the two server images from the repository root:

```powershell
docker build -f samples/Agar.Unity/Server/Dockerfile --target edge -t registry.example.com/ulinkgame/agar-edge:small-test .
docker build -f samples/Agar.Unity/Server/Dockerfile --target silo -t registry.example.com/ulinkgame/agar-silo:small-test .
docker push registry.example.com/ulinkgame/agar-edge:small-test
docker push registry.example.com/ulinkgame/agar-silo:small-test
```

Copy `terraform.tfvars.example` to `terraform.tfvars`, then fill in:

- `image_id`
- `ssh_allowed_cidr`
- `key_pair_name` or `ssh_public_key`
- `edge_image`
- `silo_image`
- `postgres_password`
- `redis_password`

The server images must include the ADO.NET Orleans clustering support from this repository change. Rebuild and push images after changing the server code.

## Apply

```powershell
terraform init
terraform plan
terraform apply
```

Useful outputs:

- `control_plane_url`: Unity WebSocket control endpoints, one per Edge
- `realtime_endpoint`: Unity KCP endpoints, one per Edge
- `silo_private_ips`: private Silo node addresses
- `ssh_command`: SSH commands for all nodes

On the data node, inspect PostgreSQL and Redis with:

```bash
cd /opt/agar
docker compose ps
docker compose logs -f postgres redis
```

On Silo or Edge nodes, inspect containers with:

```bash
docker ps
docker logs -f agar-silo
docker logs -f agar-edge
```

## Notes

- This environment stores PostgreSQL and Redis data in Docker named volumes on the data ECS system disk.
- It is suitable for internal smoke tests and distributed manual multiplayer tests.
- It does not provide managed database, managed Redis, ALB, TLS, autoscaling, backups, or production-grade failover.
- `postgres_password` and `redis_password` are sensitive variables, but Terraform still stores rendered `user_data` in state. Keep state files private.
- Open SSH only to your own IP by setting `ssh_allowed_cidr` to a `/32` CIDR.
