# ECS Fargate trial

One Linux Fargate task runs the relay and Caddy. Caddy discovers the task's
public IPv4 address through AWS's HTTPS check-IP endpoint and requests a trusted
short-lived certificate. Containers in one task share localhost; the relay binds
only to `127.0.0.1:8080`. Only TCP 80/443 are allowed from the Internet.

```text
Players -- WSS :443 --> Caddy -- localhost :8080 --> Relay
                       one Fargate task, one public IP
```

This trial uses 0.25 vCPU and 512 MB RAM. No load balancer, NAT gateway, EFS,
or domain is provisioned. Use a public subnet with a direct internet-gateway
route. VPC endpoints, private subnets and outbound NAT are not supported by the
public-IP discovery method.

## Deploy from Windows

The repository-root batch files use the saved account, region, profile, VPC and
subnet in `server-settings.json`:

Replace `YOUR_AWS_ACCOUNT_ID` with your 12-digit AWS account ID before running.
To keep your personal settings outside source control, copy this file to a local
path and pass it to a batch file with `-SettingsPath C:\path\server-settings.json`.
VPC and subnet IDs identify resources; they do not grant access, but a shared
configuration must use IDs belonging to the account and region being deployed.

```bat
Run-Server.bat
Update-Server.bat
Stop-Server.bat
```

Run starts Docker Desktop if needed, signs in through the browser if credentials
expired, builds the current local source (including uncommitted changes), uploads
new image tags, provisions the ECS stack and waits for trusted HTTPS. It does not
pull or change your Git branch. Every run rebuilds from source; Docker only reuses
layers whose inputs have not changed. Base images are refreshed by default.

After pulling the source revision you want to deploy, run **Update-Server.bat**
to rebuild and replace an existing server. It uses the same settings/profile as
Run, requires an existing stable stack, and keeps the old service running during
image builds and uploads. Build or upload failures leave that deployment alone.
After both fresh images are uploaded, it updates the existing stack and waits
for trusted HTTPS, then prints the connection URL and protocol. The task's public
IP can change; players must use the newly printed URL.

Updating disconnects players and resets in-memory rooms/progress when the task
is replaced. It does not delete the stack or image repositories. Like Run, Update
builds your current local source, including uncommitted changes; it does not pull
Git, switch branches or use the published Windows `Relay.exe`.

```bat
Update-Server.bat
Update-Server.bat -SettingsPath "C:\path\server-settings.json"
Update-Server.bat -BaseImages Cached
```

Stop disconnects players and permanently deletes this ECS stack, its logs and
both retained ECR repositories/images. It waits for tasks to stop before deleting
images. Run recreates everything on the next start and prints a new endpoint.
Stopping twice is safe. Cleanup errors produce a nonzero exit code instead of
claiming that charges have stopped. The existing EC2 instance, its disks, your
VPC/subnet and the deployment user's IAM policies remain untouched.

All three scripts work from any current directory and pause so double-clicked windows
stay readable. Set `SPIDEYNET_NO_PAUSE=1` for unattended use. For a base-image
registry outage, `Run-Server.bat -BaseImages Cached` still builds the latest local
source using already cached base images.
Temporary registry connection failures are retried up to three times. Persistent
failures stop deployment and explain the cached-build option; compilation errors
are not retried. Cached mode still needs registry access if a required base image
is absent from Docker's build cache.

The batch files check the AWS account before changing resources. Settings contain
no credentials. Install AWS CLI and Docker Desktop once; the IAM user and boundary
from `IAM.md` must already exist. The PowerShell scripts below also remain usable
directly.

Requires Docker Desktop with Linux containers, PowerShell, and AWS CLI 2.32+
for browser sign-in. First create the [scoped deployment user](IAM.md).
Update AWS CLI using https://awscli.amazonaws.com/AWSCLIV2.msi
and open a new terminal. Use a separate profile so old default credentials do
not override the sign-in:

```powershell
aws login --profile spideynet --region ap-southeast-2
aws sts get-caller-identity --profile spideynet --region ap-southeast-2
```

The deploying identity needs CloudFormation, ECR, ECS, CloudWatch Logs, EC2
network read/security-group management, and IAM role creation/pass-role access.
The containers receive no application IAM role or AWS credentials. The execution
role only pulls the two private images and sends container logs.

Use the VPC and public subnet saved in `server-settings.json`. To deploy with
different networking, supply the actual IDs from the VPC console:

```powershell
.\SpideyNet\deploy\ecs\Deploy-Ecs.ps1 -VpcId vpc-REPLACE -SubnetId subnet-REPLACE
```

The script verifies public routing, creates an idle CloudFormation stack and
two private ECR repositories, builds/pushes uniquely tagged images, and starts
one task. It prints the new endpoint and CloudWatch log group. Certificate
issuance can finish after ECS reports the service stable. Verify the displayed
HTTPS `/health` URL without bypassing certificate validation, then connect all
clients to the displayed `wss://` address using the same room key/settings.

## Update, stop and troubleshoot

Run the deployment script again to rebuild and update. It first stops the old
task, so updates interrupt games. It never runs two independent room servers
at once. An interrupted build leaves the service stopped; rerun the script.
If the base-image registry is unavailable and the official images are already
cached locally, add `-BaseImages Cached` to build with that cache. The default
refreshes base images. Registry authentication uses an owner-only temporary
Docker configuration, removed after upload; desktop credentials are unchanged.

```powershell
.\SpideyNet\deploy\ecs\Get-EcsEndpoint.ps1
```

Use the repository-root `Run-Server.bat` and `Stop-Server.bat` for starting and
stopping. The former desired-count-only helper has been removed: it retained
billable logs and images and could restart an outdated image.

Every replacement task can get a different public IP. Rediscover the endpoint
after starting it. Rooms, shared progress and certificates are ephemeral;
replacement clears them and requests a certificate for the new IP. Frequent
replacements can hit certificate issuance limits. Retaining the same public IP
or certificates across tasks is outside this trial's scope.

Inspect the ECS service Events tab and the stack's CloudWatch log group when a
task fails. Caddy prints `Relay endpoint: wss://...` before requesting its
certificate. Its health check tests the relay; it does not prove that a public
certificate has been issued. Failed starts roll back where ECS has a previous
successful deployment; inspect failures rather than repeatedly redeploying.

## Costs and removal

Fargate compute and the public IP are billed while the task runs, including idle
time. ECR image storage, CloudWatch ingestion/storage, and excess outbound
traffic are additional charges. Logs expire after seven days. Stopping the
service with desired count zero stops compute/public-IP charges, not registry
or log-storage charges. Budget alerts do not cap spending.

Use `Stop-Server.bat` for complete removal, including the retained ECR repositories.
Deleting only the CloudFormation stack manually retains those repositories and
their images. No existing EC2/VPC/subnet resources are deleted by this stack.

## Check lifecycle scripts

`Test-ServerScripts.ps1` mocks AWS, Docker and the health request. It checks fresh
builds/tags, sign-in, account safety, credential cleanup, stop/delete order,
repeated cleanup and failure propagation without touching a real deployment.
